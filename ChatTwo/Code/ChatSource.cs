using Dalamud.Game.Text;

namespace ChatTwo.Code;

[Flags]
public enum ChatSource : ushort
{
    None = 0,

    /// <summary>The player currently controlled by the local client.</summary>
    LocalPlayer = 1 << XivChatRelationKind.LocalPlayer,

    /// <summary>A player in the same 4-man or 8-man party as the local player.</summary>
    PartyMember = 1 << XivChatRelationKind.PartyMember,

    /// <summary>A player in the same alliance raid.</summary>
    AllianceMember = 1 << XivChatRelationKind.AllianceMember,

    /// <summary>A player not in the local player's party or alliance.</summary>
    OtherPlayer = 1 << XivChatRelationKind.OtherPlayer,

    /// <summary>An enemy entity that is currently in combat with the player or party.</summary>
    EngagedEnemy = 1 << XivChatRelationKind.EngagedEnemy,

    /// <summary>An enemy entity that is not yet in combat or claimed.</summary>
    UnengagedEnemy = 1 << XivChatRelationKind.UnengagedEnemy,

    /// <summary>An NPC that is friendly or neutral to the player (e.g., EventNPCs).</summary>
    FriendlyNpc = 1 << XivChatRelationKind.FriendlyNpc,

    /// <summary>A pet (Summoner/Scholar) or companion (Chocobo) belonging to the local player.</summary>
    PetOrCompanion = 1 << XivChatRelationKind.PetOrCompanion,

    /// <summary>A pet or companion belonging to a member of the local player's party.</summary>
    PetOrCompanionParty = 1 << XivChatRelationKind.PetOrCompanionParty,

    /// <summary>A pet or companion belonging to a member of the alliance.</summary>
    PetOrCompanionAlliance = 1 << XivChatRelationKind.PetOrCompanionAlliance,

    /// <summary>A pet or companion belonging to a player not in the party or alliance.</summary>
    PetOrCompanionOther = 1 << XivChatRelationKind.PetOrCompanionOther,
}
