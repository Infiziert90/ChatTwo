using Newtonsoft.Json;

namespace ChatTwo.Http.MessageProtocol;

#region Outgoing SSE
/// <summary>
/// Contains the current channel name
/// </summary>
public struct SwitchChannel(MessageTemplate[] channelName)
{
    [JsonProperty("channelName")] public MessageTemplate[] ChannelName = channelName;
}

/// <summary>
/// Contains one or multiple channels that are valid for the user to pick from
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
    /// empty = Ignore
    /// </summary>
    [JsonProperty("payload")] public required string Payload;

    /// <summary>
    /// Used for text and emote.
    /// </summary>
    [JsonProperty("content")] public string Content = "";

    /// <summary>
    /// Used for icon.
    /// </summary>
    [JsonProperty("id")] public uint Id;

    /// <summary>
    /// Used for text and url
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
/// Channel must be a valid uint number
/// </summary>
public struct IncomingChannel()
{
    [JsonProperty("channel")] public uint Channel = uint.MaxValue;
}
#endregion