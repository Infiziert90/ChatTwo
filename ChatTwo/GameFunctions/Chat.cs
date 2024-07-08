using System.Numerics;
using System.Text;
using ChatTwo.Code;
using ChatTwo.GameFunctions.Types;
using ChatTwo.Resources;
using ChatTwo.Util;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Config;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Application.Network;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Text.ReadOnly;

using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace ChatTwo.GameFunctions;

internal sealed unsafe class Chat : IDisposable
{
    // Functions
    [Signature("E8 ?? ?? ?? ?? 48 8D 4D A0 8B F8")]
    private readonly delegate* unmanaged<nint, Utf8String*, nint, uint> GetKeybindNative = null!;

    [Signature("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 48 8D B9 ?? ?? ?? ?? 33 C0")]
    private readonly delegate* unmanaged<RaptureLogModule*, ushort, Utf8String*, Utf8String*, ulong, ulong, ushort, byte, int, byte, void> PrintTellNative = null!;

    [Signature("E8 ?? ?? ?? ?? 48 8D 4C 24 ?? E8 ?? ?? ?? ?? 48 8D 8C 24 ?? ?? ?? ?? E8 ?? ?? ?? ?? B0 01")]
    private readonly delegate* unmanaged<NetworkModule*, ulong, ushort, Utf8String*, Utf8String*, ushort, ushort, bool> SendTellNative = null!;

    // Client::UI::AddonChatLog.OnRefresh
    [Signature("40 53 56 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 49 8B F0 8B FA", DetourName = nameof(ChatLogRefreshDetour))]
    private Hook<ChatLogRefreshDelegate>? ChatLogRefreshHook { get; init; }
    private delegate byte ChatLogRefreshDelegate(nint log, ushort eventId, AtkValue* value);

    private Hook<AgentChatLog.Delegates.ChangeChannelName> ChangeChannelNameHook { get; init; }
    private Hook<RaptureShellModule.Delegates.ReplyInSelectedChatMode>? ReplyInSelectedChatModeHook { get; init; }
    private Hook<RaptureShellModule.Delegates.SetContextTellTarget>? SetChatLogTellTargetHook { get; init; }
    private Hook<RaptureShellModule.Delegates.SetContextTellTargetInForay>? EurekaContextMenuTellHook { get; init; }

    // Pointers

    [Signature("48 8D 35 ?? ?? ?? ?? 8B 05", ScanType = ScanType.StaticAddress)]
    private readonly char* CurrentCharacter = null!;

    // Events

    internal event ChatActivatedEventDelegate? Activated;
    internal delegate void ChatActivatedEventDelegate(ChatActivatedArgs args);

    private Plugin Plugin { get; }

    /// <summary>
    /// Holds the current game channel details.
    /// `TellPlayerName` and `TellWorldId` are only set when the channel is `InputChannel.Tell`.
    /// </summary>
    internal (InputChannel Channel, List<Chunk> Name, string? TellPlayerName, ushort TellWorldId) Channel { get; private set; }

    internal bool UsesTellTempChannel { get; set; }
    internal InputChannel? PreviousChannel { get; private set; }

    private bool DirectChat;
    private long LastRefresh;

    internal Chat(Plugin plugin)
    {
        Plugin = plugin;
        Plugin.GameInteropProvider.InitializeFromAttributes(this);

        ChatLogRefreshHook?.Enable();

        ChangeChannelNameHook = Plugin.GameInteropProvider.HookFromAddress<AgentChatLog.Delegates.ChangeChannelName>(AgentChatLog.MemberFunctionPointers.ChangeChannelName, ChangeChannelNameDetour);
        ChangeChannelNameHook.Enable();

        ReplyInSelectedChatModeHook = Plugin.GameInteropProvider.HookFromAddress<RaptureShellModule.Delegates.ReplyInSelectedChatMode>(RaptureShellModule.MemberFunctionPointers.ReplyInSelectedChatMode, ReplyInSelectedChatModeDetour);
        ReplyInSelectedChatModeHook.Enable();

        SetChatLogTellTargetHook = Plugin.GameInteropProvider.HookFromAddress<RaptureShellModule.Delegates.SetContextTellTarget>(RaptureShellModule.MemberFunctionPointers.SetContextTellTarget, SetContextTellTarget);
        SetChatLogTellTargetHook.Enable();

        // EurekaContextMenuTellHook = Plugin.GameInteropProvider.HookFromAddress<RaptureShellModule.Delegates.SetContextTellTargetInForay>(RaptureShellModule.MemberFunctionPointers.SetContextTellTargetInForay, SetContextTellTargetInForay);
        // EurekaContextMenuTellHook.Enable();

        Plugin.Framework.Update += InterceptKeybinds;
        Plugin.ClientState.Login += Login;
        Login();
    }

    public void Dispose()
    {
        Plugin.ClientState.Login -= Login;
        Plugin.Framework.Update -= InterceptKeybinds;

        SetChatLogTellTargetHook?.Dispose();
        ReplyInSelectedChatModeHook?.Dispose();
        ChangeChannelNameHook?.Dispose();
        ChatLogRefreshHook?.Dispose();
        EurekaContextMenuTellHook?.Dispose();

        Activated = null;
    }

    internal string? GetLinkshellName(uint idx)
    {
        var utf = InfoProxyChat.Instance()->GetLinkShellName(idx);
        return utf == null ? null : MemoryHelper.ReadStringNullTerminated((nint) utf);
    }

    internal string? GetCrossLinkshellName(uint idx)
    {
        var utf = InfoProxyCrossWorldLinkshell.Instance()->GetCrossworldLinkshellName(idx);
        return utf == null ? null : utf->ToString();
    }

    private static int GetRotateIdx(RotateMode mode) => mode switch
    {
        RotateMode.Forward => 1,
        RotateMode.Reverse => -1,
        _ => 0,
    };

    internal static int RotateLinkshellHistory(RotateMode mode)
    {
        var uiModule = UIModule.Instance();
        if (mode == RotateMode.None)
            uiModule->LinkshellCycle = -1;

        return uiModule->RotateLinkshellHistory(GetRotateIdx(mode));
    }

    internal static int RotateCrossLinkshellHistory(RotateMode mode) =>
        UIModule.Instance()->RotateCrossLinkshellHistory(GetRotateIdx(mode));

    // This function looks up a channel's user-defined color.
    // If this function ever returns 0, it returns null instead.
    internal uint? GetChannelColor(ChatType type)
    {
        var parent = new ChatCode((ushort) type).Parent();
        switch (parent)
        {
            case ChatType.Debug:
            case ChatType.Urgent:
            case ChatType.Notice:
                return type.DefaultColor();
        }

        Plugin.GameConfig.TryGet(parent.ToConfigEntry(), out uint color);

        var rgb = color & 0xFFFFFF;
        if (rgb == 0)
            return null;

        return 0xFF | (rgb << 8);
    }

    private readonly Dictionary<string, Keybind> _keybinds = new();
    internal IReadOnlyDictionary<string, Keybind> Keybinds => _keybinds;

    internal static readonly IReadOnlyDictionary<string, ChannelSwitchInfo> KeybindsToIntercept = new Dictionary<string, ChannelSwitchInfo>
    {
        ["CMD_CHAT"] = new(null),
        ["CMD_COMMAND"] = new(null, text: "/"),
        ["CMD_REPLY"] = new(InputChannel.Tell, rotate: RotateMode.Forward),
        ["CMD_REPLY_REV"] = new(InputChannel.Tell, rotate: RotateMode.Reverse),
        ["CMD_SAY"] = new(InputChannel.Say),
        ["CMD_YELL"] = new(InputChannel.Yell),
        ["CMD_SHOUT"] = new(InputChannel.Shout),
        ["CMD_PARTY"] = new(InputChannel.Party),
        ["CMD_ALLIANCE"] = new(InputChannel.Alliance),
        ["CMD_FREECOM"] = new(InputChannel.FreeCompany),
        ["PVPTEAM_CHAT"] = new(InputChannel.PvpTeam),
        ["CMD_CWLINKSHELL"] = new(InputChannel.CrossLinkshell1, rotate: RotateMode.Forward),
        ["CMD_CWLINKSHELL_REV"] = new(InputChannel.CrossLinkshell1, rotate: RotateMode.Reverse),
        ["CMD_CWLINKSHELL_1"] = new(InputChannel.CrossLinkshell1),
        ["CMD_CWLINKSHELL_2"] = new(InputChannel.CrossLinkshell2),
        ["CMD_CWLINKSHELL_3"] = new(InputChannel.CrossLinkshell3),
        ["CMD_CWLINKSHELL_4"] = new(InputChannel.CrossLinkshell4),
        ["CMD_CWLINKSHELL_5"] = new(InputChannel.CrossLinkshell5),
        ["CMD_CWLINKSHELL_6"] = new(InputChannel.CrossLinkshell6),
        ["CMD_CWLINKSHELL_7"] = new(InputChannel.CrossLinkshell7),
        ["CMD_CWLINKSHELL_8"] = new(InputChannel.CrossLinkshell8),
        ["CMD_LINKSHELL"] = new(InputChannel.Linkshell1, rotate: RotateMode.Forward),
        ["CMD_LINKSHELL_REV"] = new(InputChannel.Linkshell1, rotate: RotateMode.Reverse),
        ["CMD_LINKSHELL_1"] = new(InputChannel.Linkshell1),
        ["CMD_LINKSHELL_2"] = new(InputChannel.Linkshell2),
        ["CMD_LINKSHELL_3"] = new(InputChannel.Linkshell3),
        ["CMD_LINKSHELL_4"] = new(InputChannel.Linkshell4),
        ["CMD_LINKSHELL_5"] = new(InputChannel.Linkshell5),
        ["CMD_LINKSHELL_6"] = new(InputChannel.Linkshell6),
        ["CMD_LINKSHELL_7"] = new(InputChannel.Linkshell7),
        ["CMD_LINKSHELL_8"] = new(InputChannel.Linkshell8),
        ["CMD_BEGINNER"] = new(InputChannel.NoviceNetwork),
        ["CMD_REPLY_ALWAYS"] = new(InputChannel.Tell, true, RotateMode.Forward),
        ["CMD_REPLY_REV_ALWAYS"] = new(InputChannel.Tell, true, RotateMode.Reverse),
        ["CMD_SAY_ALWAYS"] = new(InputChannel.Say, true),
        ["CMD_YELL_ALWAYS"] = new(InputChannel.Yell, true),
        ["CMD_PARTY_ALWAYS"] = new(InputChannel.Party, true),
        ["CMD_ALLIANCE_ALWAYS"] = new(InputChannel.Alliance, true),
        ["CMD_FREECOM_ALWAYS"] = new(InputChannel.FreeCompany, true),
        ["PVPTEAM_CHAT_ALWAYS"] = new(InputChannel.PvpTeam, true),
        ["CMD_CWLINKSHELL_ALWAYS"] = new(InputChannel.CrossLinkshell1, true, RotateMode.Forward),
        ["CMD_CWLINKSHELL_ALWAYS_REV"] = new(InputChannel.CrossLinkshell1, true, RotateMode.Reverse),
        ["CMD_CWLINKSHELL_1_ALWAYS"] = new(InputChannel.CrossLinkshell1, true),
        ["CMD_CWLINKSHELL_2_ALWAYS"] = new(InputChannel.CrossLinkshell2, true),
        ["CMD_CWLINKSHELL_3_ALWAYS"] = new(InputChannel.CrossLinkshell3, true),
        ["CMD_CWLINKSHELL_4_ALWAYS"] = new(InputChannel.CrossLinkshell4, true),
        ["CMD_CWLINKSHELL_5_ALWAYS"] = new(InputChannel.CrossLinkshell5, true),
        ["CMD_CWLINKSHELL_6_ALWAYS"] = new(InputChannel.CrossLinkshell6, true),
        ["CMD_CWLINKSHELL_7_ALWAYS"] = new(InputChannel.CrossLinkshell7, true),
        ["CMD_CWLINKSHELL_8_ALWAYS"] = new(InputChannel.CrossLinkshell8, true),
        ["CMD_LINKSHELL_ALWAYS"] = new(InputChannel.Linkshell1, true, RotateMode.Forward),
        ["CMD_LINKSHELL_REV_ALWAYS"] = new(InputChannel.Linkshell1, true, RotateMode.Reverse),
        ["CMD_LINKSHELL_1_ALWAYS"] = new(InputChannel.Linkshell1, true),
        ["CMD_LINKSHELL_2_ALWAYS"] = new(InputChannel.Linkshell2, true),
        ["CMD_LINKSHELL_3_ALWAYS"] = new(InputChannel.Linkshell3, true),
        ["CMD_LINKSHELL_4_ALWAYS"] = new(InputChannel.Linkshell4, true),
        ["CMD_LINKSHELL_5_ALWAYS"] = new(InputChannel.Linkshell5, true),
        ["CMD_LINKSHELL_6_ALWAYS"] = new(InputChannel.Linkshell6, true),
        ["CMD_LINKSHELL_7_ALWAYS"] = new(InputChannel.Linkshell7, true),
        ["CMD_LINKSHELL_8_ALWAYS"] = new(InputChannel.Linkshell8, true),
        ["CMD_BEGINNER_ALWAYS"] = new(InputChannel.NoviceNetwork, true),
    };

    private void UpdateKeybinds()
    {
        foreach (var name in KeybindsToIntercept.Keys)
        {
            var keybind = GetKeybind(name);
            if (keybind is null)
                continue;

            _keybinds[name] = keybind;
        }
    }

    private void InterceptKeybinds(IFramework framework1)
    {
        // Refresh current keybinds every 5s
        if (LastRefresh + 5 * 1000 < Environment.TickCount64)
        {
            UpdateKeybinds();
            DirectChat = Plugin.GameConfig.TryGet(UiControlOption.DirectChat, out bool option) && option;
            LastRefresh = Environment.TickCount64;
        }

        if (Plugin.ChatLogWindow is { CurrentTab.InputDisabled: true, IsHidden: false })
            return;

        // Vanilla text input has focus
        if (RaptureAtkModule.Instance()->AtkModule.IsTextInputActive())
            return;

        var modifierState = (ModifierFlag) 0;
        foreach (var modifier in Enum.GetValues<ModifierFlag>())
        {
            var modifierKey = GetKeyForModifier(modifier);
            if (modifierKey != VirtualKey.NO_KEY && Plugin.KeyState[modifierKey])
                modifierState |= modifier;
        }

        var turnedOff = new Dictionary<VirtualKey, (uint, string)>();
        foreach (var toIntercept in KeybindsToIntercept.Keys)
        {
            if (!Keybinds.TryGetValue(toIntercept, out var keybind))
                continue;

            if (toIntercept is "CMD_CHAT" or "CMD_COMMAND")
            {
                // Direct chat option is selected
                if (DirectChat)
                    continue;
            }

            void Intercept(VirtualKey key, ModifierFlag modifier)
            {
                if (!Plugin.KeyState.IsVirtualKeyValid(key))
                    return;

                var modifierPressed = Plugin.Config.KeybindMode switch
                {
                    KeybindMode.Strict => modifier == modifierState,
                    KeybindMode.Flexible => modifierState.HasFlag(modifier),
                    _ => false,
                };

                if (!modifierPressed)
                    return;

                if (!Plugin.KeyState[key])
                    return;

                var bits = BitOperations.PopCount((uint) modifier);
                if (!turnedOff.TryGetValue(key, out var previousBits) || previousBits.Item1 < bits)
                    turnedOff[key] = ((uint) bits, toIntercept);
            }

            Intercept(keybind.Key1, keybind.Modifier1);
            Intercept(keybind.Key2, keybind.Modifier2);
        }

        foreach (var (key, (_, keybind)) in turnedOff)
        {
            Plugin.KeyState[key] = false;
            if (!KeybindsToIntercept.TryGetValue(keybind, out var info))
                continue;

            try
            {
                Activated?.Invoke(new ChatActivatedArgs(info) { TellReason = TellReason.Reply, });
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Error in chat Activated event");
            }
        }
    }

    private void Login()
    {
        var agent = AgentChatLog.Instance();
        if (agent == null)
            return;

        ChangeChannelNameDetour(agent);
    }

    private byte ChatLogRefreshDetour(nint log, ushort eventId, AtkValue* value)
    {
        if (Plugin is { ChatLogWindow.CurrentTab.InputDisabled: true })
            return ChatLogRefreshHook!.Original(log, eventId, value);

        if (eventId != 0x31 || value == null || value->UInt is not (0x05 or 0x0C))
            return ChatLogRefreshHook!.Original(log, eventId, value);

        if (DirectChat && CurrentCharacter != null)
        {
            // FIXME: this whole system sucks
            // FIXME v2: I hate everything about this, but it works
            Plugin.Framework.RunOnTick(() =>
            {
                string? input = null;

                var utf8Bytes = MemoryHelper.ReadRaw((nint)CurrentCharacter+0x4, 2);
                var chars = Encoding.UTF8.GetString(utf8Bytes).ToCharArray();
                if (chars.Length == 0)
                    return;

                var c = chars[0];
                if (c != '\0' && !char.IsControl(c))
                    input = c.ToString();

                try
                {
                    Activated?.Invoke(new ChatActivatedArgs(new ChannelSwitchInfo(null)) { Input = input, });
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error(ex, "Error in chat Activated event");
                }
            });
        }

        string? addIfNotPresent = null;

        var str = value + 2;
        if (str != null && ((int) str->Type & 0xF) == (int) ValueType.String && str->String != null)
        {
            var add = MemoryHelper.ReadStringNullTerminated((nint) str->String);
            if (add.Length > 0)
                addIfNotPresent = add;
        }

        try
        {
            Activated?.Invoke(new ChatActivatedArgs(new ChannelSwitchInfo(null)) { AddIfNotPresent = addIfNotPresent, });
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error in chat Activated event");
        }

        // prevent the game from focusing the chat log
        return 1;
    }

    private byte* ChangeChannelNameDetour(AgentChatLog* agent)
    {
        var ret = ChangeChannelNameHook.Original(agent);
        if (agent == null)
            return ret;

        var channel = (uint) RaptureShellModule.Instance()->ChatType;
        if (channel is 17 or 18)
            channel = (uint) InputChannel.Tell;

        var name = SeString.Parse(agent->ChannelLabel);
        if (name.Payloads.Count == 0)
            name = null;

        if (name == null)
            return ret;

        var nameChunks = ChunkUtil.ToChunks(name, ChunkSource.None, null).ToList();
        if (nameChunks.Count > 0 && nameChunks[0] is TextChunk text)
            text.Content = text.Content.TrimStart('\uE01E').TrimStart();

        string? playerName = null;
        ushort worldId = 0;
        if (channel == (uint) InputChannel.Tell)
        {
            playerName = SeString.Parse(agent->TellPlayerName).TextValue;
            worldId = agent->TellWorldId;
            Plugin.Log.Debug($"Detected tell target '{playerName}'@{worldId}");
        }

        Channel = ((InputChannel) channel, nameChunks, playerName, worldId);

        return ret;
    }

    private void ReplyInSelectedChatModeDetour(RaptureShellModule* agent)
    {
        var replyMode = AgentChatLog.Instance()->ReplyChannel;
        if (replyMode == -2)
        {
            ReplyInSelectedChatModeHook!.Original(agent);
            return;
        }

        SetChannel((InputChannel) replyMode);
        ReplyInSelectedChatModeHook!.Original(agent);
    }

    private bool SetContextTellTarget(RaptureShellModule* a1, Utf8String* playerName, Utf8String* worldName, ushort worldId, ulong accountId, ulong contentId, ushort reason, bool setChatType)
    {
        if (playerName != null)
        {
            try
            {
                var target = new TellTarget(playerName->ToString(), worldId, contentId, (TellReason) reason);
                Activated?.Invoke(new ChatActivatedArgs(new ChannelSwitchInfo(InputChannel.Tell))
                {
                    TellReason = (TellReason) reason,
                    TellTarget = target,
                });
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Error in chat Activated event");
            }
        }

        return SetChatLogTellTargetHook!.Original(a1, playerName, worldName, worldId, accountId, contentId, reason, setChatType);
    }

    // private void SetContextTellTargetInForay(RaptureShellModule* a1, Utf8String* playerName, Utf8String* worldName, ushort worldId, ulong accountId, ulong contentId, ushort reason)
    // {
    //     Plugin.Log.Information($"SetContextTellTargetInForay");
    //     if (!UsesTellTempChannel)
    //     {
    //         UsesTellTempChannel = true;
    //         PreviousChannel = Channel.Channel;
    //     }
    //
    //     if (playerName != null)
    //     {
    //         try
    //         {
    //             Plugin.Log.Information($"Name {playerName->ToString()} World {worldName->ToString()} WorldId {worldId} accountId {accountId} ContentId {contentId} Reason {reason} rapture reason {a1->TellReason}");
    //             var target = new TellTarget(playerName->ToString(), worldId, contentId, (TellReason) reason);
    //             Activated?.Invoke(new ChatActivatedArgs(new ChannelSwitchInfo(InputChannel.Tell))
    //             {
    //                 TellReason = (TellReason) reason,
    //                 TellTarget = target,
    //             });
    //         }
    //         catch (Exception ex)
    //         {
    //             Plugin.Log.Error(ex, "Error in chat Activated event");
    //         }
    //     }
    //
    //     EurekaContextMenuTellHook!.Original(a1, playerName, worldName, worldId, accountId, contentId, reason);
    // }

    internal static void SetChannel(InputChannel channel, string? tellTarget = null)
    {
        // ExtraChat linkshells aren't supported in game so we never want to
        // call the ChangeChatChannel function with them.
        //
        // Callers should call ChatLogWindow.SetChannel() which handles
        // ExtraChat channels
        if (channel.IsExtraChatLinkshell())
            return;

        var target = Utf8String.FromString(tellTarget ?? "");
        var idx = channel.LinkshellIndex();
        if (idx == uint.MaxValue)
            idx = 0;

        RaptureShellModule.Instance()->ChangeChatChannel((int) channel, idx, target, true);
        target->Dtor(true);
    }

    internal void SetEurekaTellChannel(string name, string worldName, ushort worldId, ulong accountId, ulong objectId, ushort reason, bool setChatType)
    {
        // param6 is 0 for contentId and 1 for objectId
        // param7 is always 0 ?

        if (!UsesTellTempChannel)
        {
            UsesTellTempChannel = true;
            PreviousChannel = Channel.Channel;
        }

        var utfName = Utf8String.FromString(name);
        var utfWorld = Utf8String.FromString(worldName);

        RaptureShellModule.Instance()->SetTellTargetInForay(utfName, utfWorld, worldId, accountId, objectId, reason, setChatType);

        utfName->Dtor(true);
        utfWorld->Dtor(true);
    }

    private static VirtualKey GetKeyForModifier(ModifierFlag modifierFlag) => modifierFlag switch
    {
        ModifierFlag.Shift => VirtualKey.SHIFT,
        ModifierFlag.Ctrl => VirtualKey.CONTROL,
        ModifierFlag.Alt => VirtualKey.MENU,
        _ => VirtualKey.NO_KEY,
    };

    private Keybind? GetKeybind(string id)
    {
        var agent = (nint) AgentModule.Instance()->GetAgentByInternalId(AgentId.Configkey);
        var a1 = *(void**) (agent + 0x78);
        if (a1 == null)
            return null;

        var outData = stackalloc byte[32];
        var idString = Utf8String.FromString(id);
        GetKeybindNative((nint) a1, idString, (nint) outData);
        idString->Dtor(true);

        var key1 = (VirtualKey) outData[0];
        if (key1 is VirtualKey.F23)
            key1 = VirtualKey.OEM_2;

        var key2 = (VirtualKey) outData[2];
        if (key2 is VirtualKey.F23)
            key2 = VirtualKey.OEM_2;

        return new Keybind
        {
            Key1 = key1,
            Modifier1 = (ModifierFlag) outData[1],
            Key2 = key2,
            Modifier2 = (ModifierFlag) outData[3],
        };
    }

    internal TellHistoryInfo? GetTellHistoryInfo(int index)
    {
        var acquaintance = AcquaintanceModule.Instance()->GetTellHistory(index);
        if (acquaintance->ContentId == 0)
            return null;

        var name = new ReadOnlySeStringSpan(acquaintance->Name.AsSpan()).ExtractText();
        var world = acquaintance->WorldId;
        var contentId = acquaintance->ContentId;

        return new TellHistoryInfo(name, world, contentId);
    }

    internal void SendTell(TellReason reason, ulong contentId, string name, ushort homeWorld, byte[] message, string rawText)
    {
        var uName = Utf8String.FromString(name);
        var uMessage = Utf8String.FromSequence(message);

        var encoded = Utf8String.FromUtf8String(PronounModule.Instance()->ProcessString(uMessage, true));
        var decoded = EncodeMessage(rawText);
        AutoTranslate.ReplaceWithPayload(ref decoded);
        using var decodedUtf8String = new Utf8String(decoded);

        var logModule = RaptureLogModule.Instance();
        var networkModule = Framework.Instance()->GetNetworkModuleProxy()->NetworkModule;

        // TODO: Remap TellReasons
        if (reason == TellReason.Direct)
            reason = TellReason.Friend;

        var ok = SendTellNative(networkModule, contentId, homeWorld, uName, encoded, (ushort) reason, homeWorld);
        if (ok)
            PrintTellNative(logModule, 33, uName, &decodedUtf8String, 0, contentId, homeWorld, 255, 0, 0);
        else
            Plugin.ChatGui.PrintError(Language.Chat_SendTell_Error);

        encoded->Dtor(true);
        uName->Dtor(true);
        uMessage->Dtor(true);
    }

    private static byte[] EncodeMessage(string str) {
        using var input = new Utf8String(str);
        using var ouput = new Utf8String();

        input.Copy(PronounModule.Instance()->ProcessString(&input, true));
        ouput.Copy(PronounModule.Instance()->ProcessString(&input, false));
        return ouput.AsSpan().ToArray();
    }

    internal bool IsCharValid(char c)
    {
        var uC = Utf8String.FromString(c.ToString());

        uC->SanitizeString(0x27F, Utf8String.CreateEmpty());
        var wasValid = uC->ToString().Length > 0;

        uC->Dtor(true);

        return wasValid;
    }
}
