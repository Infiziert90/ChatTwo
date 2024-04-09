using System.Numerics;
using ChatTwo.Code;
using ChatTwo.GameFunctions.Types;
using ChatTwo.Util;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Config;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace ChatTwo.GameFunctions;

internal sealed unsafe class Chat : IDisposable {
    // Functions

    [Signature("E8 ?? ?? ?? ?? 0F B7 44 37 ??", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<RaptureShellModule*, int, uint, Utf8String*, byte, void> ChangeChatChannel = null!;

    [Signature("48 89 5C 24 ?? 55 56 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 48 8B 02", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<RaptureShellModule*, Utf8String*, Utf8String*, ushort, ulong, ushort, byte, bool> SetChannelTargetTell = null!;

    [Signature("4C 8B 81 ?? ?? ?? ?? 4D 85 C0 74 17", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<RaptureLogModule*, uint, ulong> GetContentIdForChatEntry = null!;

    [Signature("E8 ?? ?? ?? ?? 48 8D 4D A0 8B F8")]
    private readonly delegate* unmanaged<IntPtr, Utf8String*, IntPtr, uint> GetKeybindNative = null!;

    [Signature("E8 ?? ?? ?? ?? 48 3B F0 74 35")]
    private readonly delegate* unmanaged<AtkStage*, IntPtr> GetFocus = null!;

    [Signature("44 8B 89 ?? ?? ?? ?? 4C 8B C1 45 85 C9")]
    private readonly delegate* unmanaged<void*, int, IntPtr> GetTellHistory = null!;

    [Signature("E8 ?? ?? ?? ?? 48 8D 4D 50 E8 ?? ?? ?? ?? 48 8B 17")]
    private readonly delegate* unmanaged<RaptureLogModule*, ushort, Utf8String*, Utf8String*, ulong, ushort, byte, int, byte, void> PrintTell = null!;

    [Signature("E8 ?? ?? ?? ?? 48 8D 4C 24 ?? E8 ?? ?? ?? ?? 48 8D 8C 24 ?? ?? ?? ?? E8 ?? ?? ?? ?? B0 01")]
    private readonly delegate* unmanaged<IntPtr, ulong, ushort, Utf8String*, Utf8String*, byte, ulong, byte> SendTellNative = null!;

    [Signature("E8 ?? ?? ?? ?? F6 43 0A 40")]
    private readonly delegate* unmanaged<Framework*, IntPtr> GetNetworkModule = null!;

    [Signature("E8 ?? ?? ?? ?? 48 8B C8 E8 ?? ?? ?? ?? 45 8D 46 FB")]
    private readonly delegate* unmanaged<IntPtr, uint, Utf8String*> GetCrossLinkshellNameNative = null!;

    [Signature("3B 51 10 73 0F 8B C2 48 83 C0 0B")]
    private readonly delegate* unmanaged<IntPtr, uint, ulong*> GetLinkshellInfo = null!;

    [Signature("E8 ?? ?? ?? ?? 4C 8B C8 44 8D 47 01")]
    private readonly delegate* unmanaged<IntPtr, ulong, byte*> GetLinkshellNameNative = null!;

    [Signature("40 56 41 54 41 55 41 57 48 83 EC 28 48 8B 01", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<UIModule*, int, ulong> RotateLinkshellHistoryNative;

    [Signature("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 56 41 57 48 83 EC 20 48 8B 01 44 8B F2", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<UIModule*, int, ulong> RotateCrossLinkshellHistoryNative;

    [Signature("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 8B F2 48 8D B9")]
    private readonly delegate* unmanaged<IntPtr, uint, IntPtr> GetColourInfo = null!;

    [Signature("E8 ?? ?? ?? ?? EB 0A 48 8D 4C 24 ?? E8 ?? ?? ?? ?? 48 8D 8D")]
    private readonly delegate* unmanaged<Utf8String*, int, IntPtr, void> SanitiseString = null!;

    // Hooks

    private delegate byte ChatLogRefreshDelegate(IntPtr log, ushort eventId, AtkValue* value);

    private delegate IntPtr ChangeChannelNameDelegate(IntPtr agent);

    private delegate void ReplyInSelectedChatModeDelegate(AgentInterface* agent);

    private delegate byte SetChatLogTellTarget(IntPtr a1, Utf8String* name, Utf8String* a3, ushort world, ulong contentId, ushort a6, byte a7);

    private delegate void EurekaContextMenuTellDelegate(RaptureShellModule* param1, Utf8String* playerName, Utf8String* worldName, ushort world, ulong contentId, ushort param6);

    [Signature(
        "40 53 56 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 49 8B F0 8B FA",
        DetourName = nameof(ChatLogRefreshDetour)
    )]
    private Hook<ChatLogRefreshDelegate>? ChatLogRefreshHook { get; init; }

    [Signature(
        "E8 ?? ?? ?? ?? BA ?? ?? ?? ?? 48 8D 4D B0 48 8B F8 E8 ?? ?? ?? ?? 41 8B D6",
        DetourName = nameof(ChangeChannelNameDetour)
    )]
    private Hook<ChangeChannelNameDelegate>? ChangeChannelNameHook { get; init; }

    [Signature(
        "48 89 5C 24 ?? 57 48 83 EC 30 8B B9 ?? ?? ?? ?? 48 8B D9 83 FF FE",
        DetourName = nameof(ReplyInSelectedChatModeDetour)
    )]
    private Hook<ReplyInSelectedChatModeDelegate>? ReplyInSelectedChatModeHook { get; init; }

    [Signature(
        "E8 ?? ?? ?? ?? 4C 8B 7C 24 ?? EB 34",
        DetourName = nameof(SetChatLogTellTargetDetour)
    )]
    private Hook<SetChatLogTellTarget>? SetChatLogTellTargetHook { get; init; }

    [Signature(
        "E8 ?? ?? ?? ?? EB 8A 48 8B 1D",
        DetourName = nameof(EurekaContextMenuTell)
    )]
    private Hook<EurekaContextMenuTellDelegate>? EurekaContextMenuTellHook { get; init; }

    // Offsets

    #pragma warning disable 0649

    [Signature("8B B9 ?? ?? ?? ?? 48 8B D9 83 FF FE 0F 84", Offset = 2)]
    private readonly int? ReplyChannelOffset;

    [Signature("89 83 ?? ?? ?? ?? 48 8B 01 83 FE 13 7C 05 41 8B D4 EB 03 83 CA FF FF 90", Offset = 2)]
    private readonly int? ShellChannelOffset;

    [Signature("4C 8D B6 ?? ?? ?? ?? 41 8B 1E 45 85 E4 74 7A 33 FF 8B EF 66 0F 1F 44 00", Offset = 3)]
    private readonly int? LinkshellCycleOffset;

    [Signature("BA ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B F0 48 85 C0 0F 84 ?? ?? ?? ?? 48 8B 10 33", Offset = 1)]
    private readonly uint? LinkshellInfoProxyIdx;

    [Signature("BA ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 89 6C 24 ?? 4C 8B E0 48 89 74 24", Offset = 1)]
    private readonly uint? CrossLinkshellInfoProxyIdx;

    #pragma warning restore 0649

    // Pointers

    [Signature("48 8D 15 ?? ?? ?? ?? 0F B6 C8 48 8D 05", ScanType = ScanType.StaticAddress)]
    private readonly char* CurrentCharacter = null!;

    [Signature("48 8D 0D ?? ?? ?? ?? 8B 14 ?? 85 D2 7E ?? 48 8B 0D ?? ?? ?? ?? 48 83 C1 10 E8 ?? ?? ?? ?? 8B 70 ?? 41 8D 4D", ScanType = ScanType.StaticAddress)]
    private IntPtr ColourLookup { get; init; }

    // Events

    internal delegate void ChatActivatedEventDelegate(ChatActivatedArgs args);

    internal event ChatActivatedEventDelegate? Activated;

    private Plugin Plugin { get; }
    internal (InputChannel channel, List<Chunk> name) Channel { get; private set; }

    internal bool UsesTellTempChannel { get; set; }
    internal InputChannel? PreviousChannel { get; private set; }

    internal Chat(Plugin plugin) {
        Plugin = plugin;
        Plugin.GameInteropProvider.InitializeFromAttributes(this);

        ChatLogRefreshHook?.Enable();
        ChangeChannelNameHook?.Enable();
        ReplyInSelectedChatModeHook?.Enable();
        SetChatLogTellTargetHook?.Enable();
        EurekaContextMenuTellHook?.Enable();

        Plugin.Framework.Update += InterceptKeybinds;
        Plugin.ClientState.Login += Login;
        Login();
    }

    public void Dispose() {
        Plugin.ClientState.Login -= Login;
        Plugin.Framework.Update -= InterceptKeybinds;

        SetChatLogTellTargetHook?.Dispose();
        ReplyInSelectedChatModeHook?.Dispose();
        ChangeChannelNameHook?.Dispose();
        ChatLogRefreshHook?.Dispose();
        EurekaContextMenuTellHook?.Dispose();

        Activated = null;
    }

    internal string? GetLinkshellName(uint idx) {
        if (LinkshellInfoProxyIdx is not { } proxyIdx) {
            return null;
        }

        var infoProxy = Plugin.Functions.GetInfoProxyByIndex(proxyIdx);
        if (infoProxy == IntPtr.Zero) {
            return null;
        }

        var lsInfo = GetLinkshellInfo(infoProxy, idx);
        if (lsInfo == null) {
            return null;
        }

        var utf = GetLinkshellNameNative(infoProxy, *lsInfo);
        return utf == null ? null : MemoryHelper.ReadStringNullTerminated((IntPtr) utf);
    }

    internal string? GetCrossLinkshellName(uint idx) {
        if (CrossLinkshellInfoProxyIdx is not { } proxyIdx) {
            return null;
        }

        var infoProxy = Plugin.Functions.GetInfoProxyByIndex(proxyIdx);
        if (infoProxy == IntPtr.Zero) {
            return null;
        }

        var utf = GetCrossLinkshellNameNative(infoProxy, idx);
        return utf == null ? null : utf->ToString();
    }

    internal ulong RotateLinkshellHistory(RotateMode mode) {
        if (mode == RotateMode.None && LinkshellCycleOffset != null) {
            // for the branch at 6.08: 5E1680
            var uiModule = (IntPtr) Framework.Instance()->GetUiModule();
            *(int*) (uiModule + LinkshellCycleOffset.Value) = -1;
        }

        return RotateLinkshellHistoryInternal(RotateLinkshellHistoryNative, mode);
    }

    internal ulong RotateCrossLinkshellHistory(RotateMode mode) => RotateLinkshellHistoryInternal(RotateCrossLinkshellHistoryNative, mode);

    private static ulong RotateLinkshellHistoryInternal(delegate* unmanaged<UIModule*, int, ulong> func, RotateMode mode) {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        if (func == null) {
            return 0;
        }

        var idx = mode switch {
            RotateMode.Forward => 1,
            RotateMode.Reverse => -1,
            _ => 0,
        };

        var uiModule = Framework.Instance()->GetUiModule();
        return func(uiModule, idx);
    }

    // This function looks up a channel's user-defined colour.
    //
    // If this function would ever return 0, it returns null instead.
    internal uint? GetChannelColour(ChatType type) {
        if (GetColourInfo == null || ColourLookup == IntPtr.Zero) {
            return null;
        }

        // Colours are retrieved by looking up their code in a lookup table. Some codes share a colour, so they're lumped into a parent code here.
        // Only codes >= 10 (say) have configurable colours.
        // After getting the lookup value for the code, it is passed into a function with a handler which returns a pointer.
        // This pointer + 32 is the RGB value. This functions returns RGBA with A always max.

        var parent = new ChatCode((ushort) type).Parent();

        switch (parent) {
            case ChatType.Debug:
            case ChatType.Urgent:
            case ChatType.Notice:
                return type.DefaultColour();
        }

        var framework = (IntPtr) Framework.Instance();

        var lookupResult = *(uint*) (ColourLookup + (int) parent * 4);
        var info = GetColourInfo(framework + 16, lookupResult);
        var rgb = *(uint*) (info + 32) & 0xFFFFFF;

        if (rgb == 0) {
            return null;
        }

        return 0xFF | (rgb << 8);
    }

    private readonly Dictionary<string, Keybind> _keybinds = new();
    internal IReadOnlyDictionary<string, Keybind> Keybinds => _keybinds;

    internal static readonly IReadOnlyDictionary<string, ChannelSwitchInfo> KeybindsToIntercept = new Dictionary<string, ChannelSwitchInfo> {
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

    private bool _inputFocused;
    private int _graceFrames;


    private void CheckFocus() {
        void Decrement() {
            if (_graceFrames > 0) {
                _graceFrames -= 1;
            } else {
                _inputFocused = false;
            }
        }

        // 6.08: CB8F27
        var isTextInputActivePtr = *(bool**) ((IntPtr) AtkStage.GetSingleton() + 0x28) + 0x188E;
        if (isTextInputActivePtr == null) {
            Decrement();
            return;
        }

        if (*isTextInputActivePtr) {
            _inputFocused = true;
            _graceFrames = 60;
        } else {
            Decrement();
        }
    }

    private void UpdateKeybinds() {
        foreach (var name in KeybindsToIntercept.Keys) {
            var keybind = GetKeybind(name);
            if (keybind is null) {
                continue;
            }

            _keybinds[name] = keybind;
        }
    }

    private void InterceptKeybinds(IFramework framework1) {
        CheckFocus();
        UpdateKeybinds();

        if (_inputFocused) {
            return;
        }

        var modifierState = (ModifierFlag) 0;
        foreach (var modifier in Enum.GetValues<ModifierFlag>()) {
            var modifierKey = GetKeyForModifier(modifier);
            if (modifierKey != VirtualKey.NO_KEY && Plugin.KeyState[modifierKey]) {
                modifierState |= modifier;
            }
        }

        var turnedOff = new Dictionary<VirtualKey, (uint, string)>();
        foreach (var toIntercept in KeybindsToIntercept.Keys) {
            if (!Keybinds.TryGetValue(toIntercept, out var keybind)) {
                continue;
            }

            void Intercept(VirtualKey key, ModifierFlag modifier) {
                if (!Plugin.KeyState.IsVirtualKeyValid(key)) {
                    return;
                }

                var modifierPressed = Plugin.Config.KeybindMode switch {
                    KeybindMode.Strict => modifier == modifierState,
                    KeybindMode.Flexible => modifierState.HasFlag(modifier),
                    _ => false,
                };
                if (!modifierPressed) {
                    return;
                }

                if (!Plugin.KeyState[key]) {
                    return;
                }

                var bits = BitOperations.PopCount((uint) modifier);
                if (!turnedOff.TryGetValue(key, out var previousBits) || previousBits.Item1 < bits) {
                    turnedOff[key] = ((uint) bits, toIntercept);
                }
            }

            Intercept(keybind.Key1, keybind.Modifier1);
            Intercept(keybind.Key2, keybind.Modifier2);
        }

        foreach (var (key, (_, keybind)) in turnedOff) {
            Plugin.KeyState[key] = false;

            if (!KeybindsToIntercept.TryGetValue(keybind, out var info)) {
                continue;
            }

            try {
                Activated?.Invoke(new ChatActivatedArgs(info) {
                    TellReason = TellReason.Reply,
                });
            } catch (Exception ex) {
                Plugin.Log.Error(ex, "Error in chat Activated event");
            }
        }
    }

    private void Login() {
        if (ChangeChannelNameHook == null) {
            return;
        }

        var agent = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.ChatLog);
        if (agent == null) {
            return;
        }

        ChangeChannelNameDetour((IntPtr) agent);
    }

    private byte ChatLogRefreshDetour(IntPtr log, ushort eventId, AtkValue* value) {
        if (eventId != 0x31 || value == null || value->UInt is not (0x05 or 0x0C)) {
            return ChatLogRefreshHook!.Original(log, eventId, value);
        }

        string? input = null;
        if (Plugin.GameConfig.TryGet(UiControlOption.DirectChat, out bool option) && option) {
            if (CurrentCharacter != null) {
                // FIXME: this whole system sucks
                var c = *CurrentCharacter;
                if (c != '\0' && !char.IsControl(c)) {
                    input = c.ToString();
                }
            }
        }

        string? addIfNotPresent = null;

        var str = value + 2;
        if (str != null && ((int) str->Type & 0xF) == (int) ValueType.String && str->String != null) {
            var add = MemoryHelper.ReadStringNullTerminated((IntPtr) str->String);
            if (add.Length > 0) {
                addIfNotPresent = add;
            }
        }

        try {
            var args = new ChatActivatedArgs(new ChannelSwitchInfo(null)) {
                AddIfNotPresent = addIfNotPresent,
                Input = input,
            };
            Activated?.Invoke(args);
        } catch (Exception ex) {
            Plugin.Log.Error(ex, "Error in chat Activated event");
        }

        // prevent the game from focusing the chat log
        return 1;
    }

    private IntPtr ChangeChannelNameDetour(IntPtr agent) {
        // Last ShB patch
        // +0x40 = chat channel (byte or uint?)
        //         channel is 17 (maybe 18?) for tells
        // +0x48 = pointer to channel name string
        var ret = ChangeChannelNameHook!.Original(agent);
        if (agent == IntPtr.Zero) {
            return ret;
        }

        // E8 ?? ?? ?? ?? 8D 48 F7
        // RaptureShellModule + 0xFD0
        var shellModule = (IntPtr) Framework.Instance()->GetUiModule()->GetRaptureShellModule();
        if (shellModule == IntPtr.Zero) {
            return ret;
        }

        var channel = 0u;
        if (ShellChannelOffset != null) {
            channel = *(uint*) (shellModule + ShellChannelOffset.Value);
        }

        // var channel = *(uint*) (agent + 0x40);
        if (channel is 17 or 18) {
            channel = 0;
        }

        SeString? name = null;
        var namePtrPtr = (byte**) (agent + 0x48);
        if (namePtrPtr != null) {
            var namePtr = *namePtrPtr;
            name = MemoryHelper.ReadSeStringNullTerminated((IntPtr) namePtr);
            if (name.Payloads.Count == 0) {
                name = null;
            }
        }

        if (name == null) {
            return ret;
        }

        var nameChunks = ChunkUtil.ToChunks(name, ChunkSource.None, null).ToList();
        if (nameChunks.Count > 0 && nameChunks[0] is TextChunk text) {
            text.Content = text.Content.TrimStart('\uE01E').TrimStart();
        }

        Channel = ((InputChannel) channel, nameChunks);

        return ret;
    }

    private void ReplyInSelectedChatModeDetour(AgentInterface* agent) {
        if (ReplyChannelOffset == null) {
            goto Original;
        }

        var replyMode = *(int*) ((IntPtr) agent + ReplyChannelOffset.Value);
        if (replyMode == -2) {
            goto Original;
        }

        SetChannel((InputChannel) replyMode);

        Original:
        ReplyInSelectedChatModeHook!.Original(agent);
    }

    private byte SetChatLogTellTargetDetour(IntPtr a1, Utf8String* name, Utf8String* a3, ushort world, ulong contentId, ushort reason, byte a7) {
        if (name != null) {
            try {
                var target = new TellTarget(name->ToString(), world, contentId, (TellReason) reason);
                Activated?.Invoke(new ChatActivatedArgs(new ChannelSwitchInfo(InputChannel.Tell)) {
                    TellReason = (TellReason) reason,
                    TellTarget = target,
                });
            } catch (Exception ex) {
                Plugin.Log.Error(ex, "Error in chat Activated event");
            }
        }

        return SetChatLogTellTargetHook!.Original(a1, name, a3, world, contentId, reason, a7);
    }

    private void EurekaContextMenuTell(RaptureShellModule* param1, Utf8String* playerName, Utf8String* worldName, ushort world, ulong id, ushort param6)
    {
        if (!UsesTellTempChannel)
        {
            UsesTellTempChannel = true;
            PreviousChannel = Channel.channel;
        }

        if (SetChannelTargetTell != null)
            SetChannelTargetTell(param1, playerName, worldName, world, id, param6, 0);

        EurekaContextMenuTellHook!.Original(param1, playerName, worldName, world, id, param6);
    }

    internal ulong? GetContentIdForEntry(uint index) {
        if (GetContentIdForChatEntry == null) {
            return null;
        }

        return GetContentIdForChatEntry(Framework.Instance()->GetUiModule()->GetRaptureLogModule(), index);
    }

    internal void SetChannel(InputChannel channel, string? tellTarget = null) {
        // ExtraChat linkshells aren't supported in game so we never want to
        // call the ChangeChatChannel function with them.
        //
        // Callers should call ChatLogWindow.SetChannel() which handles
        // ExtraChat channels
        if (ChangeChatChannel == null || channel.IsExtraChatLinkshell())
            return;

        var target = Utf8String.FromString(tellTarget ?? "");
        var idx = channel.LinkshellIndex();
        if (idx == uint.MaxValue)
            idx = 0;

        ChangeChatChannel(RaptureShellModule.Instance(), (int) channel, idx, target, 1);
        target->Dtor(true);
    }

    internal void SetEurekaTellChannel(string name, string worldName, ushort worldId, ulong objectId, ushort param6, byte param7)
    {
        // param6 is 0 for contentId and 1 for objectId
        // param7 is always 0 ?

        if (SetChannelTargetTell == null)
            return;

        if (!UsesTellTempChannel)
        {
            UsesTellTempChannel = true;
            PreviousChannel = Channel.channel;
        }

        var utfName = Utf8String.FromString(name);
        var utfWorld = Utf8String.FromString(worldName);

        SetChannelTargetTell(RaptureShellModule.Instance(), utfName, utfWorld, worldId, objectId, param6, param7);

        utfName->Dtor(true);
        utfWorld->Dtor(true);
    }

    private static VirtualKey GetKeyForModifier(ModifierFlag modifierFlag) => modifierFlag switch {
        ModifierFlag.Shift => VirtualKey.SHIFT,
        ModifierFlag.Ctrl => VirtualKey.CONTROL,
        ModifierFlag.Alt => VirtualKey.MENU,
        _ => VirtualKey.NO_KEY,
    };

    private Keybind? GetKeybind(string id) {
        var agent = (IntPtr) Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.Configkey);
        if (agent == IntPtr.Zero) {
            return null;
        }

        var a1 = *(void**) (agent + 0x78);
        if (a1 == null) {
            return null;
        }

        var outData = stackalloc byte[32];
        var idString = Utf8String.FromString(id);
        GetKeybindNative((IntPtr) a1, idString, (IntPtr) outData);
        idString->Dtor(true);

        var key1 = (VirtualKey) outData[0];
        if (key1 is VirtualKey.F23) {
            key1 = VirtualKey.OEM_2;
        }

        var key2 = (VirtualKey) outData[2];
        if (key2 is VirtualKey.F23) {
            key2 = VirtualKey.OEM_2;
        }

        return new Keybind {
            Key1 = key1,
            Modifier1 = (ModifierFlag) outData[1],
            Key2 = key2,
            Modifier2 = (ModifierFlag) outData[3],
        };
    }

    internal TellHistoryInfo? GetTellHistoryInfo(int index) {
        var acquaintanceModule = Framework.Instance()->GetUiModule()->GetAcquaintanceModule();
        if (acquaintanceModule == null) {
            return null;
        }

        var ptr = GetTellHistory(acquaintanceModule, index);
        if (ptr == IntPtr.Zero) {
            return null;
        }

        var name = MemoryHelper.ReadStringNullTerminated(*(IntPtr*) ptr);
        var world = *(ushort*) (ptr + 0xD0);
        var contentId = *(ulong*) (ptr + 0xD8);

        return new TellHistoryInfo(name, world, contentId);
    }

    internal void SendTell(TellReason reason, ulong contentId, string name, ushort homeWorld, string message) {
        var uName = Utf8String.FromString(name);
        var uMessage = Utf8String.FromString(message);

        var networkModule = GetNetworkModule(Framework.Instance());
        var a1 = *(IntPtr*) (networkModule + 8);
        var logModule = Framework.Instance()->GetUiModule()->GetRaptureLogModule();

        PrintTell(logModule, 33, uName, uMessage, contentId, homeWorld, 255, 0, 0);
        SendTellNative(a1, contentId, homeWorld, uName, uMessage, (byte) reason, homeWorld);

        uName->Dtor(true);
        uMessage->Dtor(true);
    }

    internal bool IsCharValid(char c) {
        var uC = Utf8String.FromString(c.ToString());

        SanitiseString(uC, 0x27F, IntPtr.Zero);
        var wasValid = uC->ToString().Length > 0;

        uC->Dtor(true);

        return wasValid;
    }
}
