using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using Lumina.Excel;
using Lumina.Text.Payloads;
using Lumina.Text.ReadOnly;
using Pidgin;
using static Pidgin.Parser;
using static Pidgin.Parser<char>;

namespace ChatTwo.Util;

internal static class AutoTranslate
{
    private static readonly Dictionary<ClientLanguage, List<AutoTranslateEntry>> Entries = new();
    private static readonly HashSet<(uint, uint)> ValidEntries = [];

    private static Parser<char, (string name, Maybe<IEnumerable<ISelectorPart>> selector)> Parser()
    {
        var sheetName = Any
            .AtLeastOnceUntil(Lookahead(Char('[').IgnoreResult().Or(End)))
            .Select(string.Concat)
            .Labelled("sheetName");
        var numPair = Map(ISelectorPart (first, second) =>
                    new IndexRange(uint.Parse(string.Concat(first)), uint.Parse(string.Concat(second))),
                Digit.AtLeastOnce().Before(Char('-')),
                Digit.AtLeastOnce())
            .Labelled("numPair");
        var singleRow = Digit
            .AtLeastOnce()
            .Select(string.Concat)
            .Select(ISelectorPart (num) => new SingleRow(uint.Parse(num)));
        var column = String("col-")
            .Then(Digit.AtLeastOnce())
            .Select(string.Concat)
            .Select(ISelectorPart (num) => new ColumnSpecifier(uint.Parse(num)));
        var noun = String("noun")
            .Select(ISelectorPart (_) => new NounMarker());
        var selectorItems = OneOf(Try(numPair), singleRow, column, noun)
            .Separated(Char(','))
            .Labelled("selectorItems");
        var selector = selectorItems
            .Between(Char('['), Char(']'))
            .Labelled("selector");
        return Map((name, sel) => (name, sel), sheetName, selector.Optional());
    }

    /// <summary>
    /// Preloads auto-translate entries into the cache for the current game
    /// language. Without this, the first message will take a long time to send
    /// (which causes a hitch in the main thread).
    ///
    /// This spawns a new thread.
    /// </summary>
    internal static void PreloadCache()
    {
        new Thread(() =>
        {
            var sw = Stopwatch.StartNew();
            AllEntries();
            Plugin.Log.Debug($"Warming up auto-translate took {sw.ElapsedMilliseconds}ms");
        }).Start();
    }

    private static List<AutoTranslateEntry> AllEntries()
    {
        if (Entries.TryGetValue(Plugin.DataManager.Language, out var entries))
            return entries;

        var shouldAdd = ValidEntries.Count == 0;

        var parser = Parser();
        var list = new List<AutoTranslateEntry>();
        foreach (var row in Sheets.CompletionSheet)
        {
            var lookup = string.Concat(row.LookupTable.Select(p => p.Type == ReadOnlySePayloadType.Text ? Encoding.UTF8.GetString(p.Body.Span) : p.MacroCode == MacroCode.Num && p.TryGetExpression(out var num) && num.TryGetInt(out var val) ? val.ToString(CultureInfo.InvariantCulture) : ",,,unexpected macro code,,,"));
            try
            {
                if (lookup is not ("" or "@"))
                {
                    // SE added whitespace to the newest additions, but ParseOrThrow doesn't see them as valid
                    lookup = lookup.Replace(" ", "");

                    var (sheetName, selector) = parser.ParseOrThrow(lookup);
                    var sheet = Plugin.DataManager.Excel.GetSheet<WorkingRawRow>(name: sheetName);

                    var columns = new List<int>();
                    var rows = new List<Range>();
                    if (selector.HasValue)
                    {
                        columns.Clear();
                        rows.Clear();
                        foreach (var part in selector.Value)
                        {
                            switch (part)
                            {
                                case IndexRange range:
                                {
                                    var start = (int)range.Start;
                                    var end   = (int)(range.End + 1);
                                    rows.Add(start..end);
                                    break;
                                }
                                case SingleRow single:
                                {
                                    var idx = (int)single.Row;
                                    rows.Add(idx..(idx + 1));
                                    break;
                                }
                                case ColumnSpecifier col:
                                    columns.Add((int)col.Column);
                                    break;
                            }
                        }
                    }

                    if (columns.Count == 0)
                        columns.Add(0);

                    if (rows.Count == 0)
                        // We can't use an "index from end" (like `^0`) here because
                        // we're iterating over integers, not an array directly.
                        // Previously, we were setting `0..^0` which caused these
                        // sheets to be completely skipped due to this bug.
                        // See below.
                        rows.Add(..Index.FromStart((int)sheet.GetRowAt(sheet.Count - 1).RowId + 1));

                    foreach (var range in rows)
                    {
                        // We iterate over the range by numerical values here, so
                        // we can't use an "index from end" otherwise nothing will
                        // happen.
                        // See above.
                        for (var i = range.Start.Value; i < range.End.Value; i++)
                        {
                            if (!sheet.TryGetRow((uint)i, out var rowParser))
                                continue;

                            foreach (var col in columns)
                            {
                                var rawName = rowParser.RawRow.ReadStringColumn(col);
                                var name = rawName.ToDalamudString();
                                var text = name.TextValue;
                                if (text.Length > 0)
                                {
                                    list.Add(new AutoTranslateEntry(row.Group, (uint)i, text, name));

                                    if (shouldAdd)
                                        ValidEntries.Add((row.Group, (uint)i));
                                }
                            }
                        }
                    }
                }
                else if (lookup is not "@")
                {
                    var text = row.Text.ToDalamudString();
                    list.Add(new AutoTranslateEntry(row.Group, row.RowId, text.TextValue, text));

                    if (shouldAdd)
                        ValidEntries.Add((row.Group, row.RowId));
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, $"failed to translate: {lookup}");
            }
        }

        Entries[Plugin.DataManager.Language] = list;
        return list;
    }

    internal static List<AutoTranslateEntry> Matching(string prefix, bool sort)
    {
        var wholeMatches = new List<AutoTranslateEntry>();
        var prefixMatches = new List<AutoTranslateEntry>();
        var otherMatches = new List<AutoTranslateEntry>();
        foreach (var entry in AllEntries())
        {
            if (entry.String.Equals(prefix, StringComparison.OrdinalIgnoreCase))
                wholeMatches.Add(entry);
            else if (entry.String.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                prefixMatches.Add(entry);
            else if (entry.String.Contains(prefix, StringComparison.OrdinalIgnoreCase))
                otherMatches.Add(entry);
        }

        if (sort)
        {
            return wholeMatches.OrderBy(entry => entry.String, StringComparer.OrdinalIgnoreCase)
                .Concat(prefixMatches.OrderBy(entry => entry.String, StringComparer.OrdinalIgnoreCase))
                .Concat(otherMatches.OrderBy(entry => entry.String, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        return wholeMatches
            .Concat(prefixMatches)
            .Concat(otherMatches)
            .ToList();
    }

    [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int memcmp(byte[] b1, byte[] b2, nuint count);

    internal static void ReplaceWithPayload(ref byte[] bytes)
    {
        var search = "<at:"u8.ToArray();
        if (bytes.Length <= search.Length)
            return;

        // populate the list of valid entries
        if (ValidEntries.Count == 0)
            AllEntries();

        var start = -1;
        for (var i = 0; i < bytes.Length; i++)
        {
            if (start != -1)
            {
                if (bytes[i] != '>')
                    continue;

                var tag = Encoding.UTF8.GetString(bytes[start..(i + 1)]);
                var parts = tag[4..^1].Split(',', 2);
                if (parts.Length == 2 && uint.TryParse(parts[0], out var group) && uint.TryParse(parts[1], out var key))
                {
                    var payload = ValidEntries.Contains((group, key))
                        ? new AutoTranslatePayload(group, key).Encode()
                        : [];

                    var oldBytes = bytes.ToArray();
                    var lengthDiff = payload.Length - (i - start);
                    bytes = new byte[oldBytes.Length + lengthDiff];
                    Array.Copy(oldBytes, bytes, start);
                    Array.Copy(payload, 0, bytes, start, payload.Length);
                    Array.Copy(oldBytes, i + 1, bytes, start + payload.Length, oldBytes.Length - (i + 1));

                    i += lengthDiff;
                }

                start = -1;
            }

            if (i + search.Length < bytes.Length && memcmp(bytes[i..], search, (nuint) search.Length) == 0)
                start = i;
        }
    }
}

[Sheet]
public readonly struct WorkingRawRow(RawRow row) : IExcelRow<WorkingRawRow>
{
    public uint RowId => row.RowId;
    public RawRow RawRow => row;

    static WorkingRawRow IExcelRow<WorkingRawRow>.Create(ExcelPage page, uint offset, uint row) =>
        new(new RawRow(page, offset, row));
}

internal interface ISelectorPart { }

internal class SingleRow : ISelectorPart
{
    public uint Row { get; }

    public SingleRow(uint row)
    {
        Row = row;
    }
}

internal class IndexRange : ISelectorPart
{
    public uint Start { get; }
    public uint End { get; }

    public IndexRange(uint start, uint end)
    {
        Start = start;
        End = end;
    }
}

internal class NounMarker : ISelectorPart { }

internal class ColumnSpecifier : ISelectorPart
{
    public uint Column { get; }

    public ColumnSpecifier(uint column)
    {
        Column = column;
    }
}

internal class AutoTranslateEntry
{
    internal uint Group { get; }
    internal uint Row { get; }
    internal string String { get; }
    internal SeString SeString { get; }

    public AutoTranslateEntry(uint group, uint row, string str, SeString seStr)
    {
        Group = group;
        Row = row;
        String = str;
        SeString = seStr;
    }
}
