using ChatTwo.Code;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace ChatTwo;

internal unsafe class GameFunctions : IDisposable {
    private static class Signatures {
        internal const string ChatLogRefresh = "40 53 56 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 49 8B F0 8B FA";
        internal const string ChangeChannelName = "E8 ?? ?? ?? ?? BA ?? ?? ?? ?? 48 8D 4D B0 48 8B F8 E8 ?? ?? ?? ?? 41 8B D6";
    }

    private delegate byte ChatLogRefreshDelegate(IntPtr log, ushort eventId, AtkValue* value);

    private delegate IntPtr ChangeChannelNameDelegate(IntPtr agent);

    internal delegate void ChatActivatedEventDelegate(string? input);

    private Plugin Plugin { get; }
    private Hook<ChatLogRefreshDelegate>? ChatLogRefreshHook { get; }
    private Hook<ChangeChannelNameDelegate>? ChangeChannelNameHook { get; }

    internal event ChatActivatedEventDelegate? ChatActivated;

    internal (InputChannel channel, string name) ChatChannel { get; private set; }

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

        this.ChatChannel = ((InputChannel) channel, name.TextValue.TrimStart('\uE01E').Trim());

        return ret;
    }
}
