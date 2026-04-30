using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace ChatTwo.GameFunctions.Types;

[Serializable]
public class TellTarget
{
    public string Name { get; set; }
    public uint World { get; set; }
    public ulong ContentId { get; private set; }
    public TellReason Reason { get; private set; }

    public TellTarget(string name, uint world, ulong contentId, TellReason reason)
    {
        Name = name;
        World = world;
        ContentId = contentId;
        Reason = reason;
    }

    public bool IsSet()
        => Name.Length > 0 && World > 0;

    public string ToWorldString()
        => Sheets.WorldSheet.TryGetRow(World, out var worldRow) ? worldRow.Name.ToString() : string.Empty;

    public string ToTargetString()
        => $"{Name}@{ToWorldString()}";

    public unsafe void FromTarget(IPlayerCharacter target)
    {
        Name = target.Name.TextValue;
        World = target.HomeWorld.RowId;
        ContentId = ((Character*)target.Address)->ContentId;
    }

    public static TellTarget Empty() => new(string.Empty, 0, 0, TellReason.Direct);
    public static TellTarget From(TellTarget t) => new(t.Name, t.World, t.ContentId, t.Reason);
}
