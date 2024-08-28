using Newtonsoft.Json;

namespace ChatTwo.Http.MessageProtocol;

#region Outgoing SSE
public struct SwitchChannel(string name)
{
    [JsonProperty("channel")] public string Name = name;
}

public struct ChannelList(Dictionary<string, uint> channels)
{
    [JsonProperty("channels")] public Dictionary<string, uint> Channels = channels;
}

public struct Messages(MessageResponse[] set)
{
    [JsonProperty("messages")] public MessageResponse[] Set = set;
}

public struct MessageResponse()
{
    [JsonProperty("timestamp")] public string Timestamp = "";
    [JsonProperty("messageHTML")] public string Message = "";
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
public struct IncomingMessage()
{
    [JsonProperty("message")] public string Message = string.Empty;
}

public struct IncomingChannel()
{
    [JsonProperty("channel")] public uint Channel = uint.MaxValue;
}
#endregion