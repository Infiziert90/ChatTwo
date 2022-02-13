using ChatTwo.Code;
using Dalamud.Game.Text.SeStringHandling;
using LiteDB;

namespace ChatTwo;

internal abstract class Chunk {
    [BsonIgnore]
    internal Message? Message { get; set; }

    internal ChunkSource Source { get; set; }
    internal Payload? Link { get; set; }

    protected Chunk(ChunkSource source, Payload? link) {
        this.Source = source;
        this.Link = link;
    }

    internal SeString? GetSeString() => this.Source switch {
        ChunkSource.None => null,
        ChunkSource.Sender => this.Message?.SenderSource,
        ChunkSource.Content => this.Message?.ContentSource,
        _ => null,
    };
}

internal enum ChunkSource {
    None,
    Sender,
    Content,
}

internal class TextChunk : Chunk {
    internal ChatType? FallbackColour { get; set; }
    internal uint? Foreground { get; set; }
    internal uint? Glow { get; set; }
    internal bool Italic { get; set; }
    internal string Content { get; set; }

    internal TextChunk(ChunkSource source, Payload? link, string content) : base(source, link) {
        this.Content = content;
    }

    #pragma warning disable CS8618
    public TextChunk() : base(ChunkSource.None, null) {
    }
    #pragma warning restore CS8618
}

internal class IconChunk : Chunk {
    internal BitmapFontIcon Icon { get; set; }

    public IconChunk(ChunkSource source, Payload? link, BitmapFontIcon icon) : base(source, link) {
        this.Icon = icon;
    }

    public IconChunk() : base(ChunkSource.None, null) {
    }
}
