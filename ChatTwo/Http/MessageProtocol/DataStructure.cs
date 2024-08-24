using Newtonsoft.Json;

namespace ChatTwo.Http.MessageProtocol;

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