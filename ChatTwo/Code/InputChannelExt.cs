namespace ChatTwo.Code;

internal static class InputChannelExt {
    internal static ChatType ToChatType(this InputChannel input) {
        return input switch {
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
            _ => throw new ArgumentOutOfRangeException(nameof(input), input, null),
        };
    }
}
