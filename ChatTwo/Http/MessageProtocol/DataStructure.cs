using ChatTwo.Code;
using Newtonsoft.Json;

namespace ChatTwo.Http.MessageProtocol;

#region Outgoing SSE
/// <summary>
/// Contains a valid tab with its assigned index
/// </summary>
public struct ChatTab(string name, int index)
{
    [JsonProperty("name")] public string Name = name;
    [JsonProperty("index")] public int Index = index;
}

/// <summary>
/// Contains a number of tabs that are valid for the user to pick from
/// </summary>
public struct ChatTabList(ChatTab[] tabs)
{
    [JsonProperty("tabs")] public ChatTab[] Tabs = tabs;
}

/// <summary>
/// Contains the current channel name
/// </summary>
public struct SwitchChannel((MessageTemplate[] ChannelName, bool Locked) channel)
{
    [JsonProperty("channelName")] public MessageTemplate[] ChannelName = channel.ChannelName;
    [JsonProperty("channelLocked")] public bool Locked = channel.Locked;
}

/// <summary>
/// Contains a number of channels that are valid for the user to pick from
/// </summary>
public struct ChannelList(Dictionary<string, uint> channels)
{
    [JsonProperty("channels")] public Dictionary<string, uint> Channels = channels;
}

/// <summary>
/// Contains one or multiple messages
/// </summary>
public struct Messages(MessageResponse[] set)
{
    [JsonProperty("messages")] public MessageResponse[] Set = set;
}

/// <summary>
/// Contains a single message with all its templates and a timestamp
/// </summary>
public struct MessageResponse()
{
    [JsonProperty("timestamp")] public string Timestamp = "";
    [JsonProperty("templates")] public MessageTemplate[] Templates;
}

/// <summary>
/// Template that is used for the channel name or any message posted to the chatlog
/// </summary>
public struct MessageTemplate()
{
    /// <summary>
    /// Template type
    ///
    /// icon = a game icon
    /// emote = BetterTTV emote
    /// url = Simple url that should be clickable
    /// text = Simple text content of the message
    ///
    /// Note:
    /// Empty is used for invalid payloads
    /// </summary>
    [JsonProperty("payload")] public required string Payload;

    /// <summary>
    /// Used for text and emote.
    /// </summary>
    [JsonProperty("content")] public string Content = "";

    /// <summary>
    /// Used for an icon.
    /// </summary>
    [JsonProperty("id")] public uint Id;

    /// <summary>
    /// Used for text and url
    ///
    /// Note:
    /// 0 is used for invalid colors
    /// </summary>
    [JsonProperty("color")] public uint Color;

    public static MessageTemplate Empty => new() {Payload = "empty"};
}
#endregion

#region Outgoing POST
public struct OkResponse(string message)
{
    [JsonProperty("message")] public string Message = message;
}

public struct ErrorResponse(string reason)
{
    [JsonProperty("reason")] public string Reason = reason;
}
#endregion

#region Incoming POST
/// <summary>
/// Message must fulfill the posting requirement
/// Greater than or equal 2 characters
/// Less than or equal 500 characters
/// </summary>
public struct IncomingMessage()
{
    [JsonProperty("message")] public string Message = string.Empty;
}

/// <summary>
/// The channel type must be a valid <see cref="InputChannel"/>
/// </summary>
public struct IncomingChannel()
{
    [JsonProperty("channel")] public InputChannel Channel = InputChannel.Invalid;
}

/// <summary>
/// The tabs index must be a valid int
/// </summary>
public struct IncomingTab()
{
    [JsonProperty("index")] public int Index = -1;
}
#endregion