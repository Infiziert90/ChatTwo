namespace ChatTwo.Code;

internal class ChatCode
{
    private const ushort Clear7 = ~(~0 << 7);

    internal ushort Raw { get; }

    internal ChatType Type { get; }
    internal ChatSource Source { get; }
    internal ChatSource Target { get; }
    private ChatSource SourceFrom(ushort shift) => (ChatSource) (1 << ((Raw >> shift) & 0xF));

    internal ChatCode(ushort raw)
    {
        Raw = raw;
        Type = (ChatType) (Raw & Clear7);
        Source = SourceFrom(11);
        Target = SourceFrom(7);
    }

    internal ChatType Parent() => Type switch
    {
        ChatType.Say => ChatType.Say,
        ChatType.GmSay => ChatType.Say,
        ChatType.Shout => ChatType.Shout,
        ChatType.GmShout => ChatType.Shout,
        ChatType.TellOutgoing => ChatType.TellOutgoing,
        ChatType.TellIncoming => ChatType.TellOutgoing,
        ChatType.GmTell => ChatType.TellOutgoing,
        ChatType.Party => ChatType.Party,
        ChatType.CrossParty => ChatType.Party,
        ChatType.GmParty => ChatType.Party,
        ChatType.Linkshell1 => ChatType.Linkshell1,
        ChatType.GmLinkshell1 => ChatType.Linkshell1,
        ChatType.Linkshell2 => ChatType.Linkshell2,
        ChatType.GmLinkshell2 => ChatType.Linkshell2,
        ChatType.Linkshell3 => ChatType.Linkshell3,
        ChatType.GmLinkshell3 => ChatType.Linkshell3,
        ChatType.Linkshell4 => ChatType.Linkshell4,
        ChatType.GmLinkshell4 => ChatType.Linkshell4,
        ChatType.Linkshell5 => ChatType.Linkshell5,
        ChatType.GmLinkshell5 => ChatType.Linkshell5,
        ChatType.Linkshell6 => ChatType.Linkshell6,
        ChatType.GmLinkshell6 => ChatType.Linkshell6,
        ChatType.Linkshell7 => ChatType.Linkshell7,
        ChatType.GmLinkshell7 => ChatType.Linkshell7,
        ChatType.Linkshell8 => ChatType.Linkshell8,
        ChatType.GmLinkshell8 => ChatType.Linkshell8,
        ChatType.FreeCompany => ChatType.FreeCompany,
        ChatType.GmFreeCompany => ChatType.FreeCompany,
        ChatType.NoviceNetwork => ChatType.NoviceNetwork,
        ChatType.GmNoviceNetwork => ChatType.NoviceNetwork,
        ChatType.CustomEmote => ChatType.CustomEmote,
        ChatType.StandardEmote => ChatType.StandardEmote,
        ChatType.Yell => ChatType.Yell,
        ChatType.GmYell => ChatType.Yell,
        ChatType.GainBuff => ChatType.GainBuff,
        ChatType.LoseBuff => ChatType.GainBuff,
        ChatType.GainDebuff => ChatType.GainDebuff,
        ChatType.LoseDebuff => ChatType.GainDebuff,
        ChatType.System => ChatType.System,
        ChatType.Alarm => ChatType.System,
        ChatType.GlamourNotifications => ChatType.System,
        ChatType.RetainerSale => ChatType.System,
        ChatType.PeriodicRecruitmentNotification => ChatType.System,
        ChatType.Sign => ChatType.System,
        ChatType.Orchestrion => ChatType.System,
        ChatType.MessageBook => ChatType.System,
        ChatType.NpcDialogue => ChatType.NpcDialogue,
        ChatType.NpcAnnouncement => ChatType.NpcDialogue,
        ChatType.LootRoll => ChatType.LootRoll,
        ChatType.RandomNumber => ChatType.LootRoll,
        ChatType.FreeCompanyAnnouncement => ChatType.FreeCompanyAnnouncement,
        ChatType.FreeCompanyLoginLogout => ChatType.FreeCompanyAnnouncement,
        ChatType.PvpTeamAnnouncement => ChatType.PvpTeamAnnouncement,
        ChatType.PvpTeamLoginLogout => ChatType.PvpTeamAnnouncement,
        _ => Type,
    };

    internal bool IsBattle()
    {
        switch (Type)
        {
            // Error isn't a battle message, but it can be just as spammy if you
            // use macros with unavailable actions.
            case ChatType.Error:
            case ChatType.Damage:
            case ChatType.Miss:
            case ChatType.Action:
            case ChatType.Item:
            case ChatType.Healing:
            case ChatType.GainBuff:
            case ChatType.LoseBuff:
            case ChatType.GainDebuff:
            case ChatType.LoseDebuff:
            case ChatType.BattleSystem:
                return true;
            default:
                return false;
        }
    }

    internal bool IsPlayerMessage()
    {
        switch (Type)
        {
            case ChatType.Say:
            case ChatType.Shout:
            case ChatType.TellOutgoing:
            case ChatType.TellIncoming:
            case ChatType.Party:
            case ChatType.CrossParty:
            case ChatType.Linkshell1:
            case ChatType.Linkshell2:
            case ChatType.Linkshell3:
            case ChatType.Linkshell4:
            case ChatType.Linkshell5:
            case ChatType.Linkshell6:
            case ChatType.Linkshell7:
            case ChatType.Linkshell8:
            case ChatType.CrossLinkshell1:
            case ChatType.CrossLinkshell2:
            case ChatType.CrossLinkshell3:
            case ChatType.CrossLinkshell4:
            case ChatType.CrossLinkshell5:
            case ChatType.CrossLinkshell6:
            case ChatType.CrossLinkshell7:
            case ChatType.CrossLinkshell8:
            case ChatType.FreeCompany:
            case ChatType.NoviceNetwork:
            case ChatType.Yell:
            case ChatType.ExtraChatLinkshell1:
            case ChatType.ExtraChatLinkshell2:
            case ChatType.ExtraChatLinkshell3:
            case ChatType.ExtraChatLinkshell4:
            case ChatType.ExtraChatLinkshell5:
            case ChatType.ExtraChatLinkshell6:
            case ChatType.ExtraChatLinkshell7:
            case ChatType.ExtraChatLinkshell8:
                return true;
            default:
                return false;
        }
    }
}
