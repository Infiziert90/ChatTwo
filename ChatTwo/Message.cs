using System.Text;
using ChatTwo.Code;
using ChatTwo.Util;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using System.Text.RegularExpressions;
using Dalamud.Game.Text;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace ChatTwo;

internal class SortCode
{
    internal ChatType Type { get; }
    internal ChatSource Source { get; }

    public SortCode(ChatType type, ChatSource source)
    {
        Type = type;
        Source = source;
    }

    internal SortCode(uint raw)
    {
        Type = (ChatType)(raw >> 16);
        Source = (ChatSource)(raw & 0xFFFF);
    }

    internal uint Encode()
    {
        return ((uint) Type << 16) | (uint) Source;
    }

    private bool Equals(SortCode other)
    {
        return Type == other.Type && Source == other.Source;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
            return false;

        if (ReferenceEquals(this, obj))
            return true;

        return obj.GetType() == GetType() && Equals((SortCode) obj);
    }

    public override int GetHashCode()
    {
        unchecked { return ((int) Type * 397) ^ (int) Source; }
    }
}

internal partial class Message
{
    internal Guid Id { get; } = Guid.NewGuid();
    internal ulong Receiver { get; }
    internal ulong ContentId { get; set; }
    internal ulong AccountId { get; set; } // 0 if not set

    internal DateTimeOffset Date { get; }
    internal ChatCode Code { get; }
    internal List<Chunk> Sender { get; }
    internal List<Chunk> Content { get; private set; }

    internal SeString SenderSource { get; }
    internal SeString ContentSource { get; }

    internal SortCode SortCode { get; }
    internal Guid ExtraChatChannel { get; }

    // Not stored in the database:
    internal int Hash { get; }
    internal Dictionary<Guid, float?> Height { get; } = new();
    internal Dictionary<Guid, bool> IsVisible { get; } = new();

    internal Message(ulong receiver, ulong contentId, ulong accountId, ChatCode code, List<Chunk> sender, List<Chunk> content, SeString senderSource, SeString contentSource)
    {
        var extraChatChannel = ExtractExtraChatChannel(contentSource);
        Receiver = receiver;
        ContentId = contentId;
        AccountId = accountId;
        Date = DateTimeOffset.UtcNow;
        Code = code;
        Sender = sender;
        Content = CheckMessageContent(content, extraChatChannel);
        SenderSource = senderSource;
        ContentSource = contentSource;
        SortCode = new SortCode(Code.Type, Code.Source);
        ExtraChatChannel = extraChatChannel;
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

    internal static Message FakeMessage(List<Chunk> content, ChatCode code)
    {
        return new Message(0, 0, 0, code, [], content, new SeString(), new SeString());
    }

    internal bool Matches(Dictionary<ChatType, ChatSource> channels, bool allExtraChatChannels, HashSet<Guid> extraChatChannels)
    {
        if (ExtraChatChannel != Guid.Empty)
            return allExtraChatChannels || extraChatChannels.Contains(ExtraChatChannel);

        return Code.Type.IsGm()
               || channels.TryGetValue(Code.Type, out var sources)
               && (Code.Source is 0 or (ChatSource) 1
                   || sources.HasFlag(Code.Source));
    }

    private int GenerateHash()
    {
        return SortCode.GetHashCode()
               ^ ExtraChatChannel.GetHashCode()
               ^ string.Join("", Sender.Select(c => c.StringValue())).GetHashCode()
               ^ string.Join("", Content.Select(c => c.StringValue())).GetHashCode();
    }

    private static Guid ExtractExtraChatChannel(SeString contentSource)
    {
        if (contentSource.Payloads.Count > 0 && contentSource.Payloads[0] is RawPayload raw)
        {
            // this does an encode and clone every time it's accessed, so cache
            var data = raw.Data;
            if (data[1] == 0x27 && data[2] == 18 && data[3] == 0x20)
                return new Guid(data[4..^1]);
        }

        return Guid.Empty;
    }

    private List<Chunk> CheckMessageContent(List<Chunk> oldChunks, Guid extraChatChannel)
    {
        var newChunks = new List<Chunk>();
        void AddChunkWithMessage(TextChunk chunk)
        {
            if (string.IsNullOrEmpty(chunk.Content))
                return;

            chunk.Message = this;
            newChunks.Add(chunk);
        }

        var nextIsAutoTranslate = false;
        var checkForEmotes = (Code.IsPlayerMessage() || extraChatChannel != Guid.Empty) && Plugin.Config.ShowEmotes;
        foreach (var chunk in oldChunks)
        {
            // Use as is if it's not a text chunk, it already has a payload, or is auto translate
            if (chunk is not TextChunk text || chunk.Link != null || nextIsAutoTranslate)
            {
                nextIsAutoTranslate = chunk is IconChunk { Icon: BitmapFontIcon.AutoTranslateBegin };

                // No need to call AddChunkWithMessage here since the chunk
                // already has the Message field set.
                newChunks.Add(chunk);
                continue;
            }

            var wordBuilder = new StringBuilder();
            var sentenceBuilder = new StringBuilder();
            foreach (var token in Tokenizer.PrecedenceBasedRegexTokenizer.Tokenize(text.Content))
            {
                if (token.TokenType == Tokenizer.TokenType.StringValue)
                {
                    wordBuilder.Append(token.Value);
                    continue;
                }

                var word = wordBuilder.ToString();
                wordBuilder.Clear();


                var wordUsed = false;
                var tokenUsed = false;

                if (checkForEmotes && EmoteCache.Exists(word) && !Plugin.Config.BlockedEmotes.Contains(word))
                {
                    // Add the previous sentence before adding the emote
                    AddChunkWithMessage(text.NewWithStyle(chunk, sentenceBuilder.ToString()));
                    AddChunkWithMessage(new TextChunk(chunk.Source, EmotePayload.ResolveEmote(word), word) { FallbackColour = text.FallbackColour });

                    wordUsed = true;
                    sentenceBuilder.Clear();
                }

                if (token.TokenType == Tokenizer.TokenType.UrlString)
                {
                    // Add the previous sentence before adding the url
                    AddChunkWithMessage(text.NewWithStyle(chunk.Source, chunk.Link, sentenceBuilder.Append(!wordUsed ? word : "").ToString()));
                    try
                    {
                        AddChunkWithMessage(text.NewWithStyle(chunk.Source, UriPayload.ResolveURI(token.Value), token.Value));
                    }
                    catch (UriFormatException)
                    {
                        AddChunkWithMessage(text.NewWithStyle(chunk.Source, chunk.Link, token.Value));
                        Plugin.Log.Debug($"Invalid URL accepted by Regex but failed URI parsing: '{token.Value}'");
                    }

                    wordUsed = true;
                    tokenUsed = true;
                    sentenceBuilder.Clear();
                }

                // Append match if we haven't reached end of string yet
                if (token.TokenType != Tokenizer.TokenType.SequenceTerminator)
                {
                    sentenceBuilder.Append(!wordUsed ? word : "");
                    sentenceBuilder.Append(!tokenUsed ? token.Value : "");
                    continue;
                }

                // End of string reached, we add our leftover
                AddChunkWithMessage(text.NewWithStyle(chunk, sentenceBuilder.Append(!wordUsed ? word : "").ToString()));
            }
        }

        return newChunks;
    }

    public unsafe void DecodeTextParam()
    {
        var newChunks = new List<Chunk>();
        void AddChunkWithMessage(TextChunk chunk)
        {
            if (string.IsNullOrEmpty(chunk.Content))
                return;

            chunk.Message = this;
            newChunks.Add(chunk);
        }

        foreach (var chunk in Content)
        {
            // Use as is if it's not a text chunk or it already has a payload.
            if (chunk is not TextChunk text || chunk.Link != null)
            {
                // No need to call AddChunkWithMessage here since the chunk
                // already has the Message field set.
                newChunks.Add(chunk);
                continue;
            }

            var splits = TextParamRegex().Split(text.Content);
            if (splits.Length == 1)
            {
                newChunks.Add(chunk);
                continue;
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

                nextIsMatch = false;
                try
                {
                    if (split == "<item>")
                    {
                        var agentChat = AgentChatLog.Instance();
                        var item = agentChat->LinkedItem;

                        if (item.ItemId == 0)
                        {
                            AddChunkWithMessage(text.NewWithStyle(chunk.Source, chunk.Link, split));
                            continue;
                        }

                        var kind = item.ItemId switch
                        {
                            < 500_000 => ItemPayload.ItemKind.Normal,
                            < 1_000_000 => ItemPayload.ItemKind.Collectible,
                            < 2_000_000 => ItemPayload.ItemKind.Hq,
                            _ => ItemPayload.ItemKind.EventItem
                        };

                        var name = kind != ItemPayload.ItemKind.EventItem
                            ? Plugin.DataManager.GetExcelSheet<Item>().GetRow(item.ItemId).Name.ToString()
                            : Plugin.DataManager.GetExcelSheet<EventItem>().GetRow(item.ItemId).Name.ToString();

                        var link = new ItemPayload(item.ItemId, kind, $"{SeIconChar.LinkMarker.ToIconChar()}{name}");
                        AddChunkWithMessage(text.NewWithStyle(chunk.Source, link, link.DisplayName ?? "Unknown"));
                    }
                    else if (split == "<status>")
                    {
                        var statusId = AgentChatLog.Instance()->ContextStatusId;
                        if (statusId == 0 || !Sheets.StatusSheet.TryGetRow(statusId, out var statusRow))
                        {
                            AddChunkWithMessage(text.NewWithStyle(chunk.Source, chunk.Link, split));
                            continue;
                        }

                        var nameValue = statusRow.Name.ToDalamudString().TextValue;
                        var content = statusRow.StatusCategory switch
                        {
                            1 => $"{SeIconChar.Buff.ToIconString()}{nameValue}",
                            2 => $"{SeIconChar.Debuff.ToIconString()}{nameValue}",
                            _ => nameValue
                        };

                        var link = new StatusPayload(statusId);
                        AddChunkWithMessage(text.NewWithStyle(chunk.Source, link, content));
                    }
                    else if (split == "<flag>")
                    {
                        var agentMap = AgentMap.Instance();
                        if (agentMap->IsFlagMarkerSet == 0)
                        {
                            AddChunkWithMessage(text.NewWithStyle(chunk.Source, chunk.Link, split));
                            continue;
                        }

                        var mapCoords = agentMap->FlagMapMarker;
                        var rawX = (int)(MathF.Round(mapCoords.XFloat, 3, MidpointRounding.AwayFromZero) * 1000);
                        var rawY = (int)(MathF.Round(mapCoords.YFloat, 3, MidpointRounding.AwayFromZero) * 1000);

                        var link = new MapLinkPayload(mapCoords.TerritoryId, mapCoords.MapId, rawX, rawY);
                        AddChunkWithMessage(text.NewWithStyle(chunk.Source, link, $"{SeIconChar.LinkMarker.ToIconChar()}{link.PlaceName} {link.CoordinateString}"));
                    }

                }
                catch (Exception)
                {
                    AddChunkWithMessage(text.NewWithStyle(chunk.Source, chunk.Link, split));
                    Plugin.Log.Debug($"Failed to parse the text param: '{split}'");
                }
            }
        }

        Content = newChunks;
    }

    [GeneratedRegex("(<item>|<flag>|<status>)")]
    private static partial Regex TextParamRegex();
}
