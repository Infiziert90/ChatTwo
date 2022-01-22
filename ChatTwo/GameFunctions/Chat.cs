using System.Text;
using ChatTwo.Code;
using ChatTwo.Util;
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

    // Hooks

    private delegate byte ChatLogRefreshDelegate(IntPtr log, ushort eventId, AtkValue* value);

    private delegate IntPtr ChangeChannelNameDelegate(IntPtr agent);

    private delegate void ReplyInSelectedChatModeDelegate(AgentInterface* agent);

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

    // Offsets

    #pragma warning disable 0649

    [Signature("8B B9 ?? ?? ?? ?? 48 8B D9 83 FF FE 0F 84", Offset = 2)]
    private readonly int? _replyChannelOffset;

    [Signature("89 83 ?? ?? ?? ?? 48 8B 01 83 FE 13 7C 05 41 8B D4 EB 03 83 CA FF FF 90", Offset = 2)]
    private readonly int? _shellChannelOffset;

    #pragma warning restore 0649

    // Events

    internal delegate void ChatActivatedEventDelegate(string? input, InputChannel? channel, (string, string)? tellTarget);

    internal event ChatActivatedEventDelegate? Activated;

    private Plugin Plugin { get; }
    internal (InputChannel channel, List<Chunk> name) Channel { get; private set; }

    internal Chat(Plugin plugin) {
        this.Plugin = plugin;
        Siggingway.Siggingway.Initialise(this.Plugin.SigScanner, this);

        this.ChatLogRefreshHook?.Enable();
        this.ChangeChannelNameHook?.Enable();
        this.ReplyInSelectedChatModeHook?.Enable();

        this.Plugin.ClientState.Login += this.Login;
        this.Login(null, null);
    }

    public void Dispose() {
        this.Plugin.ClientState.Login -= this.Login;

        this.ReplyInSelectedChatModeHook?.Dispose();
        this.ChangeChannelNameHook?.Dispose();
        this.ChatLogRefreshHook?.Dispose();

        this.Activated = null;
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

    private byte ChatLogRefreshDetour(IntPtr log, ushort eventId, AtkValue* value) {
        if (eventId != 0x31 || value == null || value->UInt is not (0x05 or 0x0C)) {
            return this.ChatLogRefreshHook!.Original(log, eventId, value);
        }

        InputChannel? channel = null;
        (string, string)? tellTarget = null;
        // if (this._shellChannelOffset != null) {
        //     var shell = (IntPtr) Framework.Instance()->GetUiModule()->GetRaptureShellModule();
        //
        //     channel = (InputChannel) (*(uint*) (shell + this._shellChannelOffset.Value));
        //     if ((int) channel is 17 or 18) {
        //         channel = InputChannel.Tell;
        //
        //         var targetPtr = *(byte**) (shell + 0xFD8);
        //         var targetWorldPtr = *(byte**) (shell + 0x1040);
        //
        //         var target = MemoryHelper.ReadStringNullTerminated((IntPtr) targetPtr);
        //         var targetWorld = MemoryHelper.ReadStringNullTerminated((IntPtr) targetWorldPtr);
        //         tellTarget = (target, targetWorld);
        //     }
        // }

        string? eventInput = null;

        var str = value + 2;
        if (str != null && ((int) str->Type & 0xF) == (int) ValueType.String && str->String != null) {
            var input = MemoryHelper.ReadStringNullTerminated((IntPtr) str->String);
            if (input.Length > 0) {
                eventInput = input;
            }
        }

        try {
            this.Activated?.Invoke(eventInput, channel, tellTarget);
        } catch (Exception ex) {
            PluginLog.LogError(ex, "Error in ChatActivated event");
        }

        // var ret = this.ChatLogRefreshHook!.Original(log, eventId, value);
        // if (this._clearFocus != null) {
        //     this._clearFocus(AtkStage.GetSingleton());
        // }

        // return ret;

        // return this.ChatLogRefreshHook!.Original(log, eventId, value);
        return 1;
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
}
