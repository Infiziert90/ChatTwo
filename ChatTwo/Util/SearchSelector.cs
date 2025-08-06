using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Bindings.ImGui;
using System.Collections;
using Dalamud.Interface.Utility.Raii;

namespace ChatTwo.Util;

// Modified from: https://github.com/UnknownX7/Hypostasis/blob/master/ImGui/ExcelSheet.cs
public static class SearchSelector
{
    private static string[]? FilteredSearchSheet;

    private static string SheetSearchText = null!;
    private static string PrevSearchId = null!;
    private static Type PrevSearchType = null!;


    public record SelectorOptions
    {
        public Func<string, string> FormatRow { get; init; } = row => row.ToString();
        public Func<string, string, bool>? SearchPredicate { get; init; } = null;
        public Func<string, bool, bool>? DrawSelectable { get; init; } = null;
        public string[] FilteredSheet { get; init; } = [];
        public Vector2? Size { get; init; } = null;
    }

    public record SelectorPopupOptions: SelectorOptions
    {
        public ImGuiPopupFlags PopupFlags { get; init; } = ImGuiPopupFlags.None;
        public bool CloseOnSelection { get; init; } = false;
        public Func<string, bool> IsSelected { get; init; } = _ => false;
    }

    private static void SearchInput(string id, IEnumerable<string> filteredSheet, Func<string, string, bool> searchPredicate)
    {
        if (ImGui.IsWindowAppearing() && ImGui.IsWindowFocused() && !ImGui.IsAnyItemActive())
        {
            if (id != PrevSearchId)
            {
                if (typeof(string) != PrevSearchType)
                {
                    SheetSearchText = string.Empty;
                    PrevSearchType = typeof(string);
                }

                FilteredSearchSheet = null;
                PrevSearchId = id;
            }

            ImGui.SetKeyboardFocusHere(0);
        }

        if (ImGui.InputTextWithHint("##ExcelSheetSearch", "Search", ref SheetSearchText, 128, ImGuiInputTextFlags.AutoSelectAll))
            FilteredSearchSheet = null;

        FilteredSearchSheet ??= filteredSheet.Where(s => searchPredicate(s, SheetSearchText)).ToArray();
    }

    public static bool SelectorPopup(string id, out string selected, SelectorPopupOptions? options = null, bool close = false)
    {
        options ??= new SelectorPopupOptions();
        var sheet = options.FilteredSheet;
        selected = string.Empty;

        if (close)
            return false;

        ImGui.SetNextWindowSize(options.Size ?? new Vector2(0, 250 * ImGuiHelpers.GlobalScale));
        using var popup = ImRaii.ContextPopupItem(id, options.PopupFlags);
        if (!popup.Success)
            return false;

        SearchInput(id, sheet, options.SearchPredicate ?? ((row, s) => options.FormatRow(row).Contains(s, StringComparison.CurrentCultureIgnoreCase)));

        using var child = ImRaii.Child("SearchList", Vector2.Zero, true);
        if (!child.Success)
            return false;

        var ret = false;
        var drawSelectable = options.DrawSelectable ?? ((row, selected) => ImGui.Selectable(options.FormatRow(row), selected));
        using (var clipper = new ListClipper(FilteredSearchSheet!.Length))
        {
            foreach (var i in clipper.Rows)
            {
                var searched = FilteredSearchSheet[i];
                using var pushedId = ImRaii.PushId(id);
                if (!drawSelectable(searched, options.IsSelected(searched)))
                    continue;

                selected = searched;
                ret = true;
            }
        }

        // ImGui issue #273849, children keep popups from closing automatically
        if (ret && options.CloseOnSelection)
            ImGui.CloseCurrentPopup();

        return ret;
    }
}

public unsafe class ListClipper : IEnumerable<(int, int)>, IDisposable
{
    private ImGuiListClipperPtr Clipper;
    private readonly int CurrentRows;
    private readonly int CurrentColumns;
    private readonly bool TwoDimensional;
    private readonly int ItemRemainder;

    public int FirstRow { get; private set; } = -1;
    public int CurrentRow { get; private set; }
    public int DisplayEnd => Clipper.DisplayEnd;

    public IEnumerable<int> Rows
    {
        get
        {
            while (Clipper.Step()) // Supposedly this calls End()
            {
                if (Clipper.ItemsHeight > 0 && FirstRow < 0)
                    FirstRow = (int)(ImGui.GetScrollY() / Clipper.ItemsHeight);

                for (var i = Clipper.DisplayStart; i < Clipper.DisplayEnd; i++)
                {
                    CurrentRow = i;
                    yield return TwoDimensional ? i : i * CurrentColumns;
                }
            }
        }
    }

    private IEnumerable<int> Columns
    {
        get
        {
            var cols = (ItemRemainder == 0 || CurrentRows != DisplayEnd || CurrentRow != DisplayEnd - 1) ? CurrentColumns : ItemRemainder;
            for (var j = 0; j < cols; j++)
                yield return j;
        }
    }

    public ListClipper(int items, int cols = 1, bool twoD = false, float itemHeight = 0)
    {
        TwoDimensional = twoD;
        CurrentColumns = cols;
        CurrentRows = TwoDimensional ? items : (int)MathF.Ceiling((float)items / CurrentColumns);
        ItemRemainder = !TwoDimensional ? items % CurrentColumns : 0;
        Clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper());
        Clipper.Begin(CurrentRows, itemHeight);
    }

    public IEnumerator<(int, int)> GetEnumerator() => (from i in Rows from j in Columns select (i, j)).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose()
    {
        Clipper.Destroy(); // This also calls End() but I'm calling it anyway just in case
        GC.SuppressFinalize(this);
    }
}
