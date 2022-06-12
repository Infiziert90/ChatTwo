using System.Runtime.InteropServices;
using SharpDX;
using SharpDX.DirectWrite;
using FontStyle = SharpDX.DirectWrite.FontStyle;

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

    internal static List<string> GetFonts() {
        var fonts = new List<string>();

        using var factory = new Factory();
        using var collection = factory.GetSystemFontCollection(false);
        for (var i = 0; i < collection.FontFamilyCount; i++) {
            using var family = collection.GetFontFamily(i);
            var anyItalic = false;
            for (var j = 0; j < family.FontCount; j++) {
                try {
                    var font = family.GetFont(j);
                    if (font.IsSymbolFont || font.Style is not (FontStyle.Italic or FontStyle.Oblique)) {
                        continue;
                    }

                    anyItalic = true;
                    break;
                } catch (SharpDXException) {
                    // no-op
                }
            }

            if (!anyItalic) {
                continue;
            }

            var name = family.FamilyNames.GetString(0);
            fonts.Add(name);
        }

        fonts.Sort();
        return fonts;
    }

    internal static List<string> GetJpFonts() {
        var fonts = new List<string>();

        using var factory = new Factory();
        using var collection = factory.GetSystemFontCollection(false);
        for (var i = 0; i < collection.FontFamilyCount; i++) {
            using var family = collection.GetFontFamily(i);
            var probablyJp = false;
            for (var j = 0; j < family.FontCount; j++) {
                try {
                    using var font = family.GetFont(j);
                    if (!font.HasCharacter('気') || font.IsSymbolFont) {
                        continue;
                    }

                    probablyJp = true;
                    break;
                } catch (SharpDXException) {
                    // no-op
                }
            }

            if (!probablyJp) {
                continue;
            }

            var name = family.FamilyNames.GetString(0);
            fonts.Add(name);
        }

        fonts.Sort();
        return fonts;
    }

    internal static FontData? GetFont(string name, bool withItalic) {
        using var factory = new Factory();
        using var collection = factory.GetSystemFontCollection(false);
        for (var i = 0; i < collection.FontFamilyCount; i++) {
            using var family = collection.GetFontFamily(i);
            if (family.FamilyNames.GetString(0) != name) {
                continue;
            }

            using var normal = family.GetFirstMatchingFont(FontWeight.Normal, FontStretch.Normal, FontStyle.Normal);
            if (normal == null) {
                return null;
            }

            FaceData? GetFontData(SharpDX.DirectWrite.Font font) {
                using var face = new FontFace(font);
                var files = face.GetFiles();
                if (files.Length == 0) {
                    return null;
                }

                var key = files[0].GetReferenceKey();
                using var stream = files[0].Loader.CreateStreamFromKey(key);

                stream.ReadFileFragment(out var start, 0, stream.GetFileSize(), out var release);

                var data = new byte[stream.GetFileSize()];
                Marshal.Copy(start, data, 0, data.Length);

                stream.ReleaseFileFragment(release);

                var metrics = font.Metrics;
                var ratio = (metrics.Ascent + metrics.Descent + metrics.LineGap) / (float) metrics.DesignUnitsPerEm;

                return new FaceData(data, ratio);
            }

            var normalData = GetFontData(normal);
            if (normalData == null) {
                return null;
            }

            FaceData? italicData = null;
            if (withItalic) {
                using var italic = family.GetFirstMatchingFont(FontWeight.Normal, FontStretch.Normal, FontStyle.Italic)
                                   ?? family.GetFirstMatchingFont(FontWeight.Normal, FontStretch.Normal, FontStyle.Oblique);
                if (italic == null) {
                    return null;
                }

                italicData = GetFontData(italic);
            }

            if (italicData == null && withItalic) {
                return null;
            }

            return new FontData(normalData, italicData);
        }

        return null;
    }
}

internal sealed class FaceData {
    internal byte[] Data { get; }
    internal float Ratio { get; }

    internal FaceData(byte[] data, float ratio) {
        this.Data = data;
        this.Ratio = ratio;
    }
}

internal sealed class FontData {
    internal FaceData Regular { get; }
    internal FaceData? Italic { get; }

    internal FontData(FaceData regular, FaceData? italic) {
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
