namespace ChatTwo.GameFunctions.Types;

internal sealed class TellTarget {
    internal string Name { get; }
    internal ushort World { get; }
    internal ulong ContentId { get; }
    internal TellReason Reason { get; }

    internal TellTarget(string name, ushort world, ulong contentId, TellReason reason) {
        this.Name = name;
        this.World = world;
        this.ContentId = contentId;
        this.Reason = reason;
    }
}
