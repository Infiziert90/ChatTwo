using System.Text;
using ChatTwo.Code;
using ChatTwo.Util;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using System.Text.RegularExpressions;
using LiteDB;

namespace ChatTwo;

internal class SortCode {
    internal ChatType Type { get; }
    internal ChatSource Source { get; }

    [BsonCtor] // Used by LegacyMessageImporter
    public SortCode(ChatType type, ChatSource source) {
        Type = type;
        Source = source;
    }

    internal SortCode(uint raw) {
        Type = (ChatType)(raw >> 16);
        Source = (ChatSource)(raw & 0xFFFF);
    }

    internal uint Encode() {
        return ((uint) Type << 16) | (uint) Source;
    }

    private bool Equals(SortCode other) {
        return Type == other.Type && Source == other.Source;
    }

    public override bool Equals(object? obj) {
        if (ReferenceEquals(null, obj)) {
            return false;
        }

        if (ReferenceEquals(this, obj)) {
            return true;
        }

        return obj.GetType() == GetType() && Equals((SortCode) obj);
    }

    public override int GetHashCode() {
        unchecked {
            return ((int) Type * 397) ^ (int) Source;
        }
    }
}

internal class Message
{
    internal Guid Id { get; } = Guid.NewGuid();
    internal ulong Receiver { get; }
    internal ulong ContentId { get; set; }

    internal DateTimeOffset Date { get; }
    internal ChatCode Code { get; }
    internal List<Chunk> Sender { get; }
    internal List<Chunk> Content { get; }

    internal SeString SenderSource { get; }
    internal SeString ContentSource { get; }

    internal SortCode SortCode { get; }
    internal Guid ExtraChatChannel { get; }

    // Not stored in the database:
    internal int Hash { get; }
    internal Dictionary<Guid, float?> Height { get; } = new();
    internal Dictionary<Guid, bool> IsVisible { get; } = new();

    internal Message(ulong receiver, ulong contentId, ChatCode code, List<Chunk> sender, List<Chunk> content, SeString senderSource, SeString contentSource)
    {
        Receiver = receiver;
        ContentId = contentId;
        Date = DateTimeOffset.UtcNow;
        Code = code;
        Sender = sender;
        Content = CheckMessageContent(content);
        SenderSource = senderSource;
        ContentSource = contentSource;
        SortCode = new SortCode(Code.Type, Code.Source);
        ExtraChatChannel = ExtractExtraChatChannel();
        Hash = GenerateHash();

        foreach (var chunk in sender.Concat(content))
            chunk.Message = this;
    }

    internal Message(Guid id, ulong receiver, ulong contentId, DateTimeOffset date, ChatCode code, List<Chunk> sender, List<Chunk> content, SeString senderSource, SeString contentSource, SortCode sortCode, Guid extraChatChannel)
    {
        Id = id;
        Receiver = receiver;
        ContentId = contentId;
        Date = date;
        Code = code;
        Sender = sender;
        // Don't call ReplaceContentURLs here since we're loading the message
        // from the database, and it should already have parsed URL data.
        Content = content;
        SenderSource = senderSource;
        ContentSource = contentSource;
        SortCode = sortCode;
        ExtraChatChannel = extraChatChannel;
        Hash = GenerateHash();

        foreach (var chunk in sender.Concat(content))
            chunk.Message = this;
    }

    private int GenerateHash()
    {
        return SortCode.GetHashCode()
               ^ ExtraChatChannel.GetHashCode()
               ^ string.Join("", Sender.Select(c => c.StringValue())).GetHashCode()
               ^ string.Join("", Content.Select(c => c.StringValue())).GetHashCode();
    }

    private Guid ExtractExtraChatChannel()
    {
        if (ContentSource.Payloads.Count > 0 && ContentSource.Payloads[0] is RawPayload raw)
        {
            // this does an encode and clone every time it's accessed, so cache
            var data = raw.Data;
            if (data[1] == 0x27 && data[2] == 18 && data[3] == 0x20)
                return new Guid(data[4..^1]);
        }

        return Guid.Empty;
    }

    private List<Chunk> CheckMessageContent(List<Chunk> oldChunks)
    {
        var newChunks = new List<Chunk>();
        void AddChunkWithMessage(TextChunk chunk)
        {
            if (string.IsNullOrEmpty(chunk.Content))
                return;

            chunk.Message = this;
            newChunks.Add(chunk);
        }

        void AddContentAfterURLCheck(string content, TextChunk text, Chunk chunk)
        {
            // This works because c# will split regex string, while keeping named groups as separated splits
            // If the match is the first content of a string, the array will start with a ""
            // Same if 2 matches are next to each other, they will be split with a ""
            var splits = URLRegex.Split(content);
            if (splits.Length == 1)
            {
                AddChunkWithMessage(text.NewWithStyle(chunk.Source, chunk.Link, content));
                return;
            }

            var nextIsMatch = false;
            foreach (var split in splits)
            {
                if (split == "" || !nextIsMatch)
                {
                    nextIsMatch = true;
                    AddChunkWithMessage(text.NewWithStyle(chunk.Source, chunk.Link, split));
                    continue;
                }

                // Create a new TextChunk with a URIPayload for the URL text.
                nextIsMatch = false;
                try
                {
                    var link = UriPayload.ResolveURI(split);
                    AddChunkWithMessage(text.NewWithStyle(chunk.Source, link, split));
                }
                catch (UriFormatException)
                {
                    AddChunkWithMessage(text.NewWithStyle(chunk.Source, chunk.Link, split));
                    Plugin.Log.Debug($"Invalid URL accepted by Regex but failed URI parsing: '{split}'");
                }
            }
        }

        foreach (var chunk in oldChunks)
        {
            // Use as is if it's not a text chunk or it already has a payload.
            if (chunk is not TextChunk text || chunk.Link != null)
            {
                // No need to call AddChunkWithMessage here since the chunk
                // already has the Message field set.
                newChunks.Add(chunk);
                continue;
            }

            var checkForEmotes = Code.IsPlayerMessage();
            var builder = new StringBuilder();
            foreach (var word in text.Content.Split(" "))
            {
                if (checkForEmotes && Plugin.Config.ShowEmotes && EmoteCache.Exists(word) && !Plugin.Config.BlockedEmotes.Contains(word))
                {
                    // We add all the previous collected text parts
                    AddContentAfterURLCheck(builder.ToString(), text, chunk);
                    builder.Clear();

                    AddChunkWithMessage(new TextChunk(chunk.Source, EmotePayload.ResolveEmote(word), word));
                    builder.Append(' ');
                    continue;
                }

                builder.Append($"{word} ");
            }

            // We add the leftovers
            // Removing the last whitespace as it is set by us
            AddContentAfterURLCheck(builder.ToString()[..^1], text, chunk);
        }

        return newChunks;
    }

    /// <summary>
    /// URLRegex returns a regex object that matches URLs like:
    /// - https://example.com
    /// - http://example.com
    /// - www.example.com
    /// - https://sub.example.com
    /// - example.com
    /// - sub.example.com
    ///
    /// It matches URLs with www. or https:// prefix, and also matches URLs
    /// without a prefix on specific TLDs.
    /// </summary>
    private static Regex URLRegex = new(
        @"(?<URL>((https?:\/\/|www\.)[a-z0-9-]+(\.[a-z0-9-]+)*|([a-z0-9-]+(\.[a-z0-9-]+)*\.(com|net|org|co|io|app)))(:[\d]{1,5})?(\/[^\s]+)?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture
    );
}
