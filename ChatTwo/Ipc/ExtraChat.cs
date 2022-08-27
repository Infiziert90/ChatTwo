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
    private ICallGateSubscriber<Dictionary<Guid, string>, Dictionary<Guid, string>> ChannelNamesGate { get; }

    internal (string, uint)? ChannelOverride { get; set; }

    private Dictionary<string, uint> ChannelCommandColoursInternal { get; set; } = new();
    internal IReadOnlyDictionary<string, uint> ChannelCommandColours => this.ChannelCommandColoursInternal;

    private Dictionary<Guid, string> ChannelNamesInternal { get; set; } = new();
    internal IReadOnlyDictionary<Guid, string> ChannelNames => this.ChannelNamesInternal;

    internal ExtraChat(Plugin plugin) {
        this.Plugin = plugin;

        this.OverrideChannelGate = this.Plugin.Interface.GetIpcSubscriber<OverrideInfo, object>("ExtraChat.OverrideChannelColour");
        this.ChannelCommandColoursGate = this.Plugin.Interface.GetIpcSubscriber<Dictionary<string, uint>, Dictionary<string, uint>>("ExtraChat.ChannelCommandColours");
        this.ChannelNamesGate = this.Plugin.Interface.GetIpcSubscriber<Dictionary<Guid, string>, Dictionary<Guid, string>>("ExtraChat.ChannelNames");

        this.OverrideChannelGate.Subscribe(this.OnOverrideChannel);
        this.ChannelCommandColoursGate.Subscribe(this.OnChannelCommandColours);
        this.ChannelNamesGate.Subscribe(this.OnChannelNames);
        try {
            this.ChannelCommandColoursInternal = this.ChannelCommandColoursGate.InvokeFunc(null!);
            this.ChannelNamesInternal = this.ChannelNamesGate.InvokeFunc(null!);
        } catch (Exception) {
            // no-op
        }
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

    private void OnChannelCommandColours(Dictionary<string, uint> obj) {
        this.ChannelCommandColoursInternal = obj;
    }

    private void OnChannelNames(Dictionary<Guid, string> obj) {
        this.ChannelNamesInternal = obj;
    }
}
