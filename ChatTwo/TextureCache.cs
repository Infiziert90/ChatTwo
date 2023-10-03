using Dalamud.Interface.Internal;
using Dalamud.Plugin.Services;
using Lumina.Excel.GeneratedSheets;

namespace ChatTwo;

internal class TextureCache : IDisposable {
    private ITextureProvider TextureProvider { get; }

    private readonly Dictionary<(uint, bool), IDalamudTextureWrap> _itemIcons = new();
    private readonly Dictionary<(uint, bool), IDalamudTextureWrap> _statusIcons = new();
    private readonly Dictionary<(uint, bool), IDalamudTextureWrap> _eventItemIcons = new();

    internal IReadOnlyDictionary<(uint, bool), IDalamudTextureWrap> ItemIcons => this._itemIcons;
    internal IReadOnlyDictionary<(uint, bool), IDalamudTextureWrap> StatusIcons => this._statusIcons;
    internal IReadOnlyDictionary<(uint, bool), IDalamudTextureWrap> EventItemIcons => this._eventItemIcons;

    internal TextureCache(ITextureProvider textureProvider) {
        this.TextureProvider = textureProvider;
    }

    public void Dispose() {
        var allIcons = this.ItemIcons.Values
            .Concat(this.StatusIcons.Values);

        foreach (var tex in allIcons) {
            tex.Dispose();
        }
    }

    private void AddIcon(IDictionary<(uint, bool), IDalamudTextureWrap> dict, uint icon, bool hq = false) {
        if (dict.ContainsKey((icon, hq))) {
            return;
        }

        var tex = hq
            ? this.TextureProvider.GetIcon(icon, ITextureProvider.IconFlags.ItemHighQuality)
            : this.TextureProvider.GetIcon(icon);
        if (tex != null) {
            dict[(icon, hq)] = tex;
        }
    }

    internal void AddItem(Item item, bool hq) {
        this.AddIcon(this._itemIcons, item.Icon, hq);
    }

    internal void AddStatus(Status status) {
        this.AddIcon(this._statusIcons, status.Icon);
    }

    internal void AddEventItem(EventItem item) {
        this.AddIcon(this._eventItemIcons, item.Icon);
    }

    internal IDalamudTextureWrap? GetItem(Item item, bool hq = false) {
        this.AddItem(item, hq);
        this.ItemIcons.TryGetValue((item.Icon, hq), out var icon);
        return icon;
    }

    internal IDalamudTextureWrap? GetStatus(Status status) {
        this.AddStatus(status);
        this.StatusIcons.TryGetValue((status.Icon, false), out var icon);
        return icon;
    }

    internal IDalamudTextureWrap? GetEventItem(EventItem item) {
        this.AddEventItem(item);
        this.EventItemIcons.TryGetValue((item.Icon, false), out var icon);
        return icon;
    }
}
