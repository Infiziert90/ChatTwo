using ChatTwo.Code;
using Dalamud.Game.Text.SeStringHandling;

namespace ChatTwo;

internal abstract class Chunk {
    internal Payload? Link;

    protected Chunk(Payload? link) {
        this.Link = link;
    }
}

internal class TextChunk : Chunk {
    internal ChatType? FallbackColour { get; set; }
    internal uint? Foreground { get; set; }
    internal uint? Glow { get; set; }
    internal bool Italic { get; set; }
    internal string Content { get; set; }

    internal TextChunk(Payload? link, string content) : base(link) {
        this.Content = content;
    }

    internal TextChunk(Payload? link, ChatType? fallbackColour, uint? foreground, uint? glow, bool italic, string content) : base(link) {
        this.FallbackColour = fallbackColour;
        this.Foreground = foreground;
        this.Glow = glow;
        this.Italic = italic;
        this.Content = content;
    }
}

internal class IconChunk : Chunk {
    internal BitmapFontIcon Icon;

    public IconChunk(Payload? link, BitmapFontIcon icon) : base(link) {
        this.Icon = icon;
    }
}
