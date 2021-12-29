namespace ChatTwo.Code;

internal static class ChatSourceExt {
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
}
