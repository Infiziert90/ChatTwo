using Newtonsoft.Json;

namespace ChatTwo.Http.MessageProtocol;

public struct MessageResponse()
{
    [JsonProperty("timestamp")] public string Timestamp = "";
    [JsonProperty("messageHTML")] public string Message = "";
}

public class NewMessage(MessageResponse[] messages) : BaseMessage(MessageName)
{
    private const string MessageName = "chat-message";

    [JsonProperty("messages")] public MessageResponse[] Messages { get; set; } = messages;
}

public class BaseMessage(string messageType)
{
    [JsonProperty("messageType")] public string MessageType { get; set; } = messageType;
}