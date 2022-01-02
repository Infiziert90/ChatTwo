using Dalamud.Data;
using ImGuiScene;
using Lumina.Excel.GeneratedSheets;

namespace ChatTwo; 

internal class TextureCache : IDisposable {
    private DataManager Data { get; }

    private readonly Dictionary<uint, TextureWrap> _itemIcons = new();
    private readonly Dictionary<uint, TextureWrap> _statusIcons = new();

    internal IReadOnlyDictionary<uint, TextureWrap> ItemIcons => this._itemIcons;
    internal IReadOnlyDictionary<uint, TextureWrap> StatusIcons => this._statusIcons;

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

    private void AddIcon(IDictionary<uint, TextureWrap> dict, uint icon) {
        if (dict.ContainsKey(icon)) {
            return;
        }
        
        var tex = this.Data.GetImGuiTextureIcon(icon);
        if (tex != null) {
            dict[icon] = tex;
        }
    }

    internal void AddItem(Item item) {
        this.AddIcon(this._itemIcons, item.Icon);
    }

    internal void AddStatus(Status status) {
        this.AddIcon(this._statusIcons, status.Icon);
    }

    internal TextureWrap? GetItem(Item item) {
        this.AddItem(item);
        this.ItemIcons.TryGetValue(item.Icon, out var icon);
        return icon;
    }

    internal TextureWrap? GetStatus(Status status) {
        this.AddStatus(status);
        this.StatusIcons.TryGetValue(status.Icon, out var icon);
        return icon;
    }
}
