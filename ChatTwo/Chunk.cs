using ChatTwo.Code;
using Dalamud.Game.Text.SeStringHandling;

namespace ChatTwo;

internal abstract class Chunk {
}

internal class TextChunk : Chunk {
    internal ChatType? FallbackColour { get; set; }
    internal uint? Foreground { get; set; }
    internal uint? Glow { get; set; }
    internal bool Italic { get; set; }
    internal string Content { get; set; }

    internal TextChunk(string content) {
        this.Content = content;
    }

    internal TextChunk(ChatType? fallbackColour, uint? foreground, uint? glow, bool italic, string content) {
        this.FallbackColour = fallbackColour;
        this.Foreground = foreground;
        this.Glow = glow;
        this.Italic = italic;
        this.Content = content;
    }
}

internal class IconChunk : Chunk {
    internal BitmapFontIcon Icon;

    public IconChunk(BitmapFontIcon icon) {
        this.Icon = icon;
    }
}
