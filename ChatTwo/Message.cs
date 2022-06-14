using ChatTwo.Code;
using Dalamud.Game.Text.SeStringHandling;
using LiteDB;

namespace ChatTwo;

internal class SortCode {
    internal ChatType Type { get; set; }
    internal ChatSource Source { get; set; }

    internal SortCode(ChatType type, ChatSource source) {
        this.Type = type;
        this.Source = source;
    }

    public SortCode() {
    }

    private bool Equals(SortCode other) {
        return this.Type == other.Type && this.Source == other.Source;
    }

    public override bool Equals(object? obj) {
        if (ReferenceEquals(null, obj)) {
            return false;
        }

        if (ReferenceEquals(this, obj)) {
            return true;
        }

        return obj.GetType() == this.GetType() && this.Equals((SortCode) obj);
    }

    public override int GetHashCode() {
        unchecked {
            return ((int) this.Type * 397) ^ (int) this.Source;
        }
    }
}

internal class Message {
    // ReSharper disable once UnusedMember.Global
    internal ObjectId Id { get; } = ObjectId.NewObjectId();
    internal ulong Receiver { get; }
    internal ulong ContentId { get; set; }

    [BsonIgnore]
    internal float? Height;

    [BsonIgnore]
    internal bool IsVisible;

    internal DateTime Date { get; }
    internal ChatCode Code { get; }
    internal List<Chunk> Sender { get; }
    internal List<Chunk> Content { get; }

    internal SeString SenderSource { get; }
    internal SeString ContentSource { get; }

    internal SortCode SortCode { get; }

    internal int Hash { get; }

    internal Message(ulong receiver, ChatCode code, List<Chunk> sender, List<Chunk> content, SeString senderSource, SeString contentSource) {
        this.Receiver = receiver;
        this.Date = DateTime.UtcNow;
        this.Code = code;
        this.Sender = sender;
        this.Content = content;
        this.SenderSource = senderSource;
        this.ContentSource = contentSource;
        this.SortCode = new SortCode(this.Code.Type, this.Code.Source);
        this.Hash = this.GenerateHash();

        foreach (var chunk in sender.Concat(content)) {
            chunk.Message = this;
        }
    }

    internal Message(ObjectId id, ulong receiver, ulong contentId, DateTime date, BsonDocument code, BsonArray sender, BsonArray content, BsonValue senderSource, BsonValue contentSource, BsonDocument sortCode) {
        this.Id = id;
        this.Receiver = receiver;
        this.ContentId = contentId;
        this.Date = date;
        this.Code = BsonMapper.Global.ToObject<ChatCode>(code);
        this.Sender = BsonMapper.Global.Deserialize<List<Chunk>>(sender);
        this.Content = BsonMapper.Global.Deserialize<List<Chunk>>(content);
        this.SenderSource = BsonMapper.Global.Deserialize<SeString>(senderSource);
        this.ContentSource = BsonMapper.Global.Deserialize<SeString>(contentSource);
        this.SortCode = BsonMapper.Global.ToObject<SortCode>(sortCode);
        this.Hash = this.GenerateHash();

        foreach (var chunk in this.Sender.Concat(this.Content)) {
            chunk.Message = this;
        }
    }

    private int GenerateHash() {
        return this.SortCode.GetHashCode()
               ^ string.Join("", this.Sender.Select(c => c.StringValue())).GetHashCode()
               ^ string.Join("", this.Content.Select(c => c.StringValue())).GetHashCode();
    }
}
