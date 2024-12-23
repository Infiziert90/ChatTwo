using System.Text;
using ChatTwo.Code;
using ChatTwo.GameFunctions.Types;
using ChatTwo.Resources;
using ChatTwo.Util;
using Dalamud.Game.Config;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Memory;
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
    [Signature("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 48 8D B9 ?? ?? ?? ?? 33 C0")]
    private readonly delegate* unmanaged<RaptureLogModule*, ushort, Utf8String*, Utf8String*, ulong, ulong, ushort, byte, int, byte, void> PrintTellNative = null!;

    [Signature("E8 ?? ?? ?? ?? 48 8D 4C 24 ?? E8 ?? ?? ?? ?? 48 8D 8C 24 ?? ?? ?? ?? E8 ?? ?? ?? ?? B0 ?? 48 8B 8C 24")]
    private readonly delegate* unmanaged<NetworkModule*, ulong, ushort, Utf8String*, Utf8String*, ushort, ushort, byte> SendTellNative = null!;

    // Client::UI::AddonChatLog.OnRefresh
    [Signature("40 53 56 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 49 8B F0 8B FA", DetourName = nameof(ChatLogRefreshDetour))]
    private Hook<ChatLogRefreshDelegate>? ChatLogRefreshHook { get; init; }
    private delegate byte ChatLogRefreshDelegate(nint log, ushort eventId, AtkValue* value);

    // Replace with CS version later
    [Signature("48 89 5C 24 ?? 55 56 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 83 B9", DetourName = nameof(ContextMenuTellInForayDetour))]
    private Hook<ContextMenuTellInForayDelegate>? ContextMenuTellInForayHook { get; set; }
    private delegate void ContextMenuTellInForayDelegate(RaptureShellModule* module, Utf8String* playerName, Utf8String* worldName, ushort worldId, ulong accountId, ulong contentId, ushort reason);

    private Hook<AgentChatLog.Delegates.ChangeChannelName> ChangeChannelNameHook { get; init; }
    private Hook<RaptureShellModule.Delegates.ReplyInSelectedChatMode>? ReplyInSelectedChatModeHook { get; init; }
    private Hook<RaptureShellModule.Delegates.SetContextTellTarget>? SetChatLogTellTargetHook { get; init; }

    // Pointers
    [Signature("48 8D 35 ?? ?? ?? ?? 8B 05", ScanType = ScanType.StaticAddress)]
    private readonly char* CurrentCharacter = null!;

    private Plugin Plugin { get; }

    private enum PlayerNameDisplayType : uint
    {
        FullName = 0,
        SurnameAbbreviated = 1,
        ForenameAbbreviated = 2,
        Initials = 3
    }

    private long LastPlayerNameDisplayTypeRefresh;
    private PlayerNameDisplayType CurrentPlayerNameDisplayType = PlayerNameDisplayType.FullName;

    internal Chat(Plugin plugin)
    {
        Plugin = plugin;
        Plugin.GameInteropProvider.InitializeFromAttributes(this);

        ChatLogRefreshHook?.Enable();
        ContextMenuTellInForayHook?.Enable();

        ChangeChannelNameHook = Plugin.GameInteropProvider.HookFromAddress<AgentChatLog.Delegates.ChangeChannelName>(AgentChatLog.MemberFunctionPointers.ChangeChannelName, ChangeChannelNameDetour);
        ChangeChannelNameHook.Enable();

        ReplyInSelectedChatModeHook = Plugin.GameInteropProvider.HookFromAddress<RaptureShellModule.Delegates.ReplyInSelectedChatMode>(RaptureShellModule.MemberFunctionPointers.ReplyInSelectedChatMode, ReplyInSelectedChatModeDetour);
        ReplyInSelectedChatModeHook.Enable();

        SetChatLogTellTargetHook = Plugin.GameInteropProvider.HookFromAddress<RaptureShellModule.Delegates.SetContextTellTarget>(RaptureShellModule.MemberFunctionPointers.SetContextTellTarget, SetContextTellTarget);
        SetChatLogTellTargetHook.Enable();

        Plugin.ClientState.Login += Login;
        Login();
    }

    public void Dispose()
    {
        Plugin.ClientState.Login -= Login;

        SetChatLogTellTargetHook?.Dispose();
        ReplyInSelectedChatModeHook?.Dispose();
        ChangeChannelNameHook?.Dispose();
        ChatLogRefreshHook?.Dispose();
        ContextMenuTellInForayHook?.Dispose();
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

    internal static void RotateLinkshellHistory(RotateMode mode)
    {
        var uiModule = UIModule.Instance();
        if (mode == RotateMode.None)
            uiModule->LinkshellCycle = -1;

        uiModule->RotateLinkshellHistory(GetRotateIdx(mode));
    }

    internal static void RotateCrossLinkshellHistory(RotateMode mode) =>
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

    private void Login()
    {
        var agent = AgentChatLog.Instance();
        if (agent == null)
            return;

        ChangeChannelNameDetour(agent);

        // Inform all clients that a new login happened
        Plugin.ServerCore.SendNewLogin();
    }

    private byte ChatLogRefreshDetour(nint log, ushort eventId, AtkValue* value)
    {
        if (Plugin.CurrentTab.InputDisabled)
            return ChatLogRefreshHook!.Original(log, eventId, value);

        if (eventId != 0x31 || value == null || value->UInt is not (0x05 or 0x0C))
            return ChatLogRefreshHook!.Original(log, eventId, value);

        if (Plugin.Functions.KeybindManager.DirectChat && CurrentCharacter != null)
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
                    Plugin.ChatLogWindow.Activated(new ChatActivatedArgs(new ChannelSwitchInfo(null)) { Input = input, });
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
            // We already called this function once, so we skip the duplicated call
            // Also return the original value here so that vanilla chat receives all information
            if (Plugin.ChatLogWindow.TellSpecial)
            {
                Plugin.Log.Information("Return early to prevent duplicated call...");
                return ChatLogRefreshHook!.Original(log, eventId, value);
            }

            Plugin.ChatLogWindow.Activated(new ChatActivatedArgs(new ChannelSwitchInfo(null)) { AddIfNotPresent = addIfNotPresent, });
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

        Plugin.CurrentTab.CurrentChannel = new UsedChannel
        {
            Channel = (InputChannel) channel,
            Name = nameChunks,
            TellTarget = playerName != null ? new TellTarget(playerName, worldId, 0, 0) : null
        };

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
                Plugin.ChatLogWindow.Activated(new ChatActivatedArgs(new ChannelSwitchInfo(InputChannel.Tell, permanent: setChatType))
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

    private void ContextMenuTellInForayDetour(RaptureShellModule* a1, Utf8String* playerName, Utf8String* worldName, ushort worldId, ulong accountId, ulong contentId, ushort reason)
    {
        if (!Plugin.CurrentTab.CurrentChannel.UseTempChannel)
            Plugin.CurrentTab.CurrentChannel.UseTempChannel = true;

        if (playerName != null)
        {
            try
            {
                var target = new TellTarget(playerName->ToString(), worldId, contentId, (TellReason) reason);
                Plugin.ChatLogWindow.Activated(new ChatActivatedArgs(new ChannelSwitchInfo(InputChannel.Tell))
                {
                    TellReason = (TellReason) reason,
                    TellTarget = target,
                    TellSpecial = Sheets.IsInForay(), // Handle Eureka/Bozja special
                });
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Error in chat Activated event");
            }
        }

        ContextMenuTellInForayHook!.Original(a1, playerName, worldName, worldId, accountId, contentId, reason);
    }

    /// <summary>
    /// Returns true if the channel is any non-linkshell channel, or if the
    /// linkshell actually exists.
    /// </summary>
    internal static bool ValidAnyLinkshell(InputChannel channel)
    {
        var idx = channel.LinkshellIndex();
        if (idx == uint.MaxValue || channel.IsExtraChatLinkshell())
            return true;
        if (channel.IsLinkshell() && ValidLinkshell(idx))
            return true;
        if (channel.IsCrossLinkshell() && ValidCrossLinkshell(idx))
            return true;
        return false;
    }

    internal static bool ValidLinkshell(uint idx)
    {
        if (idx > 7)
            return false;
        return InfoProxyLinkshell.Instance()->LinkShells[(int) idx].Id != 0;
    }

    internal static bool ValidCrossLinkshell(uint idx)
    {
        if (idx > 7)
            return false;
        return InfoProxyCrossWorldLinkshell.Instance()->CrossWorldLinkshells[(int) idx].Name.Length > 0;
    }

    private static uint? RotateLinkshell(uint currentIndex, RotateMode rotate, Func<uint, bool> validFn)
    {
        if (rotate == RotateMode.None)
            return null;

        var delta = rotate switch
        {
            RotateMode.Forward => 1,
            RotateMode.Reverse => -1,
        };

        // Iterate up to 8 times to find a valid linkshell.
        for (var i = 0; i < 8; i++)
        {
            currentIndex = (uint) ((8 + currentIndex + delta) % 8);
            if (validFn(currentIndex))
                return currentIndex;
        }

        return null;
    }

    internal static InputChannel? ResolveTempInputChannel(InputChannel? currentTempChannel, InputChannel channel, RotateMode rotate)
    {
        switch (channel)
        {
            case InputChannel.Linkshell1 or InputChannel.CrossLinkshell1 when rotate != RotateMode.None:
            {
                var module = UIModule.Instance();

                var currentIndex = channel is InputChannel.Linkshell1 ? (uint) module->LinkshellCycle : (uint) module->CrossWorldLinkshellCycle;
                if (currentTempChannel != null)
                {
                    switch (channel)
                    {
                        case InputChannel.Linkshell1 when currentTempChannel.Value.IsLinkshell():
                        case InputChannel.CrossLinkshell1 when currentTempChannel.Value.IsCrossLinkshell():
                            currentIndex = currentTempChannel.Value.LinkshellIndex();
                            break;
                    }
                }

                var idx = RotateLinkshell(currentIndex, rotate, channel == InputChannel.Linkshell1 ? ValidLinkshell : ValidCrossLinkshell);
                return channel + idx;
            }
            default:
                return channel;
        }
    }

    internal void SetChannel(InputChannel channel, TellTarget? tellTarget = null)
    {
        // ExtraChat linkshells aren't supported in game so we never want to
        // call the ChangeChatChannel function with them.
        //
        // Callers should call ChatLogWindow.SetChannel() which handles
        // ExtraChat channels
        if (channel.IsExtraChatLinkshell())
            return;

        var target = Utf8String.FromString(tellTarget?.ToTargetString() ?? "");
        var idx = channel.LinkshellIndex();
        if (idx == uint.MaxValue)
            idx = 0;

        if (!ValidAnyLinkshell(channel))
            return;

        RaptureShellModule.Instance()->ChangeChatChannel(tellTarget != null ? 17 : (int)channel, idx, target, true);
        target->Dtor(true);
    }

    internal void SetEurekaTellChannel(string name, string worldName, ushort worldId, ulong accountId, ulong objectId, ushort reason, bool setChatType)
    {
        // param6 is 0 for contentId and 1 for objectId
        // param7 is always 0 ?

        if (!Plugin.CurrentTab.CurrentChannel.UseTempChannel)
            Plugin.CurrentTab.CurrentChannel.UseTempChannel = true;

        var utfName = Utf8String.FromString(name);
        var utfWorld = Utf8String.FromString(worldName);

        RaptureShellModule.Instance()->SetTellTargetInForay(utfName, utfWorld, worldId, accountId, objectId, reason, setChatType);

        utfName->Dtor(true);
        utfWorld->Dtor(true);
    }

    internal TellHistoryInfo? GetTellHistoryInfo(int index)
    {
        var acquaintance = AcquaintanceModule.Instance()->GetTellHistory(index);
        if (acquaintance == null || acquaintance->ContentId == 0)
            return null;

        var name = new ReadOnlySeStringSpan(acquaintance->Name.AsSpan()).ExtractText();
        var world = acquaintance->WorldId;
        var contentId = acquaintance->ContentId;

        return new TellHistoryInfo(name, world, contentId);
    }

    internal void SendTellUsingCommandInner(byte[] message)
    {
        var mes = Utf8String.FromSequence(message);

        RaptureShellModule.Instance()->ExecuteCommandInner(mes, UIModule.Instance());
        RaptureAtkModule.Instance()->ClearFocus(); // Clear the focus of vanilla chat that was still active

        mes->Dtor(true);
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

        // // TODO: Remap TellReasons
        if (reason == TellReason.Direct)
            reason = TellReason.Friend;

        if (contentId == 0)
        {
            encoded->Dtor(true);
            uName->Dtor(true);
            uMessage->Dtor(true);

            Plugin.Log.Warning("Tried to send a tell with content id being 0");
            return;
        }

        var ok = SendTellNative(networkModule, contentId, homeWorld, uName, encoded, (ushort) reason, homeWorld);
        if (ok == 1)
        {
            PrintTellNative(logModule, 33, uName, &decodedUtf8String, 0, contentId, homeWorld, 255, 0, 0);
        }
        else
        {
            Plugin.ChatGui.PrintError(Language.Chat_SendTell_Error);
        }

        encoded->Dtor(true);
        uName->Dtor(true);
        uMessage->Dtor(true);
    }

    private static byte[] EncodeMessage(string str) {
        using var input = new Utf8String(str);
        using var output = new Utf8String();

        input.Copy(PronounModule.Instance()->ProcessString(&input, true));
        output.Copy(PronounModule.Instance()->ProcessString(&input, false));
        return output.AsSpan().ToArray();
    }

    internal bool IsCharValid(char c)
    {
        var uC = Utf8String.FromString(c.ToString());

        uC->SanitizeString(0x27F, Utf8String.CreateEmpty());
        var wasValid = uC->ToString().Length > 0;

        uC->Dtor(true);

        return wasValid;
    }

    private PlayerNameDisplayType GetNameDisplayType()
    {
        var ok = Plugin.GameConfig.TryGet(UiConfigOption.LogNameType, out uint type);
        if (!ok || !Enum.IsDefined(typeof(PlayerNameDisplayType), type))
            return PlayerNameDisplayType.FullName;
        return (PlayerNameDisplayType) type;
    }

    internal string AbbreviatePlayerName(string playerName)
    {
        if (LastPlayerNameDisplayTypeRefresh + 5_000 < Environment.TickCount64)
        {
            LastPlayerNameDisplayTypeRefresh = Environment.TickCount64;
            CurrentPlayerNameDisplayType = GetNameDisplayType();
        }

        if (CurrentPlayerNameDisplayType == PlayerNameDisplayType.FullName)
            return playerName;

        var split = playerName.Split(' ');
        if (split.Length != 2)
            return playerName;

        return CurrentPlayerNameDisplayType switch
        {
            PlayerNameDisplayType.SurnameAbbreviated => $"{split.First()} {split.Last().FirstOrDefault('A')}.",
            PlayerNameDisplayType.ForenameAbbreviated => $"{split.First().FirstOrDefault('A')}. {split.Last()}",
            PlayerNameDisplayType.Initials => $"{split.First().FirstOrDefault('A')}. {split.Last().FirstOrDefault('A')}.",
            _ => playerName
        };
    }

    internal bool CheckHideFlags()
    {
        // Only hide the chat in a cutscene when the vanilla chat would've
        // also been hidden. This prevents Chat 2 from hiding for a split
        // second before the cutscene actually starts, because the game sets
        // the cutscene conditions before processing the skip.
        var raptureAtkUnitManager = RaptureAtkUnitManager.Instance();
        return raptureAtkUnitManager == null || raptureAtkUnitManager->UiFlags.HasFlag(UIModule.UiFlags.Chat);
    }
}
