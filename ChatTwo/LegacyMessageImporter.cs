using System.Diagnostics;
using ChatTwo.Code;
using ChatTwo.Util;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using LiteDB;

namespace ChatTwo;

internal enum LegacyMessageImporterEligibilityStatus
{
    Eligible,
    IneligibleOriginalDbNotExists,
    IneligibleMigrationDbExists,
    IneligibleLiteDbFailed,
    IneligibleNoMessages,
}

internal class LegacyMessageImporterEligibility
{
    internal LegacyMessageImporterEligibilityStatus Status { get; private set; }
    internal string AdditionalIneligibilityInfo { get; private set; }

    internal string OriginalDbPath { get; }
    internal string MigrationDbPath { get; }

    internal long DatabaseSizeBytes { get; }
    internal int MessageCount { get; }

    private LegacyMessageImporterEligibility(LegacyMessageImporterEligibilityStatus status, string additionalIneligibilityInfo, string originalDbPath, string migrationDbPath, long databaseSizeBytes, int messageCount)
    {
        Status = status;
        AdditionalIneligibilityInfo = additionalIneligibilityInfo;
        OriginalDbPath = originalDbPath;
        MigrationDbPath = migrationDbPath;
        DatabaseSizeBytes = databaseSizeBytes;
        MessageCount = messageCount;
    }

    private static LegacyMessageImporterEligibility NewEligible(string originalDbPath, string migrationDbPath,
        long databaseSizeBytes, int messageCount)
    {
        return new LegacyMessageImporterEligibility(LegacyMessageImporterEligibilityStatus.Eligible, "", originalDbPath, migrationDbPath, databaseSizeBytes, messageCount);
    }

    private static LegacyMessageImporterEligibility NewIneligible(LegacyMessageImporterEligibilityStatus status, string additionalIneligibilityReason)
    {
        return new LegacyMessageImporterEligibility(status, additionalIneligibilityReason, "", "", 0, 0);
    }

    internal static LegacyMessageImporterEligibility CheckEligibility(string? originalDbPath = null, string? migrationDbPath = null)
    {
        originalDbPath ??= Path.Join(Plugin.Interface.ConfigDirectory.FullName, "chat.db");
        migrationDbPath ??= Path.Join(Plugin.Interface.ConfigDirectory.FullName, "chat-litedb.db");

        // Condition 1: the database file must exist in its original path.
        if (!File.Exists(originalDbPath))
        {
            return NewIneligible(LegacyMessageImporterEligibilityStatus.IneligibleOriginalDbNotExists, $"Original database file '{originalDbPath}' does not exist");
        }

        // Condition 2: the migration file must not exist.
        if (File.Exists(migrationDbPath))
        {
            return NewIneligible(LegacyMessageImporterEligibilityStatus.IneligibleMigrationDbExists, $"Migration database file '{migrationDbPath}' already exists, migration was already started in the past");
        }

        // Condition 3: we need to be able to connect to the original database
        // path.
        try
        {
            using var db = LegacyMessageImporter.Connect(originalDbPath);
            var size = new FileInfo(originalDbPath).Length;
            var count = db.GetCollection(LegacyMessageImporter.MessagesCollection).Count();
            if (count <= 0)
                NewIneligible(LegacyMessageImporterEligibilityStatus.IneligibleNoMessages, $"No messages in original database file '{originalDbPath}'");
            return NewEligible(originalDbPath, migrationDbPath, size, count);
        }
        catch (Exception e)
        {
            // Notify the user about this error, because they might be wondering
            // why they weren't offered a migration.
            return NewIneligible(LegacyMessageImporterEligibilityStatus.IneligibleLiteDbFailed, $"LiteDB connection to original database file '{originalDbPath}' failed: {e}");
        }
    }

    internal LegacyMessageImporter StartImport(MessageStore targetStore, bool noLog = false, Plugin? plugin = null)
    {
        if (Status != LegacyMessageImporterEligibilityStatus.Eligible)
            throw new InvalidOperationException($"Migration not eligible: status is {Status}");

        return new LegacyMessageImporter(targetStore, originalDbPath: OriginalDbPath, migrationDbPath: MigrationDbPath, noLog: noLog, plugin);
    }

    /// <summary>
    /// Makes the migration ineligible so the user won't be asked again.
    /// </summary>
    internal bool RenameOldDatabase()
    {
        try
        {
            File.Move(OriginalDbPath, MigrationDbPath);
            Status = LegacyMessageImporterEligibilityStatus.IneligibleMigrationDbExists;
            AdditionalIneligibilityInfo = "User chose to rename the old database file";
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Unable to move the old database");
            return false;
        }
    }
}

internal class LegacyMessageImporter : IAsyncDisposable
{
    private readonly Plugin? Plugin;

    private readonly CancellationTokenSource CancellationToken = new();
    private Thread? WorkingThread = null;

    internal const string MessagesCollection = "messages";
    private const int MaxFailedMessageLogs = 10;

    private readonly MessageStore _targetStore;
    private readonly IPluginLog? _log;

    private LiteDatabase? _database;

    internal long ImportStart { get; } // ticks
    internal int ImportCount { get; private set; }
    internal int SuccessfulMessages { get; private set; }
    internal int FailedMessages { get; private set; }
    internal int ProcessedMessages => SuccessfulMessages + FailedMessages;
    internal int RemainingMessages => ImportCount - ProcessedMessages;
    // Progress from 0 to 1.
    internal float Progress => ImportCount > 0 ? ProcessedMessages / (float)ImportCount : 1;
    // Message count processed in the last second.
    internal float CurrentMessageRate { get; private set; }
    // ETA based on CurrentMessageRate.
    internal TimeSpan EstimatedTimeRemaining => TimeSpan.FromSeconds(CurrentMessageRate > 0 ? (ImportCount - SuccessfulMessages - FailedMessages) / CurrentMessageRate : 0);
    internal long? ImportComplete { get; private set; } // ticks

    // This can be set by the user to limit the rate at which messages are
    // imported. If the rate exceeds this value, the importer will sleep for the
    // remainder of the second.
    internal int MaxMessageRate = 250; // start low

    // Do not call this directly, use
    // LegacyMessageImporterEligibility.StartImport instead.
    internal LegacyMessageImporter(MessageStore targetStore, string? originalDbPath = null, string? migrationDbPath = null, bool noLog = false, Plugin? plugin = null)
    {
        _targetStore = targetStore;
        originalDbPath ??= Path.Join(Plugin.Interface.ConfigDirectory.FullName, "chat.db");
        migrationDbPath ??= migrationDbPath ?? Path.Join(Plugin.Interface.ConfigDirectory.FullName, "chat-litedb.db");
        _log = noLog ? null : Plugin.Log;
        Plugin = plugin;

        _log?.Info($"[Migration] Moving '{originalDbPath}' to '{migrationDbPath}'");
        File.Move(originalDbPath, migrationDbPath);
        _log?.Info($"[Migration] Opening '{migrationDbPath}'");
        _database = Connect(migrationDbPath);

        ImportStart = Environment.TickCount64;
        WorkingThread = new Thread(() => DoImport(CancellationToken.Token));
        WorkingThread.Start();
    }

    public async ValueTask DisposeAsync()
    {
        await CancellationToken.CancelAsync();

        var timeout = 10_000; // 10s
        while (WorkingThread != null && timeout > 0)
        {
            if (!WorkingThread.IsAlive)
                break;

            timeout -= 100;
            await Task.Delay(100);
            Plugin.Log.Information("Sleeping because thread still alive");
        }

        _database?.Dispose();
    }

    internal static LiteDatabase Connect(string dbPath, bool readOnly = true)
    {
        BsonMapper.Global = new BsonMapper
        {
            IncludeNonPublic = true,
            TrimWhitespace = false
        };

        BsonMapper.Global.RegisterType<Payload?>(
            payload =>
            {
                switch (payload)
                {
                    case AchievementPayload achievement:
                        return new BsonDocument(new Dictionary<string, BsonValue>
                        {
                            ["Type"] = new("Achievement"),
                            ["Id"] = new(achievement.Id)
                        });
                    case PartyFinderPayload partyFinder:
                        return new BsonDocument(new Dictionary<string, BsonValue>
                        {
                            ["Type"] = new("PartyFinder"),
                            ["Id"] = new(partyFinder.Id)
                        });
                    case UriPayload uri:
                        return new BsonDocument(new Dictionary<string, BsonValue>
                        {
                            ["Type"] = new("URI"),
                            ["Uri"] = new(uri.Uri.ToString())
                        });
                }

                return payload?.Encode();
            },
            bson =>
            {
                if (bson.IsNull)
                    return null;

                if (bson.IsDocument)
                    return bson["Type"].AsString switch
                    {
                        "Achievement" => new AchievementPayload((uint)bson["Id"].AsInt64),
                        "PartyFinder" => new PartyFinderPayload((uint)bson["Id"].AsInt64),
                        "URI" => new UriPayload(new Uri(bson["Uri"].AsString)),
                        _ => null
                    };

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
            type => (int)type,
            bson => (ChatType)bson.AsInt32
        );
        BsonMapper.Global.RegisterType(
            source => (int)source,
            bson => (ChatSource)bson.AsInt32
        );
        BsonMapper.Global.RegisterType(
            dateTime => dateTime.Subtract(DateTime.UnixEpoch).TotalMilliseconds,
            bson => DateTime.UnixEpoch.AddMilliseconds(bson.AsInt64)
        );

        var connString = $"Filename='{dbPath}';Connection=direct;ReadOnly={readOnly}";
        var conn = new LiteDatabase(connString, BsonMapper.Global)
        {
            CheckpointSize = 1_000,
            Timeout = TimeSpan.FromSeconds(1)
        };
        var messages = conn.GetCollection<Message>(MessagesCollection);
        messages.EnsureIndex(msg => msg.Date);
        return conn;
    }

    private void DoImport(CancellationToken token)
    {
        var importRateTimer = Stopwatch.StartNew();
        var messagesInLastSecond = 0;

        // Query raw BsonDocuments, so we can convert them in individual
        // try-catch blocks.
        var messagesCollection = _database!.GetCollection<Message>(MessagesCollection);
        var totalMessages = messagesCollection.Count();
        ImportCount = totalMessages;
        var messages = messagesCollection.Query().OrderBy(msg => msg.Date).ToDocuments();
        foreach (var messageDoc in messages)
        {
            if (token.IsCancellationRequested)
                return;

            try
            {
                var message = BsonDocumentToMessage(messageDoc);
                _targetStore.UpsertMessage(message);
                SuccessfulMessages++;
            }
            catch (Exception e)
            {
                FailedMessages++;
                if (FailedMessages <= MaxFailedMessageLogs)
                    _log?.Error($"[Migration] Failed to import message '{messageDoc["_id"].AsObjectId}' (usually due to corruption): {e}");
                if (FailedMessages == MaxFailedMessageLogs)
                    _log?.Error("[Migration] Further failed message logs will be suppressed");
            }

            messagesInLastSecond++;
            if (MaxMessageRate > 0 && messagesInLastSecond > MaxMessageRate)
            {
                var sleepTime = 1000 - (int)importRateTimer.ElapsedMilliseconds;
                if (sleepTime > 0)
                    Thread.Sleep(sleepTime);
            }
            if (importRateTimer.ElapsedMilliseconds > 1000)
            {
                CurrentMessageRate = messagesInLastSecond / (float)importRateTimer.ElapsedMilliseconds * 1000;
                importRateTimer.Restart();
                messagesInLastSecond = 0;
            }

            // Log every 1,000 messages
            if ((SuccessfulMessages + FailedMessages) % 1000 == 0)
                _log?.Information($"[Migration] Progress: successfully imported {SuccessfulMessages}/{totalMessages} messages ({FailedMessages} failures)");
        }

        _log?.Information($"[Migration] Imported {SuccessfulMessages}/{FailedMessages} messages, {FailedMessages} failed");
        if (ProcessedMessages != totalMessages)
            _log?.Warning($"[Migration] Total message count mismatch: expected {totalMessages}, got {SuccessfulMessages + FailedMessages}");

        ImportComplete = Environment.TickCount64;
        _database.Dispose();
        _database = null;

        Plugin?.MessageManager.FilterAllTabsAsync();
    }

    private static Message BsonDocumentToMessage(BsonDocument doc)
    {
        return new Message(
            ObjectIdToGuid(doc["_id"].AsObjectId),
            (ulong)doc["Receiver"].AsInt64,
            (ulong)doc["ContentId"].AsInt64,
            DateTimeOffset.FromUnixTimeMilliseconds(doc["Date"].AsInt64),
            BsonMapper.Global.Deserialize<ChatCode>(doc["Code"].AsDocument),
            BsonMapper.Global.Deserialize<List<Chunk>>(doc["Sender"].AsArray),
            BsonMapper.Global.Deserialize<List<Chunk>>(doc["Content"].AsArray),
            BsonMapper.Global.Deserialize<SeString>(doc["SenderSource"].AsArray),
            BsonMapper.Global.Deserialize<SeString>(doc["ContentSource"].AsArray),
            BsonMapper.Global.Deserialize<SortCode>(doc["SortCode"].AsDocument),
            doc["ExtraChatChannel"].AsGuid
        );
    }

    internal static Guid ObjectIdToGuid(ObjectId objectId)
    {
        // "Generate" a new Guid based on the ObjectId from the original
        // database. We want to have a stable unique identifier for each message
        // so that if the migration somehow happens twice the objects won't be
        // duplicated.
        //
        // Technically, when Guids are generated they follow a specific pattern.
        // However, in practice it doesn't matter at all, and we can just
        // generate whatever we want.
        var objectIdBytes = objectId.ToByteArray();
        var guidBytes = new byte[16];
        // Copy the first 7 bytes directly
        Buffer.BlockCopy(objectIdBytes, 0, guidBytes, 0, 7);
        // Fixed byte for version
        guidBytes[7] = 0b11111111;
        // Copy the next byte.
        guidBytes[8] = objectIdBytes[7];
        // Fixed reserved byte
        guidBytes[9] = 0b11111111;
        // Copy the last 4 bytes.
        Buffer.BlockCopy(objectIdBytes, 8, guidBytes, 10, 4);
        // Set the last 2 bytes to beef
        guidBytes[14] = 0xbe;
        guidBytes[15] = 0xef;

        return new Guid(guidBytes);
    }
}
