using ChatTwo.Code;
using Newtonsoft.Json;

namespace ChatTwo.Http.MessageProtocol;

#region Outgoing SSE
/// <summary>
/// Contains a valid tab with its assigned index
/// </summary>
public struct ChatTab(string name, int index, uint unreadCount)
{
    [JsonProperty("name")] public string Name = name;
    [JsonProperty("index")] public int Index = index;
    [JsonProperty("unreadCount")] public uint UnreadCount = unreadCount;
}

/// <summary>
/// Contains a number of tabs that are valid for the user to pick from
/// </summary>
public struct ChatTabList(ChatTab[] tabs)
{
    [JsonProperty("tabs")] public ChatTab[] Tabs = tabs;
}

/// <summary>
/// Contains a valid tab index and the current unread state as a number unread of messages
/// </summary>
public struct ChatTabUnreadState(int index, uint unreadCount)
{
    [JsonProperty("index")] public int Index = index;
    [JsonProperty("unreadCount")] public uint UnreadCount = unreadCount;
}

/// <summary>
/// Contains the current channel name
/// </summary>
public struct SwitchChannel((MessageTemplate[] Name, bool Locked) channel)
{
    [JsonProperty("channelName")] public MessageTemplate[] ChannelName = channel.Name;
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
    [JsonProperty("id")] public Guid Id = Guid.Empty;
    [JsonProperty("timestamp")] public string Timestamp = "";
    [JsonProperty("templates")] public MessageTemplate[] Templates;
}

/// <summary>
/// Template that is used for the channel name or any message posted to the chatlog
/// </summary>
public struct MessageTemplate()
{
    /// <summary>
    /// The type of payload.
    /// Dalamuds enum is just a baseline, there exists more that are expressed through raw values.
    /// </summary>
    [JsonProperty("payloadType")] public WebPayloadType PayloadType = WebPayloadType.Unknown;

    /// <summary>
    /// Used for text and emote.
    /// </summary>
    [JsonProperty("content")] public string Content = "";

    /// <summary>
    /// Used for an icon.
    /// </summary>
    [JsonProperty("iconId")] public uint IconId;

    /// <summary>
    /// Used for text and url
    ///
    /// Note:
    /// 0 is used for invalid colors
    /// </summary>
    [JsonProperty("color")] public uint Color;

    public static MessageTemplate Empty => new();
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