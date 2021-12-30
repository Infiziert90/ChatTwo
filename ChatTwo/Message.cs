using ChatTwo.Code;

namespace ChatTwo;

internal class Message {
    internal DateTime Date { get; }
    internal ChatCode Code { get; }
    internal List<Chunk> Sender { get; }
    internal List<Chunk> Content { get; }

    internal Message(ChatCode code, List<Chunk> sender, List<Chunk> content) {
        this.Date = DateTime.UtcNow;
        this.Code = code;
        this.Sender = sender;
        this.Content = content;
    }
}
