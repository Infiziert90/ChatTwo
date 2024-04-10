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
        Source = source;
        Link = link;
    }

    internal SeString? GetSeString() => Source switch {
        ChunkSource.None => null,
        ChunkSource.Sender => Message?.SenderSource,
        ChunkSource.Content => Message?.ContentSource,
        _ => null,
    };

    /// <summary>
    /// Get some basic text for use in generating hashes.
    /// </summary>
    internal string StringValue() {
        switch (this) {
            case TextChunk text:
                return text.Content;
            case IconChunk icon:
                return icon.Icon.ToString();
            default:
                return "";
        }
    }
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
        Content = content;
    }

    #pragma warning disable CS8618
    public TextChunk() : base(ChunkSource.None, null) {
    }
    #pragma warning restore CS8618

    /// <summary>
    /// Creates a new TextChunk with identical styling to this one.
    /// </summary>
    public TextChunk NewWithStyle(ChunkSource source, Payload? link, string content)
    {
        return new TextChunk(source, link, content) {
            FallbackColour = FallbackColour,
            Foreground = Foreground,
            Glow = Glow,
            Italic = Italic,
        };
    }
}

internal class IconChunk : Chunk {
    internal BitmapFontIcon Icon { get; set; }

    public IconChunk(ChunkSource source, Payload? link, BitmapFontIcon icon) : base(source, link) {
        Icon = icon;
    }

    public IconChunk() : base(ChunkSource.None, null) {
    }
}
