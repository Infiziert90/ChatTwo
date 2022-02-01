using Dalamud.Data;
using ImGuiScene;
using Lumina.Excel.GeneratedSheets;

namespace ChatTwo;

internal class TextureCache : IDisposable {
    private DataManager Data { get; }

    private readonly Dictionary<(uint, bool), TextureWrap> _itemIcons = new();
    private readonly Dictionary<(uint, bool), TextureWrap> _statusIcons = new();
    private readonly Dictionary<(uint, bool), TextureWrap> _eventItemIcons = new();

    internal IReadOnlyDictionary<(uint, bool), TextureWrap> ItemIcons => this._itemIcons;
    internal IReadOnlyDictionary<(uint, bool), TextureWrap> StatusIcons => this._statusIcons;
    internal IReadOnlyDictionary<(uint, bool), TextureWrap> EventItemIcons => this._eventItemIcons;

    internal TextureCache(DataManager data) {
        this.Data = data;
    }

    public void Dispose() {
        var allIcons = this.ItemIcons.Values
            .Concat(this.StatusIcons.Values);

        foreach (var tex in allIcons) {
            tex.Dispose();
        }
    }

    private void AddIcon(IDictionary<(uint, bool), TextureWrap> dict, uint icon, bool hq = false) {
        if (dict.ContainsKey((icon, hq))) {
            return;
        }

        var tex = hq
            ? this.Data.GetImGuiTextureHqIcon(icon)
            : this.Data.GetImGuiTextureIcon(icon);
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

    internal TextureWrap? GetItem(Item item, bool hq = false) {
        this.AddItem(item, hq);
        this.ItemIcons.TryGetValue((item.Icon, hq), out var icon);
        return icon;
    }

    internal TextureWrap? GetStatus(Status status) {
        this.AddStatus(status);
        this.StatusIcons.TryGetValue((status.Icon, false), out var icon);
        return icon;
    }

    internal TextureWrap? GetEventItem(EventItem item) {
        this.AddEventItem(item);
        this.EventItemIcons.TryGetValue((item.Icon, false), out var icon);
        return icon;
    }
}
