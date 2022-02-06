using ChatTwo.Resources;
using ChatTwo.Util;

namespace ChatTwo.Code;

internal static class ChatTypeExt {
    internal static readonly (string, ChatType[])[] SortOrder = {
        (Language.Options_Tabs_ChannelTypes_Special, new[] {
            ChatType.Debug,
            ChatType.Urgent,
            ChatType.Notice,
        }),
        (Language.Options_Tabs_ChannelTypes_Chat, new[] {
            ChatType.Say,
            ChatType.Yell,
            ChatType.Shout,
            ChatType.TellIncoming,
            ChatType.TellOutgoing,
            ChatType.Party,
            ChatType.CrossParty,
            ChatType.Alliance,
            ChatType.FreeCompany,
            ChatType.PvpTeam,
            ChatType.CrossLinkshell1,
            ChatType.CrossLinkshell2,
            ChatType.CrossLinkshell3,
            ChatType.CrossLinkshell4,
            ChatType.CrossLinkshell5,
            ChatType.CrossLinkshell6,
            ChatType.CrossLinkshell7,
            ChatType.CrossLinkshell8,
            ChatType.Linkshell1,
            ChatType.Linkshell2,
            ChatType.Linkshell3,
            ChatType.Linkshell4,
            ChatType.Linkshell5,
            ChatType.Linkshell6,
            ChatType.Linkshell7,
            ChatType.Linkshell8,
            ChatType.NoviceNetwork,
            ChatType.StandardEmote,
            ChatType.CustomEmote,
        }),
        (Language.Options_Tabs_ChannelTypes_Battle, new[] {
            ChatType.Damage,
            ChatType.Miss,
            ChatType.Action,
            ChatType.Item,
            ChatType.Healing,
            ChatType.GainBuff,
            ChatType.LoseBuff,
            ChatType.GainDebuff,
            ChatType.LoseDebuff,
        }),
        (Language.Options_Tabs_ChannelTypes_Announcements, new[] {
            ChatType.System,
            ChatType.BattleSystem,
            ChatType.GatheringSystem,
            ChatType.Error,
            ChatType.Echo,
            ChatType.NoviceNetworkSystem,
            ChatType.FreeCompanyAnnouncement,
            ChatType.PvpTeamAnnouncement,
            ChatType.FreeCompanyLoginLogout,
            ChatType.PvpTeamLoginLogout,
            ChatType.RetainerSale,
            ChatType.NpcDialogue,
            ChatType.NpcAnnouncement,
            ChatType.LootNotice,
            ChatType.Progress,
            ChatType.LootRoll,
            ChatType.Crafting,
            ChatType.Gathering,
            ChatType.PeriodicRecruitmentNotification,
            ChatType.Sign,
            ChatType.RandomNumber,
            ChatType.Orchestrion,
            ChatType.MessageBook,
            ChatType.Alarm,
        }),
    };

    internal static string Name(this ChatType type) {
        return type switch {
            ChatType.Debug => "Debug",
            ChatType.Urgent => "Urgent",
            ChatType.Notice => "Notice",
            ChatType.Say => "Say",
            ChatType.Shout => "Shout",
            ChatType.TellOutgoing => "Tell (Outgoing)",
            ChatType.TellIncoming => "Tell (Incoming)",
            ChatType.Party => "Party",
            ChatType.Alliance => "Alliance",
            ChatType.Linkshell1 => "Linkshell [1]",
            ChatType.Linkshell2 => "Linkshell [2]",
            ChatType.Linkshell3 => "Linkshell [3]",
            ChatType.Linkshell4 => "Linkshell [4]",
            ChatType.Linkshell5 => "Linkshell [5]",
            ChatType.Linkshell6 => "Linkshell [6]",
            ChatType.Linkshell7 => "Linkshell [7]",
            ChatType.Linkshell8 => "Linkshell [8]",
            ChatType.FreeCompany => "Free Company",
            ChatType.NoviceNetwork => "Novice Network",
            ChatType.CustomEmote => "Custom Emotes",
            ChatType.StandardEmote => "Standard Emotes",
            ChatType.Yell => "Yell",
            ChatType.CrossParty => "Cross-world Party",
            ChatType.PvpTeam => "PvP Team",
            ChatType.CrossLinkshell1 => "Cross-world Linkshell [1]",
            ChatType.Damage => "Damage dealt",
            ChatType.Miss => "Failed attacks",
            ChatType.Action => "Actions used",
            ChatType.Item => "Items used",
            ChatType.Healing => "Healing",
            ChatType.GainBuff => "Beneficial effects granted",
            ChatType.GainDebuff => "Detrimental effects inflicted",
            ChatType.LoseBuff => "Beneficial effects lost",
            ChatType.LoseDebuff => "Detrimental effects cured",
            ChatType.Alarm => "Alarm Notifications",
            ChatType.Echo => "Echo",
            ChatType.System => "System Messages",
            ChatType.BattleSystem => "Battle System Messages",
            ChatType.GatheringSystem => "Gathering System Messages",
            ChatType.Error => "Error Messages",
            ChatType.NpcDialogue => "NPC Dialogue",
            ChatType.LootNotice => "Loot Notices",
            ChatType.Progress => "Progression Messages",
            ChatType.LootRoll => "Loot Messages",
            ChatType.Crafting => "Synthesis Messages",
            ChatType.Gathering => "Gathering Messages",
            ChatType.NpcAnnouncement => "NPC Dialogue (Announcements)",
            ChatType.FreeCompanyAnnouncement => "Free Company Announcements",
            ChatType.FreeCompanyLoginLogout => "Free Company Member Login Notifications",
            ChatType.RetainerSale => "Retainer Sale Notifications",
            ChatType.PeriodicRecruitmentNotification => "Periodic Recruitment Notifications",
            ChatType.Sign => "Sign Messages for PC Targets",
            ChatType.RandomNumber => "Random Number Messages",
            ChatType.NoviceNetworkSystem => "Novice Network Notifications",
            ChatType.Orchestrion => "Current Orchestrion Track Messages",
            ChatType.PvpTeamAnnouncement => "PvP Team Announcements",
            ChatType.PvpTeamLoginLogout => "PvP Team Member Login Notifications",
            ChatType.MessageBook => "Message Book Alert",
            ChatType.GmTell => "Tell (GM)",
            ChatType.GmSay => "Say (GM)",
            ChatType.GmShout => "Shout (GM)",
            ChatType.GmYell => "Yell (GM)",
            ChatType.GmParty => "Party (GM)",
            ChatType.GmFreeCompany => "Free Company (GM)",
            ChatType.GmLinkshell1 => "Linkshell [1] (GM)",
            ChatType.GmLinkshell2 => "Linkshell [2] (GM)",
            ChatType.GmLinkshell3 => "Linkshell [3] (GM)",
            ChatType.GmLinkshell4 => "Linkshell [4] (GM)",
            ChatType.GmLinkshell5 => "Linkshell [5] (GM)",
            ChatType.GmLinkshell6 => "Linkshell [6] (GM)",
            ChatType.GmLinkshell7 => "Linkshell [7] (GM)",
            ChatType.GmLinkshell8 => "Linkshell [8] (GM)",
            ChatType.GmNoviceNetwork => "Novice Network (GM)",
            ChatType.CrossLinkshell2 => "Cross-world Linkshell [2]",
            ChatType.CrossLinkshell3 => "Cross-world Linkshell [3]",
            ChatType.CrossLinkshell4 => "Cross-world Linkshell [4]",
            ChatType.CrossLinkshell5 => "Cross-world Linkshell [5]",
            ChatType.CrossLinkshell6 => "Cross-world Linkshell [6]",
            ChatType.CrossLinkshell7 => "Cross-world Linkshell [7]",
            ChatType.CrossLinkshell8 => "Cross-world Linkshell [8]",
            _ => type.ToString(),
        };
    }

    internal static uint? DefaultColour(this ChatType type) {
        switch (type) {
            case ChatType.Debug:
                return ColourUtil.ComponentsToRgba(204, 204, 204);
            case ChatType.Urgent:
                return ColourUtil.ComponentsToRgba(255, 127, 127);
            case ChatType.Notice:
                return ColourUtil.ComponentsToRgba(179, 140, 255);

            case ChatType.Say:
            case ChatType.GmSay:
                return ColourUtil.ComponentsToRgba(247, 247, 247);
            case ChatType.Shout:
            case ChatType.GmShout:
                return ColourUtil.ComponentsToRgba(255, 166, 102);
            case ChatType.TellIncoming:
            case ChatType.TellOutgoing:
            case ChatType.GmTell:
                return ColourUtil.ComponentsToRgba(255, 184, 222);
            case ChatType.Party:
            case ChatType.CrossParty:
            case ChatType.GmParty:
                return ColourUtil.ComponentsToRgba(102, 229, 255);
            case ChatType.Alliance:
                return ColourUtil.ComponentsToRgba(255, 127, 0);
            case ChatType.NoviceNetwork:
            case ChatType.NoviceNetworkSystem:
            case ChatType.GmNoviceNetwork:
                return ColourUtil.ComponentsToRgba(212, 255, 125);
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
            case ChatType.GmLinkshell1:
            case ChatType.GmLinkshell2:
            case ChatType.GmLinkshell3:
            case ChatType.GmLinkshell4:
            case ChatType.GmLinkshell5:
            case ChatType.GmLinkshell6:
            case ChatType.GmLinkshell7:
            case ChatType.GmLinkshell8:
                return ColourUtil.ComponentsToRgba(212, 255, 125);
            case ChatType.StandardEmote:
                return ColourUtil.ComponentsToRgba(186, 255, 240);
            case ChatType.CustomEmote:
                return ColourUtil.ComponentsToRgba(186, 255, 240);
            case ChatType.Yell:
            case ChatType.GmYell:
                return ColourUtil.ComponentsToRgba(255, 255, 0);
            case ChatType.Echo:
                return ColourUtil.ComponentsToRgba(204, 204, 204);
            case ChatType.System:
            case ChatType.GatheringSystem:
            case ChatType.PeriodicRecruitmentNotification:
            case ChatType.Orchestrion:
            case ChatType.Alarm:
            case ChatType.RetainerSale:
            case ChatType.Sign:
            case ChatType.MessageBook:
                return ColourUtil.ComponentsToRgba(204, 204, 204);
            case ChatType.NpcAnnouncement:
            case ChatType.NpcDialogue:
                return ColourUtil.ComponentsToRgba(171, 214, 71);
            case ChatType.Error:
                return ColourUtil.ComponentsToRgba(255, 74, 74);
            case ChatType.FreeCompany:
            case ChatType.FreeCompanyAnnouncement:
            case ChatType.FreeCompanyLoginLogout:
            case ChatType.GmFreeCompany:
                return ColourUtil.ComponentsToRgba(171, 219, 229);
            case ChatType.PvpTeam:
                return ColourUtil.ComponentsToRgba(171, 219, 229);
            case ChatType.PvpTeamAnnouncement:
            case ChatType.PvpTeamLoginLogout:
                return ColourUtil.ComponentsToRgba(171, 219, 229);
            case ChatType.Action:
            case ChatType.Item:
            case ChatType.LootNotice:
                return ColourUtil.ComponentsToRgba(255, 255, 176);
            case ChatType.Progress:
                return ColourUtil.ComponentsToRgba(255, 222, 115);
            case ChatType.LootRoll:
            case ChatType.RandomNumber:
                return ColourUtil.ComponentsToRgba(199, 191, 158);
            case ChatType.Crafting:
            case ChatType.Gathering:
                return ColourUtil.ComponentsToRgba(222, 191, 247);
            case ChatType.Damage:
                return ColourUtil.ComponentsToRgba(255, 125, 125);
            case ChatType.Miss:
                return ColourUtil.ComponentsToRgba(204, 204, 204);
            case ChatType.Healing:
                return ColourUtil.ComponentsToRgba(212, 255, 125);
            case ChatType.GainBuff:
            case ChatType.LoseBuff:
                return ColourUtil.ComponentsToRgba(148, 191, 255);
            case ChatType.GainDebuff:
            case ChatType.LoseDebuff:
                return ColourUtil.ComponentsToRgba(255, 138, 196);
            case ChatType.BattleSystem:
                return ColourUtil.ComponentsToRgba(204, 204, 204);
            default:
                return null;
        }
    }

    internal static InputChannel? ToInputChannel(this ChatType type) => type switch {
        ChatType.TellOutgoing => InputChannel.Tell,
        ChatType.Say => InputChannel.Say,
        ChatType.Party => InputChannel.Party,
        ChatType.Alliance => InputChannel.Alliance,
        ChatType.Yell => InputChannel.Yell,
        ChatType.Shout => InputChannel.Shout,
        ChatType.FreeCompany => InputChannel.FreeCompany,
        ChatType.PvpTeam => InputChannel.PvpTeam,
        ChatType.NoviceNetwork => InputChannel.NoviceNetwork,
        ChatType.CrossLinkshell1 => InputChannel.CrossLinkshell1,
        ChatType.CrossLinkshell2 => InputChannel.CrossLinkshell2,
        ChatType.CrossLinkshell3 => InputChannel.CrossLinkshell3,
        ChatType.CrossLinkshell4 => InputChannel.CrossLinkshell4,
        ChatType.CrossLinkshell5 => InputChannel.CrossLinkshell5,
        ChatType.CrossLinkshell6 => InputChannel.CrossLinkshell6,
        ChatType.CrossLinkshell7 => InputChannel.CrossLinkshell7,
        ChatType.CrossLinkshell8 => InputChannel.CrossLinkshell8,
        ChatType.Linkshell1 => InputChannel.Linkshell1,
        ChatType.Linkshell2 => InputChannel.Linkshell2,
        ChatType.Linkshell3 => InputChannel.Linkshell3,
        ChatType.Linkshell4 => InputChannel.Linkshell4,
        ChatType.Linkshell5 => InputChannel.Linkshell5,
        ChatType.Linkshell6 => InputChannel.Linkshell6,
        ChatType.Linkshell7 => InputChannel.Linkshell7,
        ChatType.Linkshell8 => InputChannel.Linkshell8,
        _ => null,
    };

    internal static bool IsGm(this ChatType type) => type switch {
        ChatType.GmTell => true,
        ChatType.GmSay => true,
        ChatType.GmShout => true,
        ChatType.GmYell => true,
        ChatType.GmParty => true,
        ChatType.GmFreeCompany => true,
        ChatType.GmLinkshell1 => true,
        ChatType.GmLinkshell2 => true,
        ChatType.GmLinkshell3 => true,
        ChatType.GmLinkshell4 => true,
        ChatType.GmLinkshell5 => true,
        ChatType.GmLinkshell6 => true,
        ChatType.GmLinkshell7 => true,
        ChatType.GmLinkshell8 => true,
        ChatType.GmNoviceNetwork => true,
        _ => false,
    };

    internal static bool HasSource(this ChatType type) => type switch {
        // Battle
        ChatType.Damage => true,
        ChatType.Miss => true,
        ChatType.Action => true,
        ChatType.Item => true,
        ChatType.Healing => true,
        ChatType.GainBuff => true,
        ChatType.LoseBuff => true,
        ChatType.GainDebuff => true,
        ChatType.LoseDebuff => true,

        // Announcements
        ChatType.Progress => true,
        ChatType.LootRoll => true,
        ChatType.Crafting => true,
        ChatType.Gathering => true,
        _ => false,
    };
}
