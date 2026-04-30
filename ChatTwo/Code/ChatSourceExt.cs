using ChatTwo.Resources;

namespace ChatTwo.Code;

internal static class ChatSourceExt
{
    internal const ChatSource All =
        ChatSource.LocalPlayer | ChatSource.PartyMember | ChatSource.AllianceMember |
        ChatSource.OtherPlayer | ChatSource.EngagedEnemy | ChatSource.UnengagedEnemy |
        ChatSource.FriendlyNpc | ChatSource.PetOrCompanion | ChatSource.PetOrCompanionParty |
        ChatSource.PetOrCompanionAlliance | ChatSource.PetOrCompanionOther;

    internal static string Name(this ChatSource source) => source switch
    {
        ChatSource.LocalPlayer => Language.ChatSource_Self,
        ChatSource.PartyMember => Language.ChatSource_PartyMember,
        ChatSource.AllianceMember => Language.ChatSource_AllianceMember,
        ChatSource.OtherPlayer => Language.ChatSource_Other,
        ChatSource.EngagedEnemy => Language.ChatSource_EngagedEnemy,
        ChatSource.UnengagedEnemy => Language.ChatSource_UnengagedEnemy,
        ChatSource.FriendlyNpc => Language.ChatSource_FriendlyNpc,
        ChatSource.PetOrCompanion => Language.ChatSource_SelfPet,
        ChatSource.PetOrCompanionParty => Language.ChatSource_PartyPet,
        ChatSource.PetOrCompanionAlliance => Language.ChatSource_AlliancePet,
        ChatSource.PetOrCompanionOther => Language.ChatSource_OtherPet,
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
    };
}
