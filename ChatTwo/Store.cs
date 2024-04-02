using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using ChatTwo.Code;
using ChatTwo.Resources;
using ChatTwo.Util;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using ImGuiNET;
using LiteDB;
using Lumina.Excel.GeneratedSheets;

namespace ChatTwo;

internal class Store : IDisposable {
    internal const int MessagesLimit = 10_000;

    private Plugin Plugin { get; }

    private ConcurrentQueue<(uint, Message)> Pending { get; } = new();
    private Stopwatch CheckpointTimer { get; } = new();
    internal ILiteDatabase Database { get; private set; }
    private ILiteCollection<Message> Messages => this.Database.GetCollection<Message>("messages");

    private Dictionary<ChatType, NameFormatting> Formats { get; } = new();
    private ulong LastContentId { get; set; }

    private ulong CurrentContentId {
        get {
            var contentId = Plugin.ClientState.LocalContentId;
            return contentId == 0 ? this.LastContentId : contentId;
        }
    }

    internal Store(Plugin plugin) {
        this.Plugin = plugin;
        this.CheckpointTimer.Start();

        BsonMapper.Global = new BsonMapper {
            IncludeNonPublic = true,
            TrimWhitespace = false,
            // EnumAsInteger = true,
        };

        if (this.Plugin.Config.DatabaseMigration == 0) {
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
        } else {
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
            payload => {
                switch (payload) {
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
                }

                return payload?.Encode();
            },
            bson => {
                if (bson.IsNull) {
                    return null;
                }

                if (bson.IsDocument) {
                    return bson["Type"].AsString switch {
                        "Achievement" => new AchievementPayload((uint) bson["Id"].AsInt64),
                        "PartyFinder" => new PartyFinderPayload((uint) bson["Id"].AsInt64),
                        _ => null,
                    };
                }

                return Payload.Decode(new BinaryReader(new MemoryStream(bson.AsBinary)));
            });
        BsonMapper.Global.RegisterType<SeString?>(
            seString => seString == null
                ? null
                : new BsonArray(seString.Payloads.Select(payload => new BsonValue(payload.Encode()))),
            bson => {
                if (bson.IsNull) {
                    return null;
                }

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
        this.Database = this.Connect();
        this.Messages.EnsureIndex(msg => msg.Date);
        this.Messages.EnsureIndex(msg => msg.SortCode);
        this.Messages.EnsureIndex(msg => msg.ExtraChatChannel);

        this.MigrateWrapper();

        Plugin.ChatGui.ChatMessageUnhandled += this.ChatMessage;
        Plugin.Framework.Update += this.GetMessageInfo;
        Plugin.Framework.Update += this.UpdateReceiver;
        Plugin.ClientState.Logout += this.Logout;
    }

    public void Dispose() {
        Plugin.ClientState.Logout -= this.Logout;
        Plugin.Framework.Update -= this.UpdateReceiver;
        Plugin.Framework.Update -= this.GetMessageInfo;
        Plugin.ChatGui.ChatMessageUnhandled -= this.ChatMessage;

        this.Database.Dispose();
    }

    private ILiteDatabase Connect() {
        var dir = Plugin.Interface.ConfigDirectory;
        dir.Create();

        var dbPath = Path.Join(dir.FullName, "chat.db");
        var connection = this.Plugin.Config.SharedMode ? "shared" : "direct";
        var connString = $"Filename='{dbPath}';Connection={connection}";
        return new LiteDatabase(connString, BsonMapper.Global) {
            CheckpointSize = 1_000,
            Timeout = TimeSpan.FromSeconds(1),
        };
    }

    internal void Reconnect() {
        this.Database.Dispose();
        this.Database = this.Connect();
    }

    private void Logout() {
        this.LastContentId = 0;
    }

    private void UpdateReceiver(IFramework framework) {
        var contentId = Plugin.ClientState.LocalContentId;
        if (contentId != 0) {
            this.LastContentId = contentId;
        }
    }

    private void GetMessageInfo(IFramework framework) {
        if (this.CheckpointTimer.Elapsed > TimeSpan.FromMinutes(5)) {
            this.CheckpointTimer.Restart();
            new Thread(() => this.Database.Checkpoint()).Start();
        }

        if (!this.Pending.TryDequeue(out var entry)) {
            return;
        }

        var contentId = this.Plugin.Functions.Chat.GetContentIdForEntry(entry.Item1);
        entry.Item2.ContentId = contentId ?? 0;
        if (this.Plugin.Config.DatabaseBattleMessages || !entry.Item2.Code.IsBattle()) {
            this.Messages.Update(entry.Item2);
        }
    }

    private long _migrateCurrent;
    private long _migrateMax;

    private void MigrateDraw() {
        ImGui.SetNextWindowSizeConstraints(new Vector2(450, 0), new Vector2(450, float.MaxValue));
        if (!ImGui.Begin($"{Plugin.PluginName}##migration-window", ImGuiWindowFlags.AlwaysAutoResize)) {
            ImGui.End();
            return;
        }

        ImGui.PushTextWrapPos();
        ImGui.TextUnformatted(string.Format(Language.Migration_Line1, Plugin.PluginName));
        ImGui.TextUnformatted(string.Format(Language.Migration_Line2, Plugin.PluginName));
        ImGui.TextUnformatted(Language.Migration_Line3);
        ImGui.TextUnformatted(Language.Migration_Line4);
        ImGui.PopTextWrapPos();

        ImGui.Spacing();
        ImGui.ProgressBar((float) this._migrateCurrent / this._migrateMax, new Vector2(-1, 0), $"{this._migrateCurrent} / {this._migrateMax}");

        ImGui.End();
    }

    internal void MigrateWrapper() {
        if (this.Plugin.Config.DatabaseMigration < Configuration.LatestDbVersion) {
            Plugin.Interface.UiBuilder.Draw += this.MigrateDraw;
        }

        try {
            this.Migrate();
        } finally {
            Plugin.Interface.UiBuilder.Draw -= this.MigrateDraw;
        }
    }

    internal void Migrate() {
        // re-save all messages, which will add the ExtraChat channel
        if (this.Plugin.Config.DatabaseMigration == 0) {
            var total = (float) this.Messages.LongCount() / 10_000.0;
            var rounds = (long) Math.Ceiling(total);
            this._migrateMax = rounds;

            var lastId = ObjectId.Empty;
            for (var i = 0; i < rounds; i++) {
                this._migrateCurrent = i + 1;
                Plugin.Log.Info($"Update round {i + 1}/{rounds}");
                var messages = this.Messages.Query()
                    .OrderBy(msg => msg.Id)
                    .Where(msg => msg.Id > lastId)
                    .Limit(10_000)
                    .ToArray();

                foreach (var message in messages) {
                    this.Messages.Update(message);
                    lastId = message.Id;
                }
            }

            this.Database.Checkpoint();

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

            this.Plugin.Config.DatabaseMigration = 1;
            this.Plugin.SaveConfig();
        }
    }

    internal void AddMessage(Message message, Tab? currentTab) {
        if (this.Plugin.Config.DatabaseBattleMessages || !message.Code.IsBattle()) {
            this.Messages.Insert(message);
        }

        var currentMatches = currentTab?.Matches(message) ?? false;

        foreach (var tab in this.Plugin.Config.Tabs) {
            var unread = !(tab.UnreadMode == UnreadMode.Unseen && currentTab != tab && currentMatches);

            if (tab.Matches(message)) {
                tab.AddMessage(message, unread);
            }
        }
    }

    internal void FilterAllTabs(bool unread = true) {
        foreach (var tab in this.Plugin.Config.Tabs) {
            this.FilterTab(tab, unread);
        }
    }

    internal void FilterTab(Tab tab, bool unread) {
        var sortCodes = new List<SortCode>();
        foreach (var (type, sources) in tab.ChatCodes) {
            sortCodes.Add(new SortCode(type, 0));
            sortCodes.Add(new SortCode(type, (ChatSource) 1));

            if (type.HasSource()) {
                foreach (var source in Enum.GetValues<ChatSource>()) {
                    if (sources.HasFlag(source)) {
                        sortCodes.Add(new SortCode(type, source));
                    }
                }
            }
        }

        var query = this.Messages
            .Query()
            .OrderByDescending(msg => msg.Date)
            .Where(msg => sortCodes.Contains(msg.SortCode) || msg.ExtraChatChannel != Guid.Empty)
            .Where(msg => msg.Receiver == this.CurrentContentId);
        if (!this.Plugin.Config.FilterIncludePreviousSessions) {
            query = query.Where(msg => msg.Date >= this.Plugin.GameStarted);
        }

        var messages = query
            .Limit(MessagesLimit)
            .ToEnumerable()
            .Reverse();
        foreach (var message in messages) {
            // redundant matches check for extrachat
            if (tab.Matches(message)) {
                tab.AddMessage(message, unread);
            }
        }
    }

    private void ChatMessage(XivChatType type, uint senderId, SeString sender, SeString message) {
        var chatCode = new ChatCode((ushort) type);

        NameFormatting? formatting = null;
        if (sender.Payloads.Count > 0) {
            formatting = this.FormatFor(chatCode.Type);
        }

        var senderChunks = new List<Chunk>();
        if (formatting is { IsPresent: true }) {
            senderChunks.Add(new TextChunk(ChunkSource.None, null, formatting.Before) {
                FallbackColour = chatCode.Type,
            });
            senderChunks.AddRange(ChunkUtil.ToChunks(sender, ChunkSource.Sender, chatCode.Type));
            senderChunks.Add(new TextChunk(ChunkSource.None, null, formatting.After) {
                FallbackColour = chatCode.Type,
            });
        }

        var messageChunks = ChunkUtil.ToChunks(message, ChunkSource.Content, chatCode.Type).ToList();

        var msg = new Message(this.CurrentContentId, chatCode, senderChunks, messageChunks, sender, message);
        this.AddMessage(msg, this.Plugin.Ui.CurrentTab ?? null);

        var idx = this.Plugin.Functions.GetCurrentChatLogEntryIndex();
        if (idx != null) {
            this.Pending.Enqueue((idx.Value - 1, msg));
        }
    }

    internal class NameFormatting {
        internal string Before { get; private set; } = string.Empty;
        internal string After { get; private set; } = string.Empty;
        internal bool IsPresent { get; private set; } = true;

        internal static NameFormatting Empty() {
            return new() {
                IsPresent = false,
            };
        }

        internal static NameFormatting Of(string before, string after) {
            return new() {
                Before = before,
                After = after,
            };
        }
    }

    private NameFormatting? FormatFor(ChatType type) {
        if (this.Formats.TryGetValue(type, out var cached)) {
            return cached;
        }

        var logKind = Plugin.DataManager.GetExcelSheet<LogKind>()!.GetRow((ushort) type);

        if (logKind == null) {
            return null;
        }

        var format = (SeString) logKind.Format;

        static bool IsStringParam(Payload payload, byte num) {
            var data = payload.Encode();

            return data.Length >= 5 && data[1] == 0x29 && data[4] == num + 1;
        }

        var firstStringParam = format.Payloads.FindIndex(payload => IsStringParam(payload, 1));
        var secondStringParam = format.Payloads.FindIndex(payload => IsStringParam(payload, 2));

        if (firstStringParam == -1 || secondStringParam == -1) {
            return NameFormatting.Empty();
        }

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

        this.Formats[type] = nameFormatting;

        return nameFormatting;
    }
}
