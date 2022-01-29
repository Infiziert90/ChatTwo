using System.Text;
using ChatTwo.Code;
using ChatTwo.GameFunctions.Types;
using ChatTwo.Util;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Siggingway;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace ChatTwo.GameFunctions;

internal sealed unsafe class Chat : IDisposable {
    // Functions

    [Signature("E8 ?? ?? ?? ?? 0F B7 44 37 ??", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<RaptureShellModule*, int, uint, Utf8String*, byte, void> _changeChatChannel = null!;

    [Signature("4C 8B 81 ?? ?? ?? ?? 4D 85 C0 74 17", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<RaptureLogModule*, uint, ulong> _getContentIdForChatEntry = null!;

    [Signature("E8 ?? ?? ?? ?? 48 8D 4D A0 8B F8")]
    private readonly delegate* unmanaged<IntPtr, Utf8String*, IntPtr, uint> _getKeybind = null!;

    [Signature("E8 ?? ?? ?? ?? 48 3B F0 74 35")]
    private readonly delegate* unmanaged<AtkStage*, IntPtr> _getFocus = null!;

    [Signature("44 8B 89 ?? ?? ?? ?? 4C 8B C1")]
    private readonly delegate* unmanaged<void*, int, IntPtr> _getTellHistory = null!;

    // Hooks

    private delegate byte ChatLogRefreshDelegate(IntPtr log, ushort eventId, AtkValue* value);

    private delegate IntPtr ChangeChannelNameDelegate(IntPtr agent);

    private delegate void ReplyInSelectedChatModeDelegate(AgentInterface* agent);

    private delegate byte SetChatLogTellTarget(IntPtr a1, Utf8String* name, Utf8String* a3, ushort world, ulong contentId, ushort a6, byte a7);

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

    // Offsets

    #pragma warning disable 0649

    [Signature("8B B9 ?? ?? ?? ?? 48 8B D9 83 FF FE 0F 84", Offset = 2)]
    private readonly int? _replyChannelOffset;

    [Signature("89 83 ?? ?? ?? ?? 48 8B 01 83 FE 13 7C 05 41 8B D4 EB 03 83 CA FF FF 90", Offset = 2)]
    private readonly int? _shellChannelOffset;

    #pragma warning restore 0649

    // Events

    internal delegate void ChatActivatedEventDelegate(string? input, ChannelSwitchInfo info);

    internal event ChatActivatedEventDelegate? Activated;

    private Plugin Plugin { get; }
    internal (InputChannel channel, List<Chunk> name) Channel { get; private set; }

    internal Chat(Plugin plugin) {
        this.Plugin = plugin;
        Siggingway.Siggingway.Initialise(this.Plugin.SigScanner, this);

        this.ChatLogRefreshHook?.Enable();
        this.ChangeChannelNameHook?.Enable();
        this.ReplyInSelectedChatModeHook?.Enable();
        this.SetChatLogTellTargetHook?.Enable();

        this.Plugin.Framework.Update += this.InterceptKeybinds;
        this.Plugin.ClientState.Login += this.Login;
        this.Login(null, null);
    }

    public void Dispose() {
        this.Plugin.ClientState.Login -= this.Login;
        this.Plugin.Framework.Update -= this.InterceptKeybinds;

        this.SetChatLogTellTargetHook?.Dispose();
        this.ReplyInSelectedChatModeHook?.Dispose();
        this.ChangeChannelNameHook?.Dispose();
        this.ChatLogRefreshHook?.Dispose();

        this.Activated = null;
    }

    private readonly Dictionary<string, Keybind> _keybinds = new();
    internal IReadOnlyDictionary<string, Keybind> Keybinds => this._keybinds;

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
            if (this._graceFrames > 0) {
                this._graceFrames -= 1;
            } else {
                this._inputFocused = false;
            }
        }

        var focus = this._getFocus(AtkStage.GetSingleton());
        if (focus == IntPtr.Zero) {
            Decrement();
            return;
        }

        var node = (AtkResNode*) focus;
        var parent = node->ParentNode;
        if (parent == null || (uint) parent->Type is not (1007 or 1011)) {
            Decrement();
            return;
        }

        this._inputFocused = true;
        this._graceFrames = 60;
    }

    private void UpdateKeybinds() {
        foreach (var name in KeybindsToIntercept.Keys) {
            var keybind = this.GetKeybind(name);
            if (keybind is null) {
                continue;
            }

            this._keybinds[name] = keybind;
        }
    }

    private void InterceptKeybinds(Dalamud.Game.Framework framework) {
        this.CheckFocus();
        this.UpdateKeybinds();

        if (this._inputFocused) {
            return;
        }

        var modifierState = (ModifierFlag) 0;
        foreach (var modifier in Enum.GetValues<ModifierFlag>()) {
            var modifierKey = GetKeyForModifier(modifier);
            if (modifierKey != VirtualKey.NO_KEY && this.Plugin.KeyState[modifierKey]) {
                modifierState |= modifier;
            }
        }

        var turnedOff = new Dictionary<VirtualKey, (uint, string)>();
        foreach (var toIntercept in KeybindsToIntercept.Keys) {
            if (!this.Keybinds.TryGetValue(toIntercept, out var keybind)) {
                continue;
            }

            void Intercept(VirtualKey key, ModifierFlag modifier) {
                if (!this.Plugin.KeyState.IsVirtualKeyValid(key)) {
                    return;
                }

                if (!modifierState.HasFlag(modifier)) {
                    return;
                }

                if (!this.Plugin.KeyState[key]) {
                    return;
                }

                var bits = NumUtil.NumberOfSetBits((uint) modifier);
                if (!turnedOff.TryGetValue(key, out var previousBits) || previousBits.Item1 < bits) {
                    turnedOff[key] = (bits, toIntercept);
                }
            }

            Intercept(keybind.Key1, keybind.Modifier1);
            Intercept(keybind.Key2, keybind.Modifier2);
        }

        foreach (var (key, (_, keybind)) in turnedOff) {
            PluginLog.Log($"intercepting {keybind}");
            this.Plugin.KeyState[key] = false;

            if (!KeybindsToIntercept.TryGetValue(keybind, out var info)) {
                continue;
            }

            try {
                this.Activated?.Invoke(null, info);
            } catch (Exception ex) {
                PluginLog.LogError(ex, "Error in chat Activated event");
            }
        }
    }

    private void Login(object? sender, EventArgs? e) {
        if (this.ChangeChannelNameHook == null) {
            return;
        }

        var agent = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.ChatLog);
        if (agent == null) {
            return;
        }

        this.ChangeChannelNameDetour((IntPtr) agent);
    }

    private byte ChatLogRefreshDetour(IntPtr log, ushort eventId, AtkValue* value) {
        if (eventId != 0x31 || value == null || value->UInt is not (0x05 or 0x0C)) {
            return this.ChatLogRefreshHook!.Original(log, eventId, value);
        }

        string? eventInput = null;

        var str = value + 2;
        if (str != null && ((int) str->Type & 0xF) == (int) ValueType.String && str->String != null) {
            var input = MemoryHelper.ReadStringNullTerminated((IntPtr) str->String);
            if (input.Length > 0) {
                eventInput = input;
            }
        }

        try {
            this.Activated?.Invoke(eventInput, new ChannelSwitchInfo(null));
        } catch (Exception ex) {
            PluginLog.LogError(ex, "Error in chat Activated event");
        }

        // prevent the game from focusing the chat log
        return 1;
    }

    private IntPtr ChangeChannelNameDetour(IntPtr agent) {
        // Last ShB patch
        // +0x40 = chat channel (byte or uint?)
        //         channel is 17 (maybe 18?) for tells
        // +0x48 = pointer to channel name string
        var ret = this.ChangeChannelNameHook!.Original(agent);
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
        if (this._shellChannelOffset != null) {
            channel = *(uint*) (shellModule + this._shellChannelOffset.Value);
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

        var nameChunks = ChunkUtil.ToChunks(name, null).ToList();
        if (nameChunks.Count > 0 && nameChunks[0] is TextChunk text) {
            text.Content = text.Content.TrimStart('\uE01E').TrimStart();
        }

        this.Channel = ((InputChannel) channel, nameChunks);

        return ret;
    }

    private void ReplyInSelectedChatModeDetour(AgentInterface* agent) {
        if (this._replyChannelOffset == null) {
            goto Original;
        }

        var replyMode = *(int*) ((IntPtr) agent + this._replyChannelOffset.Value);
        if (replyMode == -2) {
            goto Original;
        }

        this.SetChannel((InputChannel) replyMode);

        Original:
        this.ReplyInSelectedChatModeHook!.Original(agent);
    }

    private byte SetChatLogTellTargetDetour(IntPtr a1, Utf8String* name, Utf8String* a3, ushort world, ulong contentId, ushort reason, byte a7) {
        if (name != null) {
            PluginLog.Log($"{name->ToString()}@{world} ({contentId}) {(TellReason) reason}");
        }

        return this.SetChatLogTellTargetHook!.Original(a1, name, a3, world, contentId, reason, a7);
    }

    internal ulong? GetContentIdForEntry(uint index) {
        if (this._getContentIdForChatEntry == null) {
            return null;
        }

        return this._getContentIdForChatEntry(Framework.Instance()->GetUiModule()->GetRaptureLogModule(), index);
    }

    internal void SetChannel(InputChannel channel, string? tellTarget = null) {
        if (this._changeChatChannel == null) {
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(tellTarget ?? "");
        var target = new Utf8String();
        fixed (byte* tellTargetPtr = bytes) {
            var zero = stackalloc byte[1];
            zero[0] = 0;

            target.StringPtr = tellTargetPtr == null ? zero : tellTargetPtr;
            target.StringLength = bytes.Length;

            var idx = channel.LinkshellIndex();
            if (idx == uint.MaxValue) {
                idx = 0;
            }

            this._changeChatChannel(RaptureShellModule.Instance, (int) channel, idx, &target, 1);
        }
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
        this._getKeybind((IntPtr) a1, idString, (IntPtr) outData);

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

        var ptr = this._getTellHistory(acquaintanceModule, index);
        if (ptr == IntPtr.Zero) {
            return null;
        }

        var name = MemoryHelper.ReadStringNullTerminated(*(IntPtr*) ptr);
        var world = *(ushort*) (ptr + 0xD0);
        var contentId = *(ulong*) (ptr + 0xD8);

        return new TellHistoryInfo(name, world, contentId);
    }
}
