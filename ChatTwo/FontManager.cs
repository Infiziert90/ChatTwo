using Dalamud.Interface;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using ImGuiNET;

namespace ChatTwo;

public class FontManager
{
    internal IFontHandle Axis { get; private set; }
    internal IFontHandle AxisItalic { get; private set; }

    internal IFontHandle RegularFont { get; private set; }
    internal IFontHandle? ItalicFont { get; private set; }

    internal IFontHandle FontAwesome { get; private set; }

    private readonly byte[] GameSymFont;

    private ushort[] Ranges;
    private ushort[] JpRange;
    private readonly ushort[] SymRange = [0xE020, 0xE0DB, 0];


    public static readonly HashSet<float> AxisFontSizeList =
    [
        9.6f, 10f, 12f, 14f, 16f,
        18f, 18.4f, 20f, 23f, 34f,
        36f, 40f, 45f, 46f, 68f, 90f,
    ];

    public FontManager()
    {
        var filePath = Path.Combine(Plugin.Interface.ConfigDirectory.FullName, "FFXIV_Lodestone_SSF.ttf");
        if (File.Exists(filePath))
        {
            GameSymFont = File.ReadAllBytes(filePath);
        }
        else
        {
            GameSymFont = new HttpClient().GetAsync("https://img.finalfantasyxiv.com/lds/pc/global/fonts/FFXIV_Lodestone_SSF.ttf")
                .Result
                .Content
                .ReadAsByteArrayAsync()
                .Result;

            Dalamud.Utility.Util.WriteAllBytesSafe(filePath, GameSymFont);
        }
    }

    private unsafe void SetUpRanges()
    {
        ushort[] BuildRange(IReadOnlyList<ushort>? chars, params nint[] ranges)
        {
            var builder = new ImFontGlyphRangesBuilderPtr(ImGuiNative.ImFontGlyphRangesBuilder_ImFontGlyphRangesBuilder());
            // text
            foreach (var range in ranges)
                builder.AddRanges(range);

            // chars
            if (chars != null)
            {
                for (var i = 0; i < chars.Count; i += 2)
                {
                    if (chars[i] == 0)
                        break;

                    for (var j = (uint) chars[i]; j <= chars[i + 1]; j++)
                        builder.AddChar((ushort) j);
                }
            }

            // various symbols
            // French
            // Romanian
            builder.AddText("←→↑↓《》■※☀★★☆♥♡ヅツッシ☀☁☂℃℉°♀♂♠♣♦♣♧®©™€$£♯♭♪✓√◎◆◇♦■□〇●△▽▼▲‹›≤≥<«“”─＼～");
            builder.AddText("Œœ");
            builder.AddText("ĂăÂâÎîȘșȚț");

            // "Enclosed Alphanumerics" (partial) https://www.compart.com/en/unicode/block/U+2460
            for (var i = 0x2460; i <= 0x24B5; i++)
                builder.AddChar((char) i);

            builder.AddChar('⓪');
            return builder.BuildRangesToArray();
        }

        var ranges = new List<nint> { ImGui.GetIO().Fonts.GetGlyphRangesDefault() };
        foreach (var extraRange in Enum.GetValues<ExtraGlyphRanges>())
            if (Plugin.Config.ExtraGlyphRanges.HasFlag(extraRange))
                ranges.Add(extraRange.Range());

        Ranges = BuildRange(null, ranges.ToArray());
        JpRange = BuildRange(GlyphRangesJapanese.GlyphRanges);
    }

    public void BuildFonts()
    {
        SetUpRanges();

        Axis = Plugin.Interface.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamily.Axis, SizeInPx(Plugin.Config.FontSizeV2)));
        AxisItalic = Plugin.Interface.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamily.Axis, SizeInPx(Plugin.Config.FontSizeV2))
        {
            SkewStrength = SizeInPx(Plugin.Config.FontSizeV2) / 6
        });

        FontAwesome = Plugin.Interface.UiBuilder.FontAtlas.NewDelegateFontHandle(e =>
        {
            e.OnPreBuild(tk => tk.AddFontAwesomeIconFont(new SafeFontConfig { SizePx = GetFontSize() }));
            e.OnPostBuild(tk => tk.FitRatio(tk.Font));
        });

        RegularFont = Plugin.Interface.UiBuilder.FontAtlas.NewDelegateFontHandle(
            e => e.OnPreBuild(
                tk =>
                {
                    var config = new SafeFontConfig {SizePt = Plugin.Config.GlobalFontV2.SizePt, GlyphRanges = Ranges};
                    config.MergeFont = Plugin.Config.GlobalFontV2.FontId.AddToBuildToolkit(tk, config);

                    config.SizePt = Plugin.Config.JapaneseFontV2.SizePt;
                    config.GlyphRanges = JpRange;
                    Plugin.Config.JapaneseFontV2.FontId.AddToBuildToolkit(tk, config);

                    config.SizePt = Plugin.Config.SymbolsFontSizeV2;
                    config.GlyphRanges = SymRange;
                    tk.AddFontFromMemory(GameSymFont, config, "ChatTwo2 Sym Font");

                    tk.Font = config.MergeFont;
                }
            ));

        // load italic version if it exists, else default to regular
        ItalicFont = Plugin.Interface.UiBuilder.FontAtlas.NewDelegateFontHandle(
            e => e.OnPreBuild(
                tk =>
                {
                    var italicVersion = Plugin.Config.GlobalFontV2.FontId.Family.Fonts.FirstOrDefault(f => f.EnglishName.Contains("Italic"));

                    var config = new SafeFontConfig {SizePt = Plugin.Config.GlobalFontV2.SizePt, GlyphRanges = Ranges};
                    config.MergeFont = italicVersion?.AddToBuildToolkit(tk, config) ?? Plugin.Config.GlobalFontV2.FontId.AddToBuildToolkit(tk, config);

                    config.SizePt = Plugin.Config.JapaneseFontV2.SizePt;
                    config.GlyphRanges = JpRange;
                    Plugin.Config.JapaneseFontV2.FontId.AddToBuildToolkit(tk, config);

                    config.SizePt = Plugin.Config.SymbolsFontSizeV2;
                    config.GlyphRanges = SymRange;
                    tk.AddFontFromMemory(GameSymFont, config, "ChatTwo2 Sym Font");

                    tk.Font = config.MergeFont;
                }
            ));
    }

    public static float SizeInPt(float px) => (float) (px * 3.0 / 4.0);
    public static float SizeInPx(float pt) => (float) (pt * 4.0 / 3.0);
    public static float GetFontSize() => Plugin.Config.FontsEnabled ? Plugin.Config.GlobalFontV2.SizePx : SizeInPx(Plugin.Config.FontSizeV2);
}
