using System.Text;
using Newtonsoft.Json;

namespace ChatTwo.Http.MessageProtocol;

// General
public class CloseEvent() : BaseEvent("close");

// Tab related
public class ChatTabListEvent(ChatTabList list) : BaseEvent("tab-list", JsonConvert.SerializeObject(list));
public class ChatTabSwitchedEvent(ChatTab chatTab) : BaseEvent("tab-switched", JsonConvert.SerializeObject(chatTab));
public class ChatTabUnreadStateEvent(ChatTabUnreadState unreadState) : BaseEvent("tab-unread-state", JsonConvert.SerializeObject(unreadState));

// Input channel related
public class ChannelListEvent(ChannelList channelList) : BaseEvent("channel-list", JsonConvert.SerializeObject(channelList));
public class SwitchChannelEvent(SwitchChannel switchChannel) : BaseEvent("channel-switched", JsonConvert.SerializeObject(switchChannel));

// Chat message related
public class BulkMessagesEvent(Messages messages) : BaseEvent("bulk-messages", JsonConvert.SerializeObject(messages));
public class NewMessageEvent(MessageResponse message) : BaseEvent("new-message", JsonConvert.SerializeObject(message));

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