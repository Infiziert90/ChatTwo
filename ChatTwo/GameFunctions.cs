using System.Runtime.InteropServices;
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
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace ChatTwo;

internal unsafe class GameFunctions : IDisposable {
    private static class Signatures {
        internal const string ChatLogRefresh = "40 53 56 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 49 8B F0 8B FA";
        internal const string ChangeChannelName = "E8 ?? ?? ?? ?? BA ?? ?? ?? ?? 48 8D 4D B0 48 8B F8 E8 ?? ?? ?? ?? 41 8B D6";
        internal const string ChangeChatChannel = "E8 ?? ?? ?? ?? 0F B7 44 37 ??";
    }

    private delegate byte ChatLogRefreshDelegate(IntPtr log, ushort eventId, AtkValue* value);

    private delegate IntPtr ChangeChannelNameDelegate(IntPtr agent);

    private delegate IntPtr ChangeChatChannelDelegate(RaptureShellModule* shell, int channel, uint linkshellIdx, Utf8String* tellTarget, byte one);

    internal delegate void ChatActivatedEventDelegate(string? input);

    private Plugin Plugin { get; }
    private Hook<ChatLogRefreshDelegate>? ChatLogRefreshHook { get; }
    private Hook<ChangeChannelNameDelegate>? ChangeChannelNameHook { get; }
    private readonly ChangeChatChannelDelegate? _changeChatChannel;

    internal event ChatActivatedEventDelegate? ChatActivated;

    internal (InputChannel channel, List<Chunk> name) ChatChannel { get; private set; }

    internal GameFunctions(Plugin plugin) {
        this.Plugin = plugin;

        if (this.Plugin.SigScanner.TryScanText(Signatures.ChatLogRefresh, out var chatLogPtr)) {
            this.ChatLogRefreshHook = new Hook<ChatLogRefreshDelegate>(chatLogPtr, this.ChatLogRefreshDetour);
            this.ChatLogRefreshHook.Enable();
        }

        if (this.Plugin.SigScanner.TryScanText(Signatures.ChangeChannelName, out var channelNamePtr)) {
            this.ChangeChannelNameHook = new Hook<ChangeChannelNameDelegate>(channelNamePtr, this.ChangeChannelNameDetour);
            this.ChangeChannelNameHook.Enable();
        }

        if (this.Plugin.SigScanner.TryScanText(Signatures.ChangeChatChannel, out var changeChannelPtr)) {
            this._changeChatChannel = Marshal.GetDelegateForFunctionPointer<ChangeChatChannelDelegate>(changeChannelPtr);
        }

        this.Plugin.ClientState.Login += this.Login;
        this.Login(null, null);
    }

    public void Dispose() {
        this.Plugin.ClientState.Login -= this.Login;
        this.ChangeChannelNameHook?.Dispose();
        this.ChatLogRefreshHook?.Dispose();
        this.ChatActivated = null;
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

    internal void SetChatChannel(InputChannel channel, string? tellTarget = null) {
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
            this._changeChatChannel(RaptureShellModule.Instance, (int) (channel + 1), channel.LinkshellIndex(), &target, 1);
        }
    }

    internal static void SetAddonInteractable(string name, bool interactable) {
        var unitManager = AtkStage.GetSingleton()->RaptureAtkUnitManager;

        var addon = (IntPtr) unitManager->GetAddonByName(name);
        if (addon == IntPtr.Zero) {
            return;
        }

        var flags = (uint*) (addon + 0x180);
        if (interactable) {
            *flags &= ~(1u << 22);
        } else {
            *flags |= 1 << 22;
        }
    }

    internal void SetChatInteractable(bool interactable) {
        for (var i = 0; i < 4; i++) {
            SetAddonInteractable($"ChatLogPanel_{i}", interactable);
        }

        SetAddonInteractable("ChatLog", interactable);
    }

    internal static bool IsAddonInteractable(string name) {
        var unitManager = AtkStage.GetSingleton()->RaptureAtkUnitManager;

        var addon = (IntPtr) unitManager->GetAddonByName(name);
        if (addon == IntPtr.Zero) {
            return false;
        }

        var flags = (uint*) (addon + 0x180);
        return (*flags & (1 << 22)) == 0;
    }

    internal void OpenItemTooltip(uint id) {
        var atkStage = AtkStage.GetSingleton();
        var agent = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.ItemDetail);
        var addon = atkStage->RaptureAtkUnitManager->GetAddonByName("ItemDetail");
        if (agent == null || addon == null) {
            // atkStage ain't gonna be null or we have bigger problems
            return;
        }

        var agentPtr = (IntPtr) agent;

        // addresses mentioned here are 6.01
        // see the call near the end of AgentItemDetail.Update
        // offsets valid as of 6.01

        // 8BFC49: sets some shit
        *(uint*) (agentPtr + 0x20) = 22;
        // 8C04C8: switch goes down to default, which is what we want
        *(byte*) (agentPtr + 0x118) = 1;
        // 8BFCF6: item id when hovering over item in chat
        *(uint*) (agentPtr + 0x11C) = id;
        // 8BFCE4: always 0 when hovering over item in chat
        *(uint*) (agentPtr + 0x120) = 0;
        // 8C0B55: skips a check to do with inventory
        *(byte*) (agentPtr + 0x128) &= 0xEF;
        // 8BFC7C: when set to 1, lets everything continue (one frame)
        *(byte*) (agentPtr + 0x146) = 1;
        // 8BFC89: skips early return
        *(byte*) (agentPtr + 0x14A) = 0;

        // this just probably needs to be set
        agent->AddonId = (uint) addon->ID;

        // vcall from E8 ?? ?? ?? ?? 0F B7 C0 48 83 C4 60
        var vf5 = (delegate*<AtkUnitBase*, byte, uint, void>*) ((IntPtr) addon->VTable + 40);
        // E8872D: lets vf5 actually run
        *(byte*) ((IntPtr) atkStage + 0x2B4) |= 2;
        (*vf5)(addon, 0, 15);
    }

    internal void CloseItemTooltip() {
        // hide addon first to prevent the "addon close" sound
        var addon = AtkStage.GetSingleton()->RaptureAtkUnitManager->GetAddonByName("ItemDetail");
        if (addon != null) {
            addon->Hide(true);
        }

        var agent = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.ItemDetail);
        if (agent != null) {
            agent->Hide();
        }
    }

    private byte ChatLogRefreshDetour(IntPtr log, ushort eventId, AtkValue* value) {
        if (eventId == 0x31 && value != null && value->UInt is 0x05 or 0x0C) {
            string? eventInput = null;

            var str = value + 2;
            if (str != null && str->String != null) {
                var input = MemoryHelper.ReadStringNullTerminated((IntPtr) str->String);
                if (input.Length > 0) {
                    eventInput = input;
                }
            }

            try {
                this.ChatActivated?.Invoke(eventInput);
            } catch (Exception ex) {
                PluginLog.LogError(ex, "Error in ChatActivated event");
            }

            return 0;
        }

        return this.ChatLogRefreshHook!.Original(log, eventId, value);
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

        var channel = *(uint*) (shellModule + 0xFD0);

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

        this.ChatChannel = ((InputChannel) channel, nameChunks);

        return ret;
    }
}
