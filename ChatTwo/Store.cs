using System.Collections.Concurrent;
using System.Diagnostics;
using ChatTwo.Code;
using ChatTwo.Util;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using LiteDB;
using Lumina.Excel.GeneratedSheets;

namespace ChatTwo;

internal class Store : IDisposable
{
    internal const int MessagesLimit = 10_000;

    private Plugin Plugin { get; }

    private ConcurrentQueue<(uint, Message)> Pending { get; } = new();
    private Stopwatch CheckpointTimer { get; } = new();
    internal ILiteDatabase Database { get; private set; }
    private ILiteCollection<Message> Messages => Database.GetCollection<Message>("messages");

    private Dictionary<ChatType, NameFormatting> Formats { get; } = new();
    private ulong LastContentId { get; set; }

    private ulong CurrentContentId
    {
        get
        {
            var contentId = Plugin.ClientState.LocalContentId;
            return contentId == 0 ? LastContentId : contentId;
        }
    }

    internal Store(Plugin plugin)
    {
        Plugin = plugin;
        CheckpointTimer.Start();

        BsonMapper.Global = new BsonMapper
        {
            IncludeNonPublic = true,
            TrimWhitespace = false,
            // EnumAsInteger = true,
        };

        if (Plugin.Config.DatabaseMigration == 0)
        {
            BsonMapper.Global.Entity<Message>()
                .Id(msg => msg.Id)
                .Ctor(doc => new Message(
                    doc["_id"].AsObjectId,
                    (ulong) doc["Receiver"].AsInt64,
                    (ulong) doc["ContentId"].AsInt64,
                    DateTime.UnixEpoch.AddMilliseconds(doc["Date"].AsInt64),
                    doc["Code"].AsDocument,
                    doc["Sender"].AsArray,
                    doc["Content"].AsArray,
                    doc["SenderSource"],
                    doc["ContentSource"],
                    doc["SortCode"].AsDocument
                ));
        }
        else
        {
            BsonMapper.Global.Entity<Message>()
                .Id(msg => msg.Id)
                .Ctor(doc => new Message(
                    doc["_id"].AsObjectId,
                    (ulong) doc["Receiver"].AsInt64,
                    (ulong) doc["ContentId"].AsInt64,
                    DateTime.UnixEpoch.AddMilliseconds(doc["Date"].AsInt64),
                    doc["Code"].AsDocument,
                    doc["Sender"].AsArray,
                    doc["Content"].AsArray,
                    doc["SenderSource"],
                    doc["ContentSource"],
                    doc["SortCode"].AsDocument,
                    doc["ExtraChatChannel"]
                ));
        }

        BsonMapper.Global.RegisterType<Payload?>(
            payload =>
            {
                switch (payload)
                {
                    case AchievementPayload achievement:
                        return new BsonDocument(new Dictionary<string, BsonValue> {
                            ["Type"] = new("Achievement"),
                            ["Id"] = new(achievement.Id),
                        });
                    case PartyFinderPayload partyFinder:
                        return new BsonDocument(new Dictionary<string, BsonValue> {
                            ["Type"] = new("PartyFinder"),
                            ["Id"] = new(partyFinder.Id),
                        });
                    case URIPayload uri:
                        return new BsonDocument(new Dictionary<string, BsonValue> {
                            ["Type"] = new("URI"),
                            ["Uri"] = new(uri.Uri.ToString()),
                        });
                }

                return payload?.Encode();
            },
            bson =>
            {
                if (bson.IsNull)
                    return null;

                if (bson.IsDocument)
                {
                    return bson["Type"].AsString switch
                    {
                        "Achievement" => new AchievementPayload((uint) bson["Id"].AsInt64),
                        "PartyFinder" => new PartyFinderPayload((uint) bson["Id"].AsInt64),
                        "URI" => new URIPayload(new Uri(bson["Uri"].AsString)),
                        _ => null,
                    };
                }

                return Payload.Decode(new BinaryReader(new MemoryStream(bson.AsBinary)));
            });
        BsonMapper.Global.RegisterType<SeString?>(
            seString => seString == null
                ? null
                : new BsonArray(seString.Payloads.Select(payload => new BsonValue(payload.Encode()))),
            bson =>
            {
                if (bson.IsNull)
                    return null;

                var array = bson.IsArray ? bson.AsArray : bson["Payloads"].AsArray;
                var payloads = array
                    .Select(payload => Payload.Decode(new BinaryReader(new MemoryStream(payload.AsBinary))))
                    .ToList();
                return new SeString(payloads);
            }
        );
        BsonMapper.Global.RegisterType(
            type => (int) type,
            bson => (ChatType) bson.AsInt32
        );
        BsonMapper.Global.RegisterType(
            source => (int) source,
            bson => (ChatSource) bson.AsInt32
        );
        BsonMapper.Global.RegisterType(
            dateTime => dateTime.Subtract(DateTime.UnixEpoch).TotalMilliseconds,
            bson => DateTime.UnixEpoch.AddMilliseconds(bson.AsInt64)
        );
        Database = Connect();

        Plugin.ChatGui.ChatMessageUnhandled += ChatMessage;
        Plugin.Framework.Update += GetMessageInfo;
        Plugin.Framework.Update += UpdateReceiver;
        Plugin.ClientState.Logout += Logout;
    }

    public void Dispose() {
        Plugin.ClientState.Logout -= Logout;
        Plugin.Framework.Update -= UpdateReceiver;
        Plugin.Framework.Update -= GetMessageInfo;
        Plugin.ChatGui.ChatMessageUnhandled -= ChatMessage;

        Database.Dispose();
    }

    internal static string DatabasePath()
    {
        var dir = Plugin.Interface.ConfigDirectory;
        dir.Create();
        return Path.Join(dir.FullName, "chat.db");
    }

    private LiteDatabase Connect() {
        var dbPath = DatabasePath();
        var connection = Plugin.Config.SharedMode ? "shared" : "direct";
        var connString = $"Filename='{dbPath}';Connection={connection}";
        var conn = new LiteDatabase(connString, BsonMapper.Global)
        {
            CheckpointSize = 1_000,
            Timeout = TimeSpan.FromSeconds(1),
        };
        var messages = conn.GetCollection<Message>("messages");
        messages.EnsureIndex(msg => msg.Date);
        messages.EnsureIndex(msg => msg.SortCode);
        messages.EnsureIndex(msg => msg.ExtraChatChannel);
        return conn;
    }

    internal void Reconnect()
    {
        Database.Dispose();
        Database = Connect();
    }

    internal void ClearDatabase()
    {
        Messages.DeleteAll();
        Database.Rebuild();
    }

    internal static long DatabaseSize()
    {
        var dbPath = DatabasePath();
        return !File.Exists(dbPath) ? 0 : new FileInfo(dbPath).Length;
    }

    internal static long DatabaseLogSize()
    {
        var dbLogPath = Path.Join(Plugin.Interface.ConfigDirectory.FullName, "chat-log.db");
        return !File.Exists(dbLogPath) ? 0 : new FileInfo(dbLogPath).Length;
    }

    internal int MessageCount() => Messages.Count();

    private void Logout()
    {
        LastContentId = 0;
    }

    private void UpdateReceiver(IFramework framework)
    {
        var contentId = Plugin.ClientState.LocalContentId;
        if (contentId != 0)
            LastContentId = contentId;
    }

    private void GetMessageInfo(IFramework framework)
    {
        if (CheckpointTimer.Elapsed > TimeSpan.FromMinutes(5))
        {
            CheckpointTimer.Restart();
            new Thread(() => Database.Checkpoint()).Start();
        }

        if (!Pending.TryDequeue(out var entry))
            return;

        var contentId = Plugin.Functions.Chat.GetContentIdForEntry(entry.Item1);
        entry.Item2.ContentId = contentId ?? 0;
        if (Plugin.Config.DatabaseBattleMessages || !entry.Item2.Code.IsBattle())
            Messages.Update(entry.Item2);
    }

    internal void AddMessage(Message message, Tab? currentTab)
    {
        if (Plugin.Config.DatabaseBattleMessages || !message.Code.IsBattle())
            Messages.Insert(message);

        var currentMatches = currentTab?.Matches(message) ?? false;

        foreach (var tab in Plugin.Config.Tabs)
        {
            var unread = !(tab.UnreadMode == UnreadMode.Unseen && currentTab != tab && currentMatches);

            if (tab.Matches(message))
                tab.AddMessage(message, unread);
        }
    }

    internal void FilterAllTabs(bool unread = true)
    {
        foreach (var tab in Plugin.Config.Tabs)
            FilterTab(tab, unread);
    }

    internal void FilterTab(Tab tab, bool unread)
    {
        var sortCodes = new List<SortCode>();
        foreach (var (type, sources) in tab.ChatCodes)
        {
            sortCodes.Add(new SortCode(type, 0));
            sortCodes.Add(new SortCode(type, (ChatSource) 1));

            if (!type.HasSource())
                continue;

            foreach (var source in Enum.GetValues<ChatSource>())
                if (sources.HasFlag(source))
                    sortCodes.Add(new SortCode(type, source));
        }

        var query = Messages
            .Query()
            .OrderByDescending(msg => msg.Date)
            .Where(msg => sortCodes.Contains(msg.SortCode) || msg.ExtraChatChannel != Guid.Empty)
            .Where(msg => msg.Receiver == CurrentContentId);

        if (!Plugin.Config.FilterIncludePreviousSessions)
            query = query.Where(msg => msg.Date >= Plugin.GameStarted);

        var messages = query.Limit(MessagesLimit).ToEnumerable().Reverse();

        foreach (var message in messages)
        {
            // check primarily for startup double posting messages
            if (tab.Contains(message))
                continue;

            // redundant matches check for extrachat
            if (tab.Matches(message))
                tab.AddMessage(message, unread);
        }
    }

    public (SeString? Sender, SeString? Message) LastMessage = (null, null);
    private void ChatMessage(XivChatType type, uint senderId, SeString sender, SeString message)
    {
        var chatCode = new ChatCode((ushort) type);

        NameFormatting? formatting = null;
        if (sender.Payloads.Count > 0)
            formatting = FormatFor(chatCode.Type);

        LastMessage = (sender, message);
        var senderChunks = new List<Chunk>();
        if (formatting is { IsPresent: true })
        {
            senderChunks.Add(new TextChunk(ChunkSource.None, null, formatting.Before)
            {
                FallbackColour = chatCode.Type,
            });
            senderChunks.AddRange(ChunkUtil.ToChunks(sender, ChunkSource.Sender, chatCode.Type));
            senderChunks.Add(new TextChunk(ChunkSource.None, null, formatting.After)
            {
                FallbackColour = chatCode.Type,
            });
        }

        var messageChunks = ChunkUtil.ToChunks(message, ChunkSource.Content, chatCode.Type).ToList();

        Plugin.Log.Information($"Adding Message with code {chatCode} timestamp {senderId} content {message.TextValue}");
        var msg = new Message(CurrentContentId, chatCode, senderChunks, messageChunks, sender, message);
        AddMessage(msg, Plugin.ChatLogWindow.CurrentTab ?? null);

        var idx = Plugin.Functions.GetCurrentChatLogEntryIndex();
        if (idx != null)
            Pending.Enqueue((idx.Value - 1, msg));
    }

    internal class NameFormatting
    {
        internal string Before { get; private set; } = string.Empty;
        internal string After { get; private set; } = string.Empty;
        internal bool IsPresent { get; private set; } = true;

        internal static NameFormatting Empty()
        {
            return new NameFormatting { IsPresent = false, };
        }

        internal static NameFormatting Of(string before, string after)
        {
            return new NameFormatting
            {
                Before = before,
                After = after,
            };
        }
    }

    private NameFormatting? FormatFor(ChatType type)
    {
        if (Formats.TryGetValue(type, out var cached))
            return cached;

        var logKind = Plugin.DataManager.GetExcelSheet<LogKind>()!.GetRow((ushort) type);
        if (logKind == null)
            return null;

        var format = (SeString) logKind.Format;
        static bool IsStringParam(Payload payload, byte num)
        {
            var data = payload.Encode();
            return data.Length >= 5 && data[1] == 0x29 && data[4] == num + 1;
        }

        var firstStringParam = format.Payloads.FindIndex(payload => IsStringParam(payload, 1));
        var secondStringParam = format.Payloads.FindIndex(payload => IsStringParam(payload, 2));

        if (firstStringParam == -1 || secondStringParam == -1)
            return NameFormatting.Empty();

        var before = format.Payloads
            .GetRange(0, firstStringParam)
            .Where(payload => payload is ITextProvider)
            .Cast<ITextProvider>()
            .Select(text => text.Text);
        var after = format.Payloads
            .GetRange(firstStringParam + 1, secondStringParam - firstStringParam)
            .Where(payload => payload is ITextProvider)
            .Cast<ITextProvider>()
            .Select(text => text.Text);

        var nameFormatting = NameFormatting.Of(
            string.Join("", before),
            string.Join("", after)
        );

        Formats[type] = nameFormatting;

        return nameFormatting;
    }
}
