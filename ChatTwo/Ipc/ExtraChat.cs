using Dalamud.Plugin.Ipc;

namespace ChatTwo.Ipc;

internal sealed class ExtraChat : IDisposable {
    private struct OverrideInfo {
        internal string? Channel;
        internal ushort UiColour;
        internal uint Rgba;
    }

    private Plugin Plugin { get; }

    private ICallGateSubscriber<OverrideInfo, object> OverrideChannelGate { get; }

    internal (string, uint)? ChannelOverride { get; set; }

    internal ExtraChat(Plugin plugin) {
        this.Plugin = plugin;

        this.OverrideChannelGate = this.Plugin.Interface.GetIpcSubscriber<OverrideInfo, object>("ExtraChat.OverrideChannelColour");

        this.OverrideChannelGate.Subscribe(this.OnOverrideChannel);
    }

    public void Dispose() {
        this.OverrideChannelGate.Unsubscribe(this.OnOverrideChannel);
    }

    private void OnOverrideChannel(OverrideInfo info) {
        if (info.Channel == null) {
            this.ChannelOverride = null;
            return;
        }

        this.ChannelOverride = (info.Channel, info.Rgba);
    }
}
