using ChatTwo.Code;
using Dalamud.Game.Text.SeStringHandling;

namespace ChatTwo;

internal abstract class Chunk {
    internal SeString? Source { get; set; }
    internal Payload? Link { get; set; }

    protected Chunk(SeString? source, Payload? link) {
        this.Source = source;
        this.Link = link;
    }
}

internal class TextChunk : Chunk {
    internal ChatType? FallbackColour { get; set; }
    internal uint? Foreground { get; set; }
    internal uint? Glow { get; set; }
    internal bool Italic { get; set; }
    internal string Content { get; set; }

    internal TextChunk(SeString? source, Payload? link, string content) : base(source, link) {
        this.Content = content;
    }

    internal TextChunk(SeString? source, Payload? link, ChatType? fallbackColour, uint? foreground, uint? glow, bool italic, string content) : base(source, link) {
        this.FallbackColour = fallbackColour;
        this.Foreground = foreground;
        this.Glow = glow;
        this.Italic = italic;
        this.Content = content;
    }
}

internal class IconChunk : Chunk {
    internal BitmapFontIcon Icon { get; set; }

    public IconChunk(SeString? source, Payload? link, BitmapFontIcon icon) : base(source, link) {
        this.Icon = icon;
    }
}
