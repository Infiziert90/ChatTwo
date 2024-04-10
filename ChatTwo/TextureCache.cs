using Dalamud.Interface.Internal;
using Dalamud.Plugin.Services;
using Lumina.Excel.GeneratedSheets;

namespace ChatTwo;

internal class TextureCache : IDisposable {
    private ITextureProvider TextureProvider { get; }

    private readonly Dictionary<(uint, bool), IDalamudTextureWrap> _itemIcons = new();
    private readonly Dictionary<(uint, bool), IDalamudTextureWrap> _statusIcons = new();
    private readonly Dictionary<(uint, bool), IDalamudTextureWrap> _eventItemIcons = new();

    internal IReadOnlyDictionary<(uint, bool), IDalamudTextureWrap> ItemIcons => _itemIcons;
    internal IReadOnlyDictionary<(uint, bool), IDalamudTextureWrap> StatusIcons => _statusIcons;
    internal IReadOnlyDictionary<(uint, bool), IDalamudTextureWrap> EventItemIcons => _eventItemIcons;

    internal TextureCache(ITextureProvider textureProvider) {
        TextureProvider = textureProvider;
    }

    public void Dispose() {
        var allIcons = ItemIcons.Values
            .Concat(StatusIcons.Values);

        foreach (var tex in allIcons) {
            tex.Dispose();
        }
    }

    private void AddIcon(IDictionary<(uint, bool), IDalamudTextureWrap> dict, uint icon, bool hq = false) {
        if (dict.ContainsKey((icon, hq))) {
            return;
        }

        var tex = hq
            ? TextureProvider.GetIcon(icon, ITextureProvider.IconFlags.ItemHighQuality)
            : TextureProvider.GetIcon(icon);
        if (tex != null) {
            dict[(icon, hq)] = tex;
        }
    }

    internal void AddItem(Item item, bool hq) {
        AddIcon(_itemIcons, item.Icon, hq);
    }

    internal void AddStatus(Status status) {
        AddIcon(_statusIcons, status.Icon);
    }

    internal void AddEventItem(EventItem item) {
        AddIcon(_eventItemIcons, item.Icon);
    }

    internal IDalamudTextureWrap? GetItem(Item item, bool hq = false) {
        AddItem(item, hq);
        ItemIcons.TryGetValue((item.Icon, hq), out var icon);
        return icon;
    }

    internal IDalamudTextureWrap? GetStatus(Status status) {
        AddStatus(status);
        StatusIcons.TryGetValue((status.Icon, false), out var icon);
        return icon;
    }

    internal IDalamudTextureWrap? GetEventItem(EventItem item) {
        AddEventItem(item);
        EventItemIcons.TryGetValue((item.Icon, false), out var icon);
        return icon;
    }
}
