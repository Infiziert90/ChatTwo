using ChatTwo.Resources;

namespace ChatTwo.Code;

internal static class ChatSourceExt
{
    internal const ChatSource All =
        ChatSource.Self
        | ChatSource.PartyMember
        | ChatSource.AllianceMember
        | ChatSource.Other
        | ChatSource.EngagedEnemy
        | ChatSource.UnengagedEnemy
        | ChatSource.FriendlyNpc
        | ChatSource.SelfPet
        | ChatSource.PartyPet
        | ChatSource.AlliancePet
        | ChatSource.OtherPet;

    internal static string Name(this ChatSource source) => source switch
    {
        ChatSource.Self => Language.ChatSource_Self,
        ChatSource.PartyMember => Language.ChatSource_PartyMember,
        ChatSource.AllianceMember => Language.ChatSource_AllianceMember,
        ChatSource.Other => Language.ChatSource_Other,
        ChatSource.EngagedEnemy => Language.ChatSource_EngagedEnemy,
        ChatSource.UnengagedEnemy => Language.ChatSource_UnengagedEnemy,
        ChatSource.FriendlyNpc => Language.ChatSource_FriendlyNpc,
        ChatSource.SelfPet => Language.ChatSource_SelfPet,
        ChatSource.PartyPet => Language.ChatSource_PartyPet,
        ChatSource.AlliancePet => Language.ChatSource_AlliancePet,
        ChatSource.OtherPet => Language.ChatSource_OtherPet,
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
    };
}
