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
    internal IReadOnlyDictionary<string, uint> ChannelCommandColours => ChannelCommandColoursInternal;

    private Dictionary<Guid, string> ChannelNamesInternal { get; set; } = new();
    internal IReadOnlyDictionary<Guid, string> ChannelNames => ChannelNamesInternal;

    internal ExtraChat(Plugin plugin) {
        Plugin = plugin;

        OverrideChannelGate = Plugin.Interface.GetIpcSubscriber<OverrideInfo, object>("ExtraChat.OverrideChannelColour");
        ChannelCommandColoursGate = Plugin.Interface.GetIpcSubscriber<Dictionary<string, uint>, Dictionary<string, uint>>("ExtraChat.ChannelCommandColours");
        ChannelNamesGate = Plugin.Interface.GetIpcSubscriber<Dictionary<Guid, string>, Dictionary<Guid, string>>("ExtraChat.ChannelNames");

        OverrideChannelGate.Subscribe(OnOverrideChannel);
        ChannelCommandColoursGate.Subscribe(OnChannelCommandColours);
        ChannelNamesGate.Subscribe(OnChannelNames);
        try {
            ChannelCommandColoursInternal = ChannelCommandColoursGate.InvokeFunc(null!);
            ChannelNamesInternal = ChannelNamesGate.InvokeFunc(null!);
        } catch (Exception) {
            // no-op
        }
    }

    public void Dispose() {
        OverrideChannelGate.Unsubscribe(OnOverrideChannel);
    }

    private void OnOverrideChannel(OverrideInfo info) {
        if (info.Channel == null) {
            ChannelOverride = null;
            return;
        }

        ChannelOverride = (info.Channel, info.Rgba);
    }

    private void OnChannelCommandColours(Dictionary<string, uint> obj) {
        ChannelCommandColoursInternal = obj;
    }

    private void OnChannelNames(Dictionary<Guid, string> obj) {
        ChannelNamesInternal = obj;
    }
}
