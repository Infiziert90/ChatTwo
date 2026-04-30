using ChatTwo.Code;
using ChatTwo.Resources;

namespace ChatTwo.Util;

public static class TabsUtil
{
    public static Dictionary<ChatType, (ChatSource, ChatSource)> AllChannels()
    {
        var channels = new Dictionary<ChatType, (ChatSource, ChatSource)>();
        foreach (var chatType in Enum.GetValues<ChatType>())
            channels[chatType] = (ChatSourceExt.All, ChatSourceExt.All);

        return channels;
    }

    public static Tab VanillaGeneral => new()
    {
        Name = Language.Tabs_Presets_General,
        SelectedChannels = new Dictionary<ChatType, (ChatSource, ChatSource)>
        {
            // Special
            [ChatType.Debug] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Urgent] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Notice] = (ChatSourceExt.All, ChatSourceExt.All),
            // Chat
            [ChatType.Say] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Yell] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Shout] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.TellIncoming] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.TellOutgoing] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Party] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.CrossParty] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Alliance] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.FreeCompany] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.PvpTeam] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.CrossLinkshell1] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.CrossLinkshell2] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.CrossLinkshell3] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.CrossLinkshell4] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.CrossLinkshell5] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.CrossLinkshell6] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.CrossLinkshell7] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.CrossLinkshell8] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Linkshell1] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Linkshell2] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Linkshell3] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Linkshell4] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Linkshell5] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Linkshell6] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Linkshell7] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Linkshell8] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.NoviceNetwork] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.StandardEmote] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.CustomEmote] = (ChatSourceExt.All, ChatSourceExt.All),
            // Announcements
            [ChatType.System] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.GatheringSystem] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Error] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Echo] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.NoviceNetworkSystem] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.FreeCompanyAnnouncement] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.PvpTeamAnnouncement] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.FreeCompanyLoginLogout] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.PvpTeamLoginLogout] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.RetainerSale] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.NpcAnnouncement] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.LootNotice] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Progress] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.LootRoll] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Crafting] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Gathering] = (ChatSource.LocalPlayer, ChatSource.LocalPlayer),
            [ChatType.PeriodicRecruitmentNotification] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Sign] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.RandomNumber] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Orchestrion] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.MessageBook] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Alarm] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.GlamourNotifications] = (ChatSourceExt.All, ChatSourceExt.All),
        }
    };

    public static Tab VanillaEvent => new()
    {
        Name = Language.Tabs_Presets_Event,
        SelectedChannels = new Dictionary<ChatType, (ChatSource, ChatSource)> { [ChatType.NpcDialogue] = (ChatSourceExt.All, ChatSourceExt.All), },
    };

    public static Tab VanillaTellExclusive => new()
    {
        Name = Language.Tabs_Presets_Tell,
        SelectedChannels = new Dictionary<ChatType, (ChatSource, ChatSource)>
        {
            [ChatType.TellIncoming] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.TellOutgoing] = (ChatSourceExt.All, ChatSourceExt.All),
        },
        Channel = InputChannel.Tell,
        AllSenderMessages = true,
    };

    public static Dictionary<ChatType, (ChatSource, ChatSource)> MostlyPlayer => new()
    {
            // Special
            [ChatType.Debug] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Urgent] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Notice] = (ChatSourceExt.All, ChatSourceExt.All),
            // Chat
            [ChatType.Say] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Yell] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Shout] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.TellIncoming] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.TellOutgoing] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Party] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.CrossParty] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Alliance] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.FreeCompany] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.PvpTeam] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.CrossLinkshell1] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.CrossLinkshell2] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.CrossLinkshell3] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.CrossLinkshell4] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.CrossLinkshell5] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.CrossLinkshell6] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.CrossLinkshell7] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.CrossLinkshell8] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Linkshell1] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Linkshell2] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Linkshell3] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Linkshell4] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Linkshell5] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Linkshell6] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Linkshell7] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Linkshell8] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.NoviceNetwork] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.StandardEmote] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.CustomEmote] = (ChatSourceExt.All, ChatSourceExt.All),
            // Announcements
            [ChatType.System] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Error] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.Echo] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.NoviceNetworkSystem] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.FreeCompanyAnnouncement] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.PvpTeamAnnouncement] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.FreeCompanyLoginLogout] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.PvpTeamLoginLogout] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.RandomNumber] = (ChatSourceExt.All, ChatSourceExt.All),
            [ChatType.MessageBook] = (ChatSourceExt.All, ChatSourceExt.All),
    };
}
