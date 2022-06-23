using Dalamud.Plugin.Ipc;

namespace ChatTwo.Ipc;

internal sealed class ExtraChat : IDisposable {
    private Plugin Plugin { get; }

    private ICallGateSubscriber<string?, object> OverrideChannelGate { get; }

    internal string? ChannelOverride { get; set; }

    internal ExtraChat(Plugin plugin) {
        this.Plugin = plugin;

        this.OverrideChannelGate = this.Plugin.Interface.GetIpcSubscriber<string?, object>("ExtraChat.OverrideChannel");

        this.OverrideChannelGate.Subscribe(this.OnOverrideChannel);
    }

    public void Dispose() {
        this.OverrideChannelGate.Unsubscribe(this.OnOverrideChannel);
    }

    private void OnOverrideChannel(string? channel) {
        this.ChannelOverride = channel;
    }
}
