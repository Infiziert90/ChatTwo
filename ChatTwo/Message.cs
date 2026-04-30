using System.Text;
using ChatTwo.Code;
using ChatTwo.Util;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using System.Text.RegularExpressions;
using Dalamud.Game.Text;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace ChatTwo;

public partial class Message
{
    public Guid Id { get; } = Guid.NewGuid();
    public ulong Receiver { get; }
    public ulong ContentId { get; set; }
    public ulong AccountId { get; set; } // 0 if not set

    public DateTimeOffset Date { get; }
    public ChatCode Code { get; }
    public List<Chunk> Sender { get; }
    public List<Chunk> Content { get; private set; }

    public SeString SenderSource { get; }
    public SeString ContentSource { get; }

    public int SortCodeV2 { get; }
    public Guid ExtraChatChannel { get; }

    // Not stored in the database:
    public int Hash { get; }
    public Dictionary<Guid, float?> Height { get; } = new();
    public Dictionary<Guid, bool> IsVisible { get; } = new();

    public Message(ulong receiver, ulong contentId, ulong accountId, ChatCode code, List<Chunk> sender, List<Chunk> content, SeString senderSource, SeString contentSource)
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
        SortCodeV2 = Code.ToSortCodeV2();
        ExtraChatChannel = extraChatChannel;
        Hash = GenerateHash();

        foreach (var chunk in sender.Concat(content))
            chunk.Message = this;
    }

    public Message(Guid id, ulong receiver, ulong contentId, DateTimeOffset date, ChatCode code, List<Chunk> sender, List<Chunk> content, SeString senderSource, SeString contentSource, Guid extraChatChannel)
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
        SortCodeV2 = code.ToSortCodeV2();
        ExtraChatChannel = extraChatChannel;
        Hash = GenerateHash();

        foreach (var chunk in sender.Concat(content))
            chunk.Message = this;
    }

    public static Message FakeMessage(List<Chunk> content, ChatCode code)
    {
        return new Message(0, 0, 0, code, [], content, new SeString(), new SeString());
    }

    public bool Matches(Dictionary<ChatType, (ChatSource Source, ChatSource Target)> channels, bool allExtraChatChannels, HashSet<Guid> extraChatChannels)
    {
        if (ExtraChatChannel != Guid.Empty)
            return allExtraChatChannels || extraChatChannels.Contains(ExtraChatChannel);

        var source = (ChatSource)(1 << (int)Code.Source);
        var target = (ChatSource)(1 << (int)Code.Target);
        return Code.Type.IsGm()
               || channels.TryGetValue(Code.Type, out var sources)
               && (Code.Source is 0 || sources.Source.HasFlag(source) || sources.Target.HasFlag(target));
    }

    private int GenerateHash()
    {
        var hash = SortCodeV2.GetHashCode()
                   ^ ExtraChatChannel.GetHashCode()
                   ^ string.Join("", Sender.Select(c => c.StringValue())).GetHashCode()
                   ^ string.Join("", Content.Select(c => c.StringValue())).GetHashCode();

        if (Plugin.Config.CollapseKeepUniqueLinks)
        {
            // Hash the link too for something like DeathRecap where the message is the same
            // but the link is different
            hash ^= string.Join("", Content.Select(c => c.Link?.GetHashCode())).GetHashCode();
        }

        return hash;
    }

    private static Guid ExtractExtraChatChannel(SeString contentSource)
    {
        if (contentSource.Payloads.Count > 0 && contentSource.Payloads[0] is RawPayload raw)
        {
            // this does an encode and clone every time it's accessed, so cache
            var data = raw.Data;
            try
            {
                if (data[1] == 0x27 && data[2] == 18 && data[3] == 0x20)
                    return new Guid(data[4..^1]);
            }
            catch (ArgumentException ex)
            {
                Plugin.Log.Error(ex, "Failed to parse extra chat channel GUID");
                Plugin.Log.Error($"Byte Array: ${string.Join(", ", data[4..^1])}");
                return Guid.Empty;
            }
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
                        AddChunkWithMessage(text.NewWithStyle(chunk.Source, UriPayload.ResolveUri(token.Value), token.Value));
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
                            < 500_000 => ItemKind.Normal,
                            < 1_000_000 => ItemKind.Collectible,
                            < 2_000_000 => ItemKind.Hq,
                            _ => ItemKind.EventItem
                        };

                        var name = kind != ItemKind.EventItem
                            ? Sheets.ItemSheet.GetRow(item.ItemId).Name.ToString()
                            : Sheets.EventItemSheet.GetRow(item.ItemId).Name.ToString();

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

                        var nameValue = statusRow.Name.ToString();
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
                        if (agentMap->FlagMarkerCount == 0)
                        {
                            AddChunkWithMessage(text.NewWithStyle(chunk.Source, chunk.Link, split));
                            continue;
                        }

                        var mapCoords = agentMap->FlagMapMarkers[0];
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
