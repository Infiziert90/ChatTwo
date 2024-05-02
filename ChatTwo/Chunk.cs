using ChatTwo.Code;
using Dalamud.Game.Text.SeStringHandling;
using LiteDB;
using MessagePack;

namespace ChatTwo;

[Union(0, typeof(TextChunk))]
[Union(1, typeof(IconChunk))]
[MessagePackObject]
public abstract class Chunk
{
    [IgnoreMember]
    [BsonIgnore] // used by LegacyMessageImporter
    internal Message? Message { get; set; }

    [Key(0)]
    public ChunkSource Source { get; set; }

    [Key(1)]
    [MessagePackFormatter(typeof(PayloadMessagePackFormatter))]
    public Payload? Link { get; set; }

    protected Chunk(ChunkSource source, Payload? link)
    {
        Source = source;
        Link = link;
    }

    internal SeString? GetSeString() => Source switch
    {
        ChunkSource.None => null,
        ChunkSource.Sender => Message?.SenderSource,
        ChunkSource.Content => Message?.ContentSource,
        _ => null,
    };

    /// <summary>
    /// Get some basic text for use in generating hashes.
    /// </summary>
    internal string StringValue()
    {
        return this switch
        {
            TextChunk text => text.Content,
            IconChunk icon => icon.Icon.ToString(),
            _ => ""
        };
    }
}

public enum ChunkSource
{
    None,
    Sender,
    Content,
}

[MessagePackObject]
public class TextChunk : Chunk
{
    [Key(2)]
    public ChatType? FallbackColour { get; set; }
    [Key(3)]
    public uint? Foreground { get; set; }
    [Key(4)]
    public uint? Glow { get; set; }
    [Key(5)]
    public bool Italic { get; set; }
    [Key(6)]
    public string Content { get; set; }

    internal TextChunk(ChunkSource source, Payload? link, string content) : base(source, link)
    {
        Content = content;
    }

    // ReSharper disable once UnusedMember.Global // Used by MessagePack
    public TextChunk(ChunkSource source, Payload? link, ChatType? fallbackColour, uint? foreground, uint? glow, bool italic, string content) : base(source, link)
    {
        FallbackColour = fallbackColour;
        Foreground = foreground;
        Glow = glow;
        Italic = italic;
        Content = content;
    }

    /// <summary>
    /// Creates a new TextChunk with identical styling to this one.
    /// </summary>
    public TextChunk NewWithStyle(ChunkSource source, Payload? link, string content)
    {
        return new TextChunk(source, link, content)
        {
            FallbackColour = FallbackColour,
            Foreground = Foreground,
            Glow = Glow,
            Italic = Italic,
        };
    }
}

[MessagePackObject]
public class IconChunk : Chunk
{
    [Key(2)]
    public BitmapFontIcon Icon { get; set; }

    public IconChunk(ChunkSource source, Payload? link, BitmapFontIcon icon) : base(source, link)
    {
        Icon = icon;
    }
}
