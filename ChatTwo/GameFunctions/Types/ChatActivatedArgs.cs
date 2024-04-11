namespace ChatTwo.GameFunctions.Types;

internal sealed class ChatActivatedArgs
{
    internal string? AddIfNotPresent { get; init; }
    internal string? Input { get; init; }
    internal ChannelSwitchInfo ChannelSwitchInfo { get; }
    internal TellReason? TellReason { get; init; }
    internal TellTarget? TellTarget { get; init; }

    internal ChatActivatedArgs(ChannelSwitchInfo channelSwitchInfo)
    {
        ChannelSwitchInfo = channelSwitchInfo;
    }
}
