using Newtonsoft.Json;

namespace ChatTwo.Http.MessageProtocol;

#region Outgoing
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

#region Incoming
public struct IncomingMessage()
{
    [JsonProperty("message")] public string Message = string.Empty;
}

public struct IncomingChannel()
{
    [JsonProperty("channel")] public uint Channel = uint.MaxValue;
}
#endregion