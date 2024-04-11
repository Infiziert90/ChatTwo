using Dalamud.Plugin.Services;
using Lumina.Excel.GeneratedSheets;

namespace ChatTwo.Code;

internal static class InputChannelExt
{
    internal static ChatType ToChatType(this InputChannel input) => input switch
    {
        InputChannel.Tell => ChatType.TellOutgoing,
        InputChannel.Say => ChatType.Say,
        InputChannel.Party => ChatType.Party,
        InputChannel.Alliance => ChatType.Alliance,
        InputChannel.Yell => ChatType.Yell,
        InputChannel.Shout => ChatType.Shout,
        InputChannel.FreeCompany => ChatType.FreeCompany,
        InputChannel.PvpTeam => ChatType.PvpTeam,
        InputChannel.NoviceNetwork => ChatType.NoviceNetwork,
        InputChannel.CrossLinkshell1 => ChatType.CrossLinkshell1,
        InputChannel.CrossLinkshell2 => ChatType.CrossLinkshell2,
        InputChannel.CrossLinkshell3 => ChatType.CrossLinkshell3,
        InputChannel.CrossLinkshell4 => ChatType.CrossLinkshell4,
        InputChannel.CrossLinkshell5 => ChatType.CrossLinkshell5,
        InputChannel.CrossLinkshell6 => ChatType.CrossLinkshell6,
        InputChannel.CrossLinkshell7 => ChatType.CrossLinkshell7,
        InputChannel.CrossLinkshell8 => ChatType.CrossLinkshell8,
        InputChannel.Linkshell1 => ChatType.Linkshell1,
        InputChannel.Linkshell2 => ChatType.Linkshell2,
        InputChannel.Linkshell3 => ChatType.Linkshell3,
        InputChannel.Linkshell4 => ChatType.Linkshell4,
        InputChannel.Linkshell5 => ChatType.Linkshell5,
        InputChannel.Linkshell6 => ChatType.Linkshell6,
        InputChannel.Linkshell7 => ChatType.Linkshell7,
        InputChannel.Linkshell8 => ChatType.Linkshell8,
        InputChannel.ExtraChatLinkshell1 => ChatType.ExtraChatLinkshell1,
        InputChannel.ExtraChatLinkshell2 => ChatType.ExtraChatLinkshell2,
        InputChannel.ExtraChatLinkshell3 => ChatType.ExtraChatLinkshell3,
        InputChannel.ExtraChatLinkshell4 => ChatType.ExtraChatLinkshell4,
        InputChannel.ExtraChatLinkshell5 => ChatType.ExtraChatLinkshell5,
        InputChannel.ExtraChatLinkshell6 => ChatType.ExtraChatLinkshell6,
        InputChannel.ExtraChatLinkshell7 => ChatType.ExtraChatLinkshell7,
        InputChannel.ExtraChatLinkshell8 => ChatType.ExtraChatLinkshell8,
        _ => throw new ArgumentOutOfRangeException(nameof(input), input, null),
    };

    public static uint LinkshellIndex(this InputChannel channel) => channel switch
    {
        InputChannel.Linkshell1 => 0,
        InputChannel.Linkshell2 => 1,
        InputChannel.Linkshell3 => 2,
        InputChannel.Linkshell4 => 3,
        InputChannel.Linkshell5 => 4,
        InputChannel.Linkshell6 => 5,
        InputChannel.Linkshell7 => 6,
        InputChannel.Linkshell8 => 7,
        InputChannel.CrossLinkshell1 => 0,
        InputChannel.CrossLinkshell2 => 1,
        InputChannel.CrossLinkshell3 => 2,
        InputChannel.CrossLinkshell4 => 3,
        InputChannel.CrossLinkshell5 => 4,
        InputChannel.CrossLinkshell6 => 5,
        InputChannel.CrossLinkshell7 => 6,
        InputChannel.CrossLinkshell8 => 7,
        InputChannel.ExtraChatLinkshell1 => 0,
        InputChannel.ExtraChatLinkshell2 => 1,
        InputChannel.ExtraChatLinkshell3 => 2,
        InputChannel.ExtraChatLinkshell4 => 3,
        InputChannel.ExtraChatLinkshell5 => 4,
        InputChannel.ExtraChatLinkshell6 => 5,
        InputChannel.ExtraChatLinkshell7 => 6,
        InputChannel.ExtraChatLinkshell8 => 7,
        _ => uint.MaxValue,
    };

    public static string Prefix(this InputChannel channel) => channel switch
    {
        InputChannel.Tell => "/tell",
        InputChannel.Say => "/say",
        InputChannel.Party => "/party",
        InputChannel.Alliance => "/alliance",
        InputChannel.Yell => "/yell",
        InputChannel.Shout => "/shout",
        InputChannel.FreeCompany => "/freecompany",
        InputChannel.PvpTeam => "/pvpteam",
        InputChannel.NoviceNetwork => "/beginner",
        InputChannel.CrossLinkshell1 => "/cwlinkshell1",
        InputChannel.CrossLinkshell2 => "/cwlinkshell2",
        InputChannel.CrossLinkshell3 => "/cwlinkshell3",
        InputChannel.CrossLinkshell4 => "/cwlinkshell4",
        InputChannel.CrossLinkshell5 => "/cwlinkshell5",
        InputChannel.CrossLinkshell6 => "/cwlinkshell6",
        InputChannel.CrossLinkshell7 => "/cwlinkshell7",
        InputChannel.CrossLinkshell8 => "/cwlinkshell8",
        InputChannel.Linkshell1 => "/linkshell1",
        InputChannel.Linkshell2 => "/linkshell2",
        InputChannel.Linkshell3 => "/linkshell3",
        InputChannel.Linkshell4 => "/linkshell4",
        InputChannel.Linkshell5 => "/linkshell5",
        InputChannel.Linkshell6 => "/linkshell6",
        InputChannel.Linkshell7 => "/linkshell7",
        InputChannel.Linkshell8 => "/linkshell8",
        InputChannel.ExtraChatLinkshell1 => "/ecl1",
        InputChannel.ExtraChatLinkshell2 => "/ecl2",
        InputChannel.ExtraChatLinkshell3 => "/ecl3",
        InputChannel.ExtraChatLinkshell4 => "/ecl4",
        InputChannel.ExtraChatLinkshell5 => "/ecl5",
        InputChannel.ExtraChatLinkshell6 => "/ecl6",
        InputChannel.ExtraChatLinkshell7 => "/ecl7",
        InputChannel.ExtraChatLinkshell8 => "/ecl8",
        _ => "",
    };

    public static IEnumerable<TextCommand>? TextCommands(this InputChannel channel, IDataManager data)
    {
        var ids = channel switch
        {
            InputChannel.Tell => [104, 118],
            InputChannel.Say => [102],
            InputChannel.Party => [105],
            InputChannel.Alliance => [119],
            InputChannel.Yell => [117],
            InputChannel.Shout => [103],
            InputChannel.FreeCompany => [115],
            InputChannel.PvpTeam => [91],
            InputChannel.NoviceNetwork => [101],
            InputChannel.CrossLinkshell1 => [13],
            InputChannel.CrossLinkshell2 => [14],
            InputChannel.CrossLinkshell3 => [15],
            InputChannel.CrossLinkshell4 => [16],
            InputChannel.CrossLinkshell5 => [17],
            InputChannel.CrossLinkshell6 => [18],
            InputChannel.CrossLinkshell7 => [19],
            InputChannel.CrossLinkshell8 => [20],
            InputChannel.Linkshell1 => [107],
            InputChannel.Linkshell2 => [108],
            InputChannel.Linkshell3 => [109],
            InputChannel.Linkshell4 => [110],
            InputChannel.Linkshell5 => [111],
            InputChannel.Linkshell6 => [112],
            InputChannel.Linkshell7 => [113],
            InputChannel.Linkshell8 => [114],
            _ => Array.Empty<uint>(),
        };

        if (ids.Length == 0)
            return null;

        var cmds = data.GetExcelSheet<TextCommand>();
        return cmds == null ? null : ids.Select(id => cmds.GetRow(id)).Where(id => id != null).Cast<TextCommand>();
    }

    internal static bool IsLinkshell(this InputChannel channel) => channel switch
    {
        InputChannel.Linkshell1 => true,
        InputChannel.Linkshell2 => true,
        InputChannel.Linkshell3 => true,
        InputChannel.Linkshell4 => true,
        InputChannel.Linkshell5 => true,
        InputChannel.Linkshell6 => true,
        InputChannel.Linkshell7 => true,
        InputChannel.Linkshell8 => true,
        _ => false,
    };

    internal static bool IsCrossLinkshell(this InputChannel channel) => channel switch
    {
        InputChannel.CrossLinkshell1 => true,
        InputChannel.CrossLinkshell2 => true,
        InputChannel.CrossLinkshell3 => true,
        InputChannel.CrossLinkshell4 => true,
        InputChannel.CrossLinkshell5 => true,
        InputChannel.CrossLinkshell6 => true,
        InputChannel.CrossLinkshell7 => true,
        InputChannel.CrossLinkshell8 => true,
        _ => false,
    };

    internal static bool IsExtraChatLinkshell(this InputChannel channel) => channel switch
    {
        InputChannel.ExtraChatLinkshell1 => true,
        InputChannel.ExtraChatLinkshell2 => true,
        InputChannel.ExtraChatLinkshell3 => true,
        InputChannel.ExtraChatLinkshell4 => true,
        InputChannel.ExtraChatLinkshell5 => true,
        InputChannel.ExtraChatLinkshell6 => true,
        InputChannel.ExtraChatLinkshell7 => true,
        InputChannel.ExtraChatLinkshell8 => true,
        _ => false,
    };
}
