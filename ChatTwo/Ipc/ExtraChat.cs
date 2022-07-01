using Dalamud.Plugin.Ipc;

namespace ChatTwo.Ipc;

internal sealed class ExtraChat : IDisposable {
    [Serializable]
    private struct OverrideInfo {
        public string? Channel;
        public ushort UiColour;
        public uint Rgba;
    }

    private Plugin Plugin { get; }

    private ICallGateSubscriber<OverrideInfo, object> OverrideChannelGate { get; }
    private ICallGateSubscriber<Dictionary<string, uint>, Dictionary<string, uint>> ChannelCommandColoursGate { get; }

    internal (string, uint)? ChannelOverride { get; set; }
    private Dictionary<string, uint> ChannelCommandColoursInternal { get; set; } = new();
    internal IReadOnlyDictionary<string, uint> ChannelCommandColours => this.ChannelCommandColoursInternal;

    internal ExtraChat(Plugin plugin) {
        this.Plugin = plugin;

        this.OverrideChannelGate = this.Plugin.Interface.GetIpcSubscriber<OverrideInfo, object>("ExtraChat.OverrideChannelColour");
        this.ChannelCommandColoursGate = this.Plugin.Interface.GetIpcSubscriber<Dictionary<string, uint>, Dictionary<string, uint>>("ExtraChat.ChannelCommandColours");

        this.OverrideChannelGate.Subscribe(this.OnOverrideChannel);
        this.ChannelCommandColoursGate.Subscribe(this.OnChannelCommandColours);
        try {
            this.ChannelCommandColoursInternal = this.ChannelCommandColoursGate.InvokeFunc(null!);
        } catch (Exception) {
            // no-op
        }
    }

    private void OnChannelCommandColours(Dictionary<string, uint> obj) {
        this.ChannelCommandColoursInternal = obj;
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
