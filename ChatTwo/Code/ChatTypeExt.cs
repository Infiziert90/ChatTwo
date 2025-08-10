using ChatTwo.Resources;
using ChatTwo.Util;
using Dalamud.Game.Config;

namespace ChatTwo.Code;

internal static class ChatTypeExt
{
    internal static IEnumerable<(string, ChatType[])> SortOrder => new[]
    {
        (Language.Options_Tabs_ChannelTypes_Special,
        [
            ChatType.Debug,
            ChatType.Urgent,
            ChatType.Notice
        ]),

        (Language.Options_Tabs_ChannelTypes_Chat,
        [
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
            ChatType.CustomEmote
        ]),

        (Language.Options_Tabs_ChannelTypes_Battle, new[]
        {
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

        (Language.Options_Tabs_ChannelTypes_Announcements, new[]
        {
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
            ChatType.GlamourNotifications,
        }),
        // Note: ExtraChat linkshells are handled separately in the tab settings
        // UI.
    };

    internal static string Name(this ChatType type)
    {
        return type switch
        {
            ChatType.Debug => Language.ChatType_Debug,
            ChatType.Urgent => Language.ChatType_Urgent,
            ChatType.Notice => Language.ChatType_Notice,
            ChatType.Say => Language.ChatType_Say,
            ChatType.Shout => Language.ChatType_Shout,
            ChatType.TellOutgoing => Language.ChatType_TellOutgoing,
            ChatType.TellIncoming => Language.ChatType_TellIncoming,
            ChatType.Party => Language.ChatType_Party,
            ChatType.Alliance => Language.ChatType_Alliance,
            ChatType.Linkshell1 => Language.ChatType_Linkshell1,
            ChatType.Linkshell2 => Language.ChatType_Linkshell2,
            ChatType.Linkshell3 => Language.ChatType_Linkshell3,
            ChatType.Linkshell4 => Language.ChatType_Linkshell4,
            ChatType.Linkshell5 => Language.ChatType_Linkshell5,
            ChatType.Linkshell6 => Language.ChatType_Linkshell6,
            ChatType.Linkshell7 => Language.ChatType_Linkshell7,
            ChatType.Linkshell8 => Language.ChatType_Linkshell8,
            ChatType.FreeCompany => Language.ChatType_FreeCompany,
            ChatType.NoviceNetwork => Language.ChatType_NoviceNetwork,
            ChatType.CustomEmote => Language.ChatType_CustomEmotes,
            ChatType.StandardEmote => Language.ChatType_StandardEmotes,
            ChatType.Yell => Language.ChatType_Yell,
            ChatType.CrossParty => Language.ChatType_CrossWorldParty,
            ChatType.PvpTeam => Language.ChatType_PvpTeam,
            ChatType.CrossLinkshell1 => Language.ChatType_CrossLinkshell1,
            ChatType.Damage => Language.ChatType_Damage,
            ChatType.Miss => Language.ChatType_Miss,
            ChatType.Action => Language.ChatType_Action,
            ChatType.Item => Language.ChatType_Item,
            ChatType.Healing => Language.ChatType_Healing,
            ChatType.GainBuff => Language.ChatType_GainBuff,
            ChatType.GainDebuff => Language.ChatType_GainDebuff,
            ChatType.LoseBuff => Language.ChatType_LoseBuff,
            ChatType.LoseDebuff => Language.ChatType_LoseDebuff,
            ChatType.Alarm => Language.ChatType_Alarm,
            ChatType.GlamourNotifications => Language.ChatType_Glamour,
            ChatType.Echo => Language.ChatType_Echo,
            ChatType.System => Language.ChatType_System,
            ChatType.BattleSystem => Language.ChatType_BattleSystem,
            ChatType.GatheringSystem => Language.ChatType_GatheringSystem,
            ChatType.Error => Language.ChatType_Error,
            ChatType.NpcDialogue => Language.ChatType_NpcDialogue,
            ChatType.LootNotice => Language.ChatType_LootNotice,
            ChatType.Progress => Language.ChatType_Progress,
            ChatType.LootRoll => Language.ChatType_LootRoll,
            ChatType.Crafting => Language.ChatType_Crafting,
            ChatType.Gathering => Language.ChatType_Gathering,
            ChatType.NpcAnnouncement => Language.ChatType_NpcAnnouncement,
            ChatType.FreeCompanyAnnouncement => Language.ChatType_FreeCompanyAnnouncement,
            ChatType.FreeCompanyLoginLogout => Language.ChatType_FreeCompanyLoginLogout,
            ChatType.RetainerSale => Language.ChatType_RetainerSale,
            ChatType.PeriodicRecruitmentNotification => Language.ChatType_PeriodicRecruitmentNotification,
            ChatType.Sign => Language.ChatType_Sign,
            ChatType.RandomNumber => Language.ChatType_RandomNumber,
            ChatType.NoviceNetworkSystem => Language.ChatType_NoviceNetworkSystem,
            ChatType.Orchestrion => Language.ChatType_Orchestrion,
            ChatType.PvpTeamAnnouncement => Language.ChatType_PvpTeamAnnouncement,
            ChatType.PvpTeamLoginLogout => Language.ChatType_PvpTeamLoginLogout,
            ChatType.MessageBook => Language.ChatType_MessageBook,
            ChatType.GmTell => Language.ChatType_GmTell,
            ChatType.GmSay => Language.ChatType_GmSay,
            ChatType.GmShout => Language.ChatType_GmShout,
            ChatType.GmYell => Language.ChatType_GmYell,
            ChatType.GmParty => Language.ChatType_GmParty,
            ChatType.GmFreeCompany => Language.ChatType_GmFreeCompany,
            ChatType.GmLinkshell1 => Language.ChatType_GmLinkshell1,
            ChatType.GmLinkshell2 => Language.ChatType_GmLinkshell2,
            ChatType.GmLinkshell3 => Language.ChatType_GmLinkshell3,
            ChatType.GmLinkshell4 => Language.ChatType_GmLinkshell4,
            ChatType.GmLinkshell5 => Language.ChatType_GmLinkshell5,
            ChatType.GmLinkshell6 => Language.ChatType_GmLinkshell6,
            ChatType.GmLinkshell7 => Language.ChatType_GmLinkshell7,
            ChatType.GmLinkshell8 => Language.ChatType_GmLinkshell8,
            ChatType.GmNoviceNetwork => Language.ChatType_GmNoviceNetwork,
            ChatType.CrossLinkshell2 => Language.ChatType_CrossLinkshell2,
            ChatType.CrossLinkshell3 => Language.ChatType_CrossLinkshell3,
            ChatType.CrossLinkshell4 => Language.ChatType_CrossLinkshell4,
            ChatType.CrossLinkshell5 => Language.ChatType_CrossLinkshell5,
            ChatType.CrossLinkshell6 => Language.ChatType_CrossLinkshell6,
            ChatType.CrossLinkshell7 => Language.ChatType_CrossLinkshell7,
            ChatType.CrossLinkshell8 => Language.ChatType_CrossLinkshell8,
            ChatType.ExtraChatLinkshell1 => Language.ChatType_ExtraChatLinkshell1,
            ChatType.ExtraChatLinkshell2 => Language.ChatType_ExtraChatLinkshell2,
            ChatType.ExtraChatLinkshell3 => Language.ChatType_ExtraChatLinkshell3,
            ChatType.ExtraChatLinkshell4 => Language.ChatType_ExtraChatLinkshell4,
            ChatType.ExtraChatLinkshell5 => Language.ChatType_ExtraChatLinkshell5,
            ChatType.ExtraChatLinkshell6 => Language.ChatType_ExtraChatLinkshell6,
            ChatType.ExtraChatLinkshell7 => Language.ChatType_ExtraChatLinkshell7,
            ChatType.ExtraChatLinkshell8 => Language.ChatType_ExtraChatLinkshell8,
            _ => type.ToString(),
        };
    }

    internal static uint? DefaultColor(this ChatType type)
    {
        switch (type)
        {
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
            case ChatType.GlamourNotifications:
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

    internal static InputChannel? ToInputChannel(this ChatType type) => type switch
    {
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

    internal static bool IsGm(this ChatType type) => type switch
    {
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

    internal static bool IsExtraChatLinkshell(this ChatType type) => type switch
    {
        ChatType.ExtraChatLinkshell1 => true,
        ChatType.ExtraChatLinkshell2 => true,
        ChatType.ExtraChatLinkshell3 => true,
        ChatType.ExtraChatLinkshell4 => true,
        ChatType.ExtraChatLinkshell5 => true,
        ChatType.ExtraChatLinkshell6 => true,
        ChatType.ExtraChatLinkshell7 => true,
        ChatType.ExtraChatLinkshell8 => true,
        _ => false,
    };

    public static UiConfigOption ToConfigEntry(this ChatType type) => type switch
    {
        ChatType.Say => UiConfigOption.ColorSay,
        ChatType.Shout => UiConfigOption.ColorShout,
        ChatType.TellOutgoing => UiConfigOption.ColorTell,
        ChatType.Party => UiConfigOption.ColorParty,
        ChatType.Linkshell1 => UiConfigOption.ColorLS1,
        ChatType.Linkshell2 => UiConfigOption.ColorLS2,
        ChatType.Linkshell3 => UiConfigOption.ColorLS3,
        ChatType.Linkshell4 => UiConfigOption.ColorLS4,
        ChatType.Linkshell5 => UiConfigOption.ColorLS5,
        ChatType.Linkshell6 => UiConfigOption.ColorLS6,
        ChatType.Linkshell7 => UiConfigOption.ColorLS7,
        ChatType.Linkshell8 => UiConfigOption.ColorLS8,
        ChatType.FreeCompany => UiConfigOption.ColorFCompany,
        ChatType.NoviceNetwork => UiConfigOption.ColorBeginner,
        ChatType.CustomEmote => UiConfigOption.ColorEmoteUser,
        ChatType.StandardEmote => UiConfigOption.ColorEmote,
        ChatType.Yell => UiConfigOption.ColorYell,
        ChatType.GainBuff => UiConfigOption.ColorBuffGive,
        ChatType.GainDebuff => UiConfigOption.ColorDebuffGive,
        ChatType.System => UiConfigOption.ColorSysMsg,
        ChatType.NpcDialogue => UiConfigOption.ColorNpcSay,
        ChatType.LootRoll => UiConfigOption.ColorLoot,
        ChatType.FreeCompanyAnnouncement => UiConfigOption.ColorFCAnnounce,
        ChatType.PvpTeamAnnouncement => UiConfigOption.ColorPvPGroupAnnounce,
        _ => UiConfigOption.ColorSay,
    };

    internal static bool HasSource(this ChatType type) => type switch
    {
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
        ChatType.System => true,
        ChatType.BattleSystem => true,
        ChatType.Error => true,
        ChatType.LootNotice => true,
        ChatType.Progress => true,
        ChatType.LootRoll => true,
        ChatType.Crafting => true,
        ChatType.Gathering => true,
        ChatType.FreeCompanyLoginLogout => true,
        ChatType.PvpTeamLoginLogout => true,
        _ => false,
    };
}
