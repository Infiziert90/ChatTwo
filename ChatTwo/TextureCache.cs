using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Lumina.Excel.GeneratedSheets;

namespace ChatTwo;

internal class TextureCache : IDisposable
{
    private readonly Dictionary<(uint, bool), IDalamudTextureWrap> _itemIcons = new();
    private readonly Dictionary<(uint, bool), IDalamudTextureWrap> _statusIcons = new();
    private readonly Dictionary<(uint, bool), IDalamudTextureWrap> _eventItemIcons = new();

    private IReadOnlyDictionary<(uint, bool), IDalamudTextureWrap> ItemIcons => _itemIcons;
    private IReadOnlyDictionary<(uint, bool), IDalamudTextureWrap> StatusIcons => _statusIcons;
    private IReadOnlyDictionary<(uint, bool), IDalamudTextureWrap> EventItemIcons => _eventItemIcons;

    public void Dispose()
    {
        foreach (var tex in ItemIcons.Values.Concat(StatusIcons.Values))
            tex.Dispose();
    }

    private void AddIcon(IDictionary<(uint, bool), IDalamudTextureWrap> dict, uint icon, bool hq = false) {
        if (dict.ContainsKey((icon, hq)))
            return;

        var tex = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(icon, hq)).GetWrapOrDefault();
        if (tex != null)
            dict[(icon, hq)] = tex;
    }

    private void AddItem(Item item, bool hq)
    {
        AddIcon(_itemIcons, item.Icon, hq);
    }

    private void AddStatus(Status status)
    {
        AddIcon(_statusIcons, status.Icon);
    }

    private void AddEventItem(EventItem item)
    {
        AddIcon(_eventItemIcons, item.Icon);
    }

    internal IDalamudTextureWrap? GetItem(Item item, bool hq = false)
    {
        AddItem(item, hq);
        ItemIcons.TryGetValue((item.Icon, hq), out var icon);
        return icon;
    }

    internal IDalamudTextureWrap? GetStatus(Status status)
    {
        AddStatus(status);
        StatusIcons.TryGetValue((status.Icon, false), out var icon);
        return icon;
    }

    internal IDalamudTextureWrap? GetEventItem(EventItem item)
    {
        AddEventItem(item);
        EventItemIcons.TryGetValue((item.Icon, false), out var icon);
        return icon;
    }
}
