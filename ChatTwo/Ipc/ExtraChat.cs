using Dalamud.Plugin.Ipc;

namespace ChatTwo.Ipc;

internal sealed class ExtraChat : IDisposable {
    private Plugin Plugin { get; }

    private ICallGateSubscriber<(string, ushort, uint)?, object> OverrideChannelGate { get; }

    internal (string, uint)? ChannelOverride { get; set; }

    internal ExtraChat(Plugin plugin) {
        this.Plugin = plugin;

        this.OverrideChannelGate = this.Plugin.Interface.GetIpcSubscriber<(string, ushort, uint)?, object>("ExtraChat.OverrideChannelColour");

        this.OverrideChannelGate.Subscribe(this.OnOverrideChannel);
    }

    public void Dispose() {
        this.OverrideChannelGate.Unsubscribe(this.OnOverrideChannel);
    }

    private void OnOverrideChannel((string, ushort, uint)? info) {
        if (info == null) {
            this.ChannelOverride = null;
            return;
        }

        this.ChannelOverride = (info.Value.Item1, info.Value.Item3);
    }
}
