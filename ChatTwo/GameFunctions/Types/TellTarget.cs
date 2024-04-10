namespace ChatTwo.GameFunctions.Types;

internal sealed class TellTarget {
    internal string Name { get; }
    internal ushort World { get; }
    internal ulong ContentId { get; }
    internal TellReason Reason { get; }

    internal TellTarget(string name, ushort world, ulong contentId, TellReason reason) {
        Name = name;
        World = world;
        ContentId = contentId;
        Reason = reason;
    }
}
