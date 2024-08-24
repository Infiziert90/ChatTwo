using System.Text;
using Newtonsoft.Json;

namespace ChatTwo.Http.MessageProtocol;

public class CloseEvent() : BaseEvent("close");

public class ChannelListEvent(ChannelList channelList) : BaseEvent("channel-list", JsonConvert.SerializeObject(channelList));

public class SwitchChannelEvent(SwitchChannel switchChannel) : BaseEvent("switch-channel", JsonConvert.SerializeObject(switchChannel));

public class NewMessageEvent(Messages messages) : BaseEvent("new-message", JsonConvert.SerializeObject(messages));

public class BaseEvent(string eventType, string? data = null)
{
    private string Event = eventType;
    private string Data = data ?? "0"; // SSE requires data on each response

    public byte[] Build()
    {
        // SSE always ends with \n\n
        return Encoding.UTF8.GetBytes($"event: {Event}\ndata: {Data}\n\n");
    }
}