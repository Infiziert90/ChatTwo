using System.Drawing;
using Vanara.PInvoke;

namespace ChatTwo.Ui;

internal static class Fonts {
    internal const string IncludedIndicator = "Chat 2: ";

    internal static readonly Font[] GlobalFonts = {
        new(
            $"{IncludedIndicator}Noto Sans",
            "ChatTwo.fonts.NotoSans-Regular.ttf",
            "ChatTwo.fonts.NotoSans-Italic.ttf"
        ),
        new(
            $"{IncludedIndicator}Noto Serif",
            "ChatTwo.fonts.NotoSerif-Regular.ttf",
            "ChatTwo.fonts.NotoSerif-Italic.ttf"
        ),
        new(
            $"{IncludedIndicator}Open Sans",
            "ChatTwo.fonts.OpenSans-Regular.ttf",
            "ChatTwo.fonts.OpenSans-Italic.ttf"
        ),
        new(
            $"{IncludedIndicator}Roboto",
            "ChatTwo.fonts.Roboto-Regular.ttf",
            "ChatTwo.fonts.Roboto-Italic.ttf"
        ),
    };

    internal static readonly (string, string)[] JapaneseFonts = {
        ($"{IncludedIndicator}Noto Sans JP", "ChatTwo.fonts.NotoSansJP-Regular.otf"),
        // ($"{IncludedIndicator}Noto Serif JP", "ChatTwo.fonts.NotoSerifJP-Regular.otf"),
    };

    internal static List<string> GetJpFonts() {
        var fonts = new List<string>();
        using var g = Graphics.FromImage(new Bitmap(1, 1));
        foreach (var (lpelfe, _, fontType) in Gdi32.EnumFontFamiliesEx(g.GetHdc(), CharacterSet.SHIFTJIS_CHARSET)) {
            var name = lpelfe.elfEnumLogfontEx.elfLogFont.lfFaceName;
            if (name.StartsWith("@")) {
                continue;
            }

            fonts.Add(name);
        }

        return fonts;
    }

    internal static unsafe FontData? GetFont(string name, bool withItalic, CharacterSet charset = CharacterSet.ANSI_CHARSET) {
        var regularFont = Gdi32.CreateFontIndirect(new LOGFONT {
            lfFaceName = name,
            lfItalic = false,
            lfCharSet = charset,
            lfOutPrecision = LogFontOutputPrecision.OUT_TT_ONLY_PRECIS,
        });

        using var g = Graphics.FromImage(new Bitmap(1, 1));
        var hdc = g.GetHdc();

        byte[]? GetFontData(HGDIOBJ obj) {
            Gdi32.SelectObject(hdc, obj);
            var size = Gdi32.GetFontData(hdc, pvBuffer: IntPtr.Zero);
            var data = new byte[size];
            fixed (byte* p = data) {
                var res = Gdi32.GetFontData(hdc, pvBuffer: (IntPtr) p, cjBuffer: size);
                Gdi32.DeleteObject(obj);
                if (res == Gdi32.GDI_ERROR) {
                    return null;
                }
            }

            return data;
        }

        var regular = GetFontData(regularFont);
        var italic = Array.Empty<byte>();
        if (withItalic) {
            var italicFont = Gdi32.CreateFontIndirect(new LOGFONT {
                lfFaceName = name,
                lfItalic = true,
                lfCharSet = charset,
                lfOutPrecision = LogFontOutputPrecision.OUT_TT_ONLY_PRECIS,
            });

            italic = GetFontData(italicFont);
        }

        if (regular == null || italic == null) {
            return null;
        }

        return new FontData(regular, italic);
    }
}

internal sealed class FontData {
    internal byte[] Regular { get; }
    internal byte[] Italic { get; }

    internal FontData(byte[] regular, byte[] italic) {
        this.Regular = regular;
        this.Italic = italic;
    }
}

internal sealed class Font {
    internal string Name { get; }
    internal string ResourcePath { get; }
    internal string ResourcePathItalic { get; }

    internal Font(string name, string resourcePath, string resourcePathItalic) {
        this.Name = name;
        this.ResourcePath = resourcePath;
        this.ResourcePathItalic = resourcePathItalic;
    }
}
