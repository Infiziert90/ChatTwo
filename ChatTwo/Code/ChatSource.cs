namespace ChatTwo.Code;

[Flags]
public enum ChatSource : ushort
{
    Self = 2,
    PartyMember = 4,
    AllianceMember = 8,
    Other = 16,
    EngagedEnemy = 32,
    UnengagedEnemy = 64,
    FriendlyNpc = 128,
    SelfPet = 256,
    PartyPet = 512,
    AlliancePet = 1024,
    OtherPet = 2048,
}
