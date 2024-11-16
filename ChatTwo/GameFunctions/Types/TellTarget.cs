namespace ChatTwo.GameFunctions.Types;

internal sealed class TellTarget
{
    internal string Name { get; }
    internal ushort World { get; }
    internal ulong ContentId { get; }
    internal TellReason Reason { get; }

    internal TellTarget(string name, ushort world, ulong contentId, TellReason reason)
    {
        Name = name;
        World = world;
        ContentId = contentId;
        Reason = reason;
    }

    public bool IsSet() => Name.Length > 0 && World > 0;

    public string ToWorldString() => Sheets.WorldSheet.TryGetRow(World, out var worldRow) ? worldRow.Name.ExtractText() : string.Empty;
    public string ToTargetString() => $"{Name}@{ToWorldString()}";
}
