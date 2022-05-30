using System.Runtime.InteropServices;
using System.Text;
using Dalamud;
using Dalamud.Data;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using Pidgin;
using static Pidgin.Parser;
using static Pidgin.Parser<char>;
using TextPayload = Lumina.Text.Payloads.TextPayload;

namespace ChatTwo.Util;

internal static class AutoTranslate {
    private static readonly Dictionary<ClientLanguage, List<AutoTranslateEntry>> Entries = new();

    private static Parser<char, (string name, Maybe<IEnumerable<ISelectorPart>> selector)> Parser() {
        var sheetName = Any
            .AtLeastOnceUntil(Lookahead(Char('[').IgnoreResult().Or(End)))
            .Select(string.Concat)
            .Labelled("sheetName");
        var numPair = Map(
                (first, second) => (ISelectorPart) new IndexRange(
                    uint.Parse(string.Concat(first)),
                    uint.Parse(string.Concat(second))
                ),
                Digit.AtLeastOnce().Before(Char('-')),
                Digit.AtLeastOnce()
            )
            .Labelled("numPair");
        var singleRow = Digit
            .AtLeastOnce()
            .Select(string.Concat)
            .Select(num => (ISelectorPart) new SingleRow(uint.Parse(num)));
        var column = String("col-")
            .Then(Digit.AtLeastOnce())
            .Select(string.Concat)
            .Select(num => (ISelectorPart) new ColumnSpecifier(uint.Parse(num)));
        var noun = String("noun")
            .Select(_ => (ISelectorPart) new NounMarker());
        var selectorItems = OneOf(
                Try(numPair),
                singleRow,
                column,
                noun
            )
            .Separated(Char(','))
            .Labelled("selectorItems");
        var selector = selectorItems
            .Between(Char('['), Char(']'))
            .Labelled("selector");
        return Map(
            (name, selector) => (name, selector),
            sheetName,
            selector.Optional()
        );
    }

    private static string TextValue(this Lumina.Text.SeString str) {
        var payloads = str.Payloads
            .Select(p => {
                if (p is TextPayload text) {
                    return p.Data[0] == 0x03
                        ? text.RawString[1..]
                        : text.RawString;
                }

                if (p.Data.Length <= 1) {
                    return "";
                }

                if (p.Data[1] == 0x1F) {
                    return "-";
                }

                if (p.Data.Length > 2 && p.Data[1] == 0x20) {
                    var value = p.Data.Length > 4
                        ? p.Data[3] - 1
                        : p.Data[2];
                    return ((char) (48 + value)).ToString();
                }

                return "";
            });
        return string.Join("", payloads);
    }

    private static List<AutoTranslateEntry> AllEntries(DataManager data) {
        if (Entries.TryGetValue(data.Language, out var entries)) {
            return entries;
        }

        var parser = Parser();
        var list = new List<AutoTranslateEntry>();
        foreach (var row in data.GetExcelSheet<Completion>()!) {
            var lookup = row.LookupTable.TextValue();
            if (lookup is not ("" or "@")) {
                var (sheetName, selector) = parser.ParseOrThrow(lookup);
                var sheetType = typeof(Completion)
                    .Assembly
                    .GetType($"Lumina.Excel.GeneratedSheets.{sheetName}")!;
                var getSheet = data
                    .GetType()
                    .GetMethod("GetExcelSheet", Type.EmptyTypes)!
                    .MakeGenericMethod(sheetType);
                var sheet = (ExcelSheetImpl) getSheet.Invoke(data, null)!;
                var rowParsers = sheet.GetRowParsers().ToArray();

                var columns = new List<int>();
                var rows = new List<Range>();
                if (selector.HasValue) {
                    columns.Clear();
                    rows.Clear();
                    foreach (var part in selector.Value) {
                        switch (part) {
                            case IndexRange range: {
                                var start = (int) range.Start;
                                var end = (int) (range.End + 1);
                                rows.Add(start..end);
                                break;
                            }
                            case SingleRow single: {
                                var idx = (int) single.Row;
                                rows.Add(idx..(idx + 1));
                                break;
                            }
                            case ColumnSpecifier col:
                                columns.Add((int) col.Column);
                                break;
                        }
                    }
                }

                if (columns.Count == 0) {
                    columns.Add(0);
                }

                if (rows.Count == 0) {
                    rows.Add(..);
                }

                var validRows = rowParsers
                    .Select(parser => parser.RowId)
                    .ToArray();
                foreach (var range in rows) {
                    for (var i = range.Start.Value; i < range.End.Value; i++) {
                        if (!validRows.Contains((uint) i)) {
                            continue;
                        }

                        foreach (var col in columns) {
                            var rowParser = rowParsers.FirstOrDefault(parser => parser.RowId == i);
                            if (rowParser == null) {
                                continue;
                            }

                            var rawName = rowParser.ReadColumn<Lumina.Text.SeString>(col)!;
                            var name = rawName.ToDalamudString();
                            var text = name.TextValue;
                            if (text.Length > 0) {
                                list.Add(new AutoTranslateEntry(
                                    row.Group,
                                    (uint) i,
                                    text,
                                    name
                                ));
                            }
                        }
                    }
                }
            } else if (lookup is not "@") {
                var text = row.Text.ToDalamudString();
                list.Add(new AutoTranslateEntry(
                    row.Group,
                    row.RowId,
                    text.TextValue,
                    text
                ));
            }
        }

        Entries[data.Language] = list;
        return list;
    }

    internal static List<AutoTranslateEntry> Matching(DataManager data, string prefix) {
        var wholeMatches = new List<AutoTranslateEntry>();
        var prefixMatches = new List<AutoTranslateEntry>();
        var otherMatches = new List<AutoTranslateEntry>();
        foreach (var entry in AllEntries(data)) {
            if (entry.String.Equals(prefix, StringComparison.OrdinalIgnoreCase)) {
                wholeMatches.Add(entry);
            } else if (entry.String.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
                prefixMatches.Add(entry);
            } else if (entry.String.Contains(prefix, StringComparison.OrdinalIgnoreCase)) {
                otherMatches.Add(entry);
            }
        }

        return wholeMatches.OrderBy(entry => entry.String, StringComparer.OrdinalIgnoreCase)
            .Concat(prefixMatches.OrderBy(entry => entry.String, StringComparer.OrdinalIgnoreCase))
            .Concat(otherMatches.OrderBy(entry => entry.String, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int memcmp(byte[] b1, byte[] b2, UIntPtr count);

    internal static void ReplaceWithPayload(DataManager data, ref byte[] bytes) {
        var search = Encoding.UTF8.GetBytes("<at:");
        if (bytes.Length <= search.Length) {
            return;
        }

        var start = -1;
        for (var i = 0; i < bytes.Length; i++) {
            if (start != -1) {
                if (bytes[i] == '>') {
                    var tag = Encoding.UTF8.GetString(bytes[start..(i + 1)]);
                    var parts = tag[4..^1].Split(',', 2);
                    if (uint.TryParse(parts[0], out var group) && uint.TryParse(parts[1], out var key)) {
                        var payload = AllEntries(data).FirstOrDefault(entry => entry.Group == group && entry.Row == key) == null
                            ? Array.Empty<byte>()
                            : new AutoTranslatePayload(group, key).Encode();
                        var oldBytes = bytes.ToArray();
                        var lengthDiff = payload.Length - (i - start);
                        bytes = new byte[oldBytes.Length + lengthDiff];
                        Array.Copy(oldBytes, bytes, start);
                        Array.Copy(payload, 0, bytes, start, payload.Length);
                        Array.Copy(oldBytes, i + 1, bytes, start + payload.Length, oldBytes.Length - (i + 1));

                        i += lengthDiff;
                    }

                    start = -1;
                } else {
                    continue;
                }
            }

            if (i + search.Length < bytes.Length && memcmp(bytes[i..], search, (UIntPtr) search.Length) == 0) {
                start = i;
            }
        }
    }
}

internal interface ISelectorPart {
}

internal class SingleRow : ISelectorPart {
    public uint Row { get; }

    public SingleRow(uint row) {
        this.Row = row;
    }
}

internal class IndexRange : ISelectorPart {
    public uint Start { get; }
    public uint End { get; }

    public IndexRange(uint start, uint end) {
        this.Start = start;
        this.End = end;
    }
}

internal class NounMarker : ISelectorPart {
}

internal class ColumnSpecifier : ISelectorPart {
    public uint Column { get; }

    public ColumnSpecifier(uint column) {
        this.Column = column;
    }
}

internal class AutoTranslateEntry {
    internal uint Group { get; }
    internal uint Row { get; }
    internal string String { get; }
    internal SeString SeString { get; }

    public AutoTranslateEntry(uint group, uint row, string str, SeString seStr) {
        this.Group = group;
        this.Row = row;
        this.String = str;
        this.SeString = seStr;
    }
}
