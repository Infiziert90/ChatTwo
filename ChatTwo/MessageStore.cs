using System.Buffers;
using System.Collections;
using System.Data.Common;
using ChatTwo.Code;
using ChatTwo.Util;
using Dalamud.Game.Text.SeStringHandling;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using Microsoft.Data.Sqlite;
using DalamudUtil = Dalamud.Utility.Util;
using Encoding = System.Text.Encoding;

namespace ChatTwo;

internal static class DbExtensions
{
    internal static void Execute(this DbConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}

internal enum PayloadMessagePackType : byte
{
    Achievement,
    PartyFinder,
    Uri,
    Emote,
    Other = 255,
}

public class PayloadMessagePackFormatter : IMessagePackFormatter<Payload?>
{
    public void Serialize(ref MessagePackWriter writer, Payload? value, MessagePackSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNil();
            return;
        }

        writer.WriteArrayHeader(2);
        switch (value)
        {
            case AchievementPayload achievementPayload:
                writer.WriteUInt8((byte)PayloadMessagePackType.Achievement);
                writer.WriteUInt32(achievementPayload.Id);
                break;
            case PartyFinderPayload partyFinderPayload:
                writer.WriteUInt8((byte)PayloadMessagePackType.PartyFinder);
                writer.WriteUInt32(partyFinderPayload.Id);
                break;
            case UriPayload uriPayload:
                writer.WriteUInt8((byte)PayloadMessagePackType.Uri);
                writer.WriteString(Encoding.UTF8.GetBytes(uriPayload.Uri.ToString()));
                break;
            case EmotePayload emotePayload:
                writer.WriteUInt8((byte)PayloadMessagePackType.Emote);
                writer.WriteString(Encoding.UTF8.GetBytes(emotePayload.Code));
                break;
            default:
                writer.WriteUInt8((byte)PayloadMessagePackType.Other);
                writer.Write(value.Encode());
                break;
        }
    }

    public Payload? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
            return null;

        if (reader.ReadArrayHeader() != 2)
            throw new InvalidOperationException("Invalid array count for Payload object");

        var type = (PayloadMessagePackType)reader.ReadByte();
        switch (type)
        {
            case PayloadMessagePackType.Achievement:
                return new AchievementPayload(reader.ReadUInt32());
            case PayloadMessagePackType.PartyFinder:
                return new PartyFinderPayload(reader.ReadUInt32());
            case PayloadMessagePackType.Uri:
                return new UriPayload(new Uri(reader.ReadString() ?? ""));
            case PayloadMessagePackType.Emote:
                return EmotePayload.ResolveEmote(reader.ReadString() ?? "");
            case PayloadMessagePackType.Other:
            default:
                var bytes = reader.ReadBytes() ?? new ReadOnlySequence<byte>();
                var binReader = new BinaryReader(new MemoryStream(bytes.ToArray()));
                return Payload.Decode(binReader);
        }
    }
}

public class SeStringMessagePackFormatter : IMessagePackFormatter<SeString>
{
    public void Serialize(ref MessagePackWriter writer, SeString value, MessagePackSerializerOptions options)
    {
        options.Resolver.GetFormatter<List<Payload>>()!.Serialize(ref writer, value.Payloads, options);
    }

    public SeString Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        return new SeString(options.Resolver.GetFormatter<List<Payload>>()!.Deserialize(ref reader, options));
    }
}

internal class MessageStore : IDisposable
{
    private const int MessageQueryLimit = 10_000;

    private string DbPath { get; }

    private SqliteConnection Connection { get; set; }

    internal static readonly MessagePackSerializerOptions MsgPackOptions = MessagePackSerializerOptions.Standard
        .WithResolver(CompositeResolver.Create(
            new IMessagePackFormatter[] { new PayloadMessagePackFormatter(), new SeStringMessagePackFormatter(), },
            new IFormatterResolver[] { StandardResolver.Instance }
            )
        );

    internal MessageStore(string dbPath)
    {
        DbPath = dbPath;
        Connection = Connect();
        Migrate();
    }

    public void Dispose()
    {
        Connection.Close();
        Connection.Dispose();
        // Closing the connection doesn't immediately release the file.
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    private SqliteConnection Connect()
    {
        var uriBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = DbPath,
            DefaultTimeout = 5,
            Pooling = false,
            Mode = SqliteOpenMode.ReadWriteCreate,
        };

        var conn = new SqliteConnection(uriBuilder.ToString());
        conn.Open();
        conn.Execute(@"PRAGMA journal_mode=WAL;");
        conn.Execute(@"PRAGMA synchronous=NORMAL;");
        if (DalamudUtil.IsWine())
            conn.Execute(@"PRAGMA cache_size = 32768;");
        return conn;
    }

    private void Migrate()
    {
        // Get current user_version.
        var cmd = Connection.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        var userVersion = Convert.ToInt32(cmd.ExecuteScalar());

        var migrationsToDo = new List<Action>();
        switch (userVersion)
        {
            case <= 0:
                migrationsToDo.Add(Migrate0);
                // Migration support was only added in version 1. Migrate0 is
                // idempotent.
                migrationsToDo.Add(Migrate1);
                break;
            case 1:
                migrationsToDo.Add(Migrate2);
                break;
        }

        foreach (var migration in migrationsToDo)
            migration();
    }

    private void Migrate0()
    {
        Connection.Execute(@"
            CREATE TABLE IF NOT EXISTS messages (
                Id BLOB PRIMARY KEY NOT NULL,  -- Guid
                Receiver INTEGER NOT NULL,     -- uint64 (first bits are always 0)
                ContentId INTEGER NOT NULL,    -- uint64 (first bits are always 0)
                Date INTEGER NOT NULL,         -- unix timestamp with millisecond precision
                Code INTEGER NOT NULL,         -- ChatCode encoding
                Sender BLOB NOT NULL,          -- Chunk[] msgpack
                Content BLOB NOT NULL,         -- Chunk[] msgpack
                SenderSource BLOB NOT NULL,    -- SeString
                ContentSource BLOB NOT NULL,   -- SeString
                SortCode INTEGER NOT NULL,     -- SortCode encoding
                ExtraChatChannel BLOB NOT NULL -- Guid
            );

            CREATE INDEX IF NOT EXISTS idx_messages_receiver ON messages (Receiver);
            CREATE INDEX IF NOT EXISTS idx_messages_date ON messages (Date);
        ");

        SetMigrationVersion(0);
    }

    private void Migrate1()
    {
        Connection.Execute(@"
            -- Migration 1: Add Deleted column
            ALTER TABLE messages ADD COLUMN Deleted BOOLEAN NOT NULL DEFAULT false;
        ");

        SetMigrationVersion(1);
    }

    private void Migrate2()
    {
        Connection.Execute(@"
            -- Migration 2: Add Channel generated column
            ALTER TABLE messages ADD COLUMN Channel INTEGER GENERATED ALWAYS AS (Code & 0x7f) VIRTUAL;
            CREATE INDEX IF NOT EXISTS idx_messages_channel ON messages (Channel);
        ");

        SetMigrationVersion(2);
    }

    private void SetMigrationVersion(int version)
    {
        var cmd = Connection.CreateCommand();
        // Parameters aren't supported for PRAGMA queries, and you can't set the
        // version with a pragma_ function.
        cmd.CommandText = $"PRAGMA user_version = {version};";
        cmd.ExecuteNonQuery();
    }

    internal void ClearMessages()
    {
        Connection.Execute("DELETE FROM messages;");
        PerformMaintenance();
    }

    internal void PerformMaintenance()
    {
        Connection.Execute(@"
            VACUUM;
            REINDEX messages;
            ANALYZE;
        ");
    }

    private string LogPath => DbPath + "-wal";
    internal long DatabaseSize() => !File.Exists(DbPath) ? 0 : new FileInfo(DbPath).Length;
    internal long DatabaseLogSize() => !File.Exists(LogPath) ? 0 : new FileInfo(LogPath).Length;

    internal int MessageCount()
    {
        var cmd = Connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM messages;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    internal void UpsertMessage(Message message)
    {
        var cmd = Connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO messages (
                Id,
                Receiver,
                ContentId,
                Date,
                Code,
                Sender,
                Content,
                SenderSource,
                ContentSource,
                SortCode,
                ExtraChatChannel,
                Deleted
            ) VALUES (
                $Id,
                $Receiver,
                $ContentId,
                $Date,
                $Code,
                $Sender,
                $Content,
                $SenderSource,
                $ContentSource,
                $SortCode,
                $ExtraChatChannel,
                false
            )
            ON CONFLICT (id) DO UPDATE SET
                Receiver = excluded.Receiver,
                ContentId = excluded.ContentId,
                Date = excluded.Date,
                Code = excluded.Code,
                Sender = excluded.Sender,
                Content = excluded.Content,
                SenderSource = excluded.SenderSource,
                ContentSource = excluded.ContentSource,
                SortCode = excluded.SortCode,
                ExtraChatChannel = excluded.ExtraChatChannel,
                Deleted = false
            ;
        ";

        cmd.Parameters.AddWithValue("$Id", message.Id);
        cmd.Parameters.AddWithValue("$Receiver", message.Receiver);
        cmd.Parameters.AddWithValue("$ContentId", message.ContentId);
        cmd.Parameters.AddWithValue("$Date", message.Date.ToUnixTimeMilliseconds());
        cmd.Parameters.AddWithValue("$Code", message.Code.Raw);
        cmd.Parameters.AddWithValue("$Sender", MessagePackSerializer.Serialize(message.Sender, MsgPackOptions));
        cmd.Parameters.AddWithValue("$Content", MessagePackSerializer.Serialize(message.Content, MsgPackOptions));
        cmd.Parameters.AddWithValue("$SenderSource", MessagePackSerializer.Serialize(message.SenderSource, MsgPackOptions));
        cmd.Parameters.AddWithValue("$ContentSource", MessagePackSerializer.Serialize(message.ContentSource, MsgPackOptions));
        cmd.Parameters.AddWithValue("$SortCode", message.SortCode.Encode());
        cmd.Parameters.AddWithValue("$ExtraChatChannel", message.ExtraChatChannel);

        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Get the most recent messages.
    /// </summary>
    /// <param name="receiver">The receiver content ID to filter by. If null, no filtering is performed.</param>
    /// <param name="since">Only show messages since this date. If null, no filtering is performed.</param>
    /// <param name="count">The amount to return. Defaults to 10,000.</param>
    internal MessageEnumerator GetMostRecentMessages(ulong? receiver = null, DateTimeOffset? since = null, int count = MessageQueryLimit)
    {
        List<string> whereClauses = ["deleted = false"];
        if (receiver != null)
            whereClauses.Add("Receiver = $Receiver");
        if (since != null)
            whereClauses.Add("Date >= $Since");

        var whereClause = "WHERE " + string.Join(" AND ", whereClauses);

        var cmd = Connection.CreateCommand();
        // Select last N messages by date DESC, but reverse the order to get
        // them in ascending order.
        cmd.CommandText = @"
            SELECT *
            FROM (
                SELECT
                    Id,
                    Receiver,
                    ContentId,
                    Date,
                    Code,
                    Sender,
                    Content,
                    SenderSource,
                    ContentSource,
                    SortCode,
                    ExtraChatChannel
                FROM messages
                " + whereClause + @"
                ORDER BY Date DESC
                LIMIT $Count
            )
            ORDER BY Date ASC;
        ";
        cmd.CommandTimeout = 120; // this could take a while on slow computers

        if (receiver != null)
            cmd.Parameters.AddWithValue("$Receiver", receiver);
        if (since != null)
            cmd.Parameters.AddWithValue("$Since", since.Value.ToUnixTimeMilliseconds());

        cmd.Parameters.AddWithValue("$Count", count);

        return new MessageEnumerator(cmd.ExecuteReader());
    }

    /// <summary>
    /// Marks a message as deleted so it won't get returned in queries.
    /// </summary>
    internal void DeleteMessage(Guid id)
    {
        var cmd = Connection.CreateCommand();
        cmd.CommandText = "UPDATE messages SET Deleted = true WHERE Id = $Id;";
        cmd.Parameters.AddWithValue("$Id", id);
        cmd.ExecuteNonQuery();
    }

    internal long CountDateRange(DateTime after, DateTime before, uint[] channels, ulong? receiver = null)
    {
        List<string> whereClauses = ["deleted = false"];
        if (receiver != null)
            whereClauses.Add("Receiver = $Receiver");

        whereClauses.Add("Date BETWEEN $After AND $Before");
        whereClauses.Add($"Channel IN ({string.Join(", ", channels)})");

        var whereClause = "WHERE " + string.Join(" AND ", whereClauses);

        var cmd = Connection.CreateCommand();
        // Select last N messages by date DESC, but reverse the order to get
        // them in ascending order.
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM messages
            " + whereClause;
        cmd.CommandTimeout = 120; // this could take a while on slow computers

        if (receiver != null)
            cmd.Parameters.AddWithValue("$Receiver", receiver);

        cmd.Parameters.AddWithValue("$After", ((DateTimeOffset) after).ToUnixTimeMilliseconds());
        cmd.Parameters.AddWithValue("$Before", ((DateTimeOffset) before).ToUnixTimeMilliseconds());

        return (long) cmd.ExecuteScalar()!;
    }

    internal MessageEnumerator GetDateRange(DateTime after, DateTime before, uint[] channels, ulong? receiver = null, int page = 0)
    {
        List<string> whereClauses = ["deleted = false"];
        if (receiver != null)
            whereClauses.Add("Receiver = $Receiver");

        whereClauses.Add("Date BETWEEN $After AND $Before");
        whereClauses.Add($"Channel IN ({string.Join(", ", channels)})");

        var whereClause = "WHERE " + string.Join(" AND ", whereClauses);

        var cmd = Connection.CreateCommand();
        // Select last N messages by date DESC, but reverse the order to get
        // them in ascending order.
        cmd.CommandText = @"
            SELECT
                Id,
                Receiver,
                ContentId,
                Date,
                Code,
                Sender,
                Content,
                SenderSource,
                ContentSource,
                SortCode,
                ExtraChatChannel
            FROM messages
            " + whereClause + @"
            LIMIT $Offset, 500;
        ";
        cmd.CommandTimeout = 120; // this could take a while on slow computers

        if (receiver != null)
            cmd.Parameters.AddWithValue("$Receiver", receiver);

        cmd.Parameters.AddWithValue("$After", ((DateTimeOffset) after).ToUnixTimeMilliseconds());
        cmd.Parameters.AddWithValue("$Before", ((DateTimeOffset) before).ToUnixTimeMilliseconds());
        cmd.Parameters.AddWithValue("$Offset", 500 * page);

        return new MessageEnumerator(cmd.ExecuteReader());
    }
}

internal class MessageEnumerator(DbDataReader reader) : IEnumerable<Message>
{
    private const int MaxErrorLogs = 10;

    // FailedIds and FailedCount are separate, because messages might fail to
    // even parse the ID field.
    private readonly List<Guid> FailedIds = new();
    private int FailedCount;
    public bool DidError => FailedCount > 0;

    public IEnumerator<Message> GetEnumerator()
    {
        while (reader.Read())
        {
            var id = Guid.Empty;
            Message msg;
            try
            {
                id = reader.GetGuid(0);
                msg = new Message(
                    id,
                    (ulong)reader.GetInt64(1),
                    (ulong)reader.GetInt64(2),
                    DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(3)),
                    new ChatCode((ushort)reader.GetInt32(4)),
                    MessagePackSerializer.Deserialize<List<Chunk>>(reader.GetFieldValue<byte[]>(5), MessageStore.MsgPackOptions),
                    MessagePackSerializer.Deserialize<List<Chunk>>(reader.GetFieldValue<byte[]>(6), MessageStore.MsgPackOptions),
                    MessagePackSerializer.Deserialize<SeString>(reader.GetFieldValue<byte[]>(7), MessageStore.MsgPackOptions),
                    MessagePackSerializer.Deserialize<SeString>(reader.GetFieldValue<byte[]>(8), MessageStore.MsgPackOptions),
                    new SortCode((uint)reader.GetInt32(9)),
                    reader.GetGuid(10)
                );
            }
            catch (Exception e)
            {
                if (FailedCount < MaxErrorLogs)
                    Plugin.Log.Error($"Exception while reading message '{id}' from database: {e}");
                FailedCount++;
                if (FailedCount == MaxErrorLogs)
                    Plugin.Log.Error("Further parsing errors will not be logged");
                if (id != Guid.Empty)
                    FailedIds.Add(id);

                continue;
            }

            yield return msg;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public IReadOnlyList<Guid> FailedMessageIds()
    {
        return FailedIds;
    }
}
