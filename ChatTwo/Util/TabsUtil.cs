using ChatTwo.Code;

namespace ChatTwo.Util;

internal static class TabsUtil {
    internal static Tab VanillaGeneral => new() {
        Name = "General",
        ChatCodes = new Dictionary<ChatType, ChatSource> {
            // Special
            [ChatType.Debug] = ChatSourceExt.All,
            [ChatType.Urgent] = ChatSourceExt.All,
            [ChatType.Notice] = ChatSourceExt.All,
            // Chat
            [ChatType.Say] = ChatSourceExt.All,
            [ChatType.Yell] = ChatSourceExt.All,
            [ChatType.Shout] = ChatSourceExt.All,
            [ChatType.TellIncoming] = ChatSourceExt.All,
            [ChatType.TellOutgoing] = ChatSourceExt.All,
            [ChatType.Party] = ChatSourceExt.All,
            [ChatType.CrossParty] = ChatSourceExt.All,
            [ChatType.Alliance] = ChatSourceExt.All,
            [ChatType.FreeCompany] = ChatSourceExt.All,
            [ChatType.PvpTeam] = ChatSourceExt.All,
            [ChatType.CrossLinkshell1] = ChatSourceExt.All,
            [ChatType.CrossLinkshell2] = ChatSourceExt.All,
            [ChatType.CrossLinkshell3] = ChatSourceExt.All,
            [ChatType.CrossLinkshell4] = ChatSourceExt.All,
            [ChatType.CrossLinkshell5] = ChatSourceExt.All,
            [ChatType.CrossLinkshell6] = ChatSourceExt.All,
            [ChatType.CrossLinkshell7] = ChatSourceExt.All,
            [ChatType.CrossLinkshell8] = ChatSourceExt.All,
            [ChatType.Linkshell1] = ChatSourceExt.All,
            [ChatType.Linkshell2] = ChatSourceExt.All,
            [ChatType.Linkshell3] = ChatSourceExt.All,
            [ChatType.Linkshell4] = ChatSourceExt.All,
            [ChatType.Linkshell5] = ChatSourceExt.All,
            [ChatType.Linkshell6] = ChatSourceExt.All,
            [ChatType.Linkshell7] = ChatSourceExt.All,
            [ChatType.Linkshell8] = ChatSourceExt.All,
            [ChatType.NoviceNetwork] = ChatSourceExt.All,
            [ChatType.StandardEmote] = ChatSourceExt.All,
            [ChatType.CustomEmote] = ChatSourceExt.All,
            // Announcements
            [ChatType.System] = ChatSourceExt.All,
            [ChatType.GatheringSystem] = ChatSourceExt.All,
            [ChatType.Error] = ChatSourceExt.All,
            [ChatType.Echo] = ChatSourceExt.All,
            [ChatType.NoviceNetworkSystem] = ChatSourceExt.All,
            [ChatType.FreeCompanyAnnouncement] = ChatSourceExt.All,
            [ChatType.PvpTeamAnnouncement] = ChatSourceExt.All,
            [ChatType.FreeCompanyLoginLogout] = ChatSourceExt.All,
            [ChatType.PvpTeamLoginLogout] = ChatSourceExt.All,
            [ChatType.RetainerSale] = ChatSourceExt.All,
            [ChatType.NpcAnnouncement] = ChatSourceExt.All,
            [ChatType.LootNotice] = ChatSourceExt.All,
            [ChatType.Progress] = ChatSourceExt.All,
            [ChatType.LootRoll] = ChatSourceExt.All,
            [ChatType.Crafting] = ChatSourceExt.All,
            [ChatType.Gathering] = ChatSource.Self,
            [ChatType.PeriodicRecruitmentNotification] = ChatSourceExt.All,
            [ChatType.Sign] = ChatSourceExt.All,
            [ChatType.RandomNumber] = ChatSourceExt.All,
            [ChatType.Orchestrion] = ChatSourceExt.All,
            [ChatType.MessageBook] = ChatSourceExt.All,
            [ChatType.Alarm] = ChatSourceExt.All,
        },
    };

    internal static Tab VanillaEvent => new() {
        Name = "Event",
        ChatCodes = new Dictionary<ChatType, ChatSource> {
            [ChatType.NpcDialogue] = ChatSourceExt.All,
        },
    };
}
