using ChatTwo.Code;
using Dalamud.Configuration;

namespace ChatTwo;

[Serializable]
internal class Configuration : IPluginConfiguration {
    public int Version { get; set; } = 1;

    public bool HideChat = true;
    public bool NativeItemTooltips = true;
    public bool SidebarTabView;
    public float FontSize = 17f;
    public Dictionary<ChatType, uint> ChatColours = new();
    public List<Tab> Tabs = new();
}

[Serializable]
internal class Tab {
    public string Name = "New tab";
    public Dictionary<ChatType, ChatSource> ChatCodes = new();
    public bool DisplayUnread = true;
    public bool DisplayTimestamp = true;
    public InputChannel? Channel;

    [NonSerialized]
    public uint Unread;

    [NonSerialized]
    public Mutex MessagesMutex = new();

    [NonSerialized]
    public List<Message> Messages = new();

    ~Tab() {
        this.MessagesMutex.Dispose();
    }

    internal bool Matches(Message message) {
        return this.ChatCodes.TryGetValue(message.Code.Type, out var sources) && (message.Code.Source is 0 or (ChatSource) 1 || sources.HasFlag(message.Code.Source));
    }

    internal void AddMessage(Message message) {
        this.MessagesMutex.WaitOne();
        this.Messages.Add(message);
        if (this.Messages.Count > 1000) {
            this.Messages.RemoveAt(0);
        }

        this.MessagesMutex.ReleaseMutex();

        this.Unread += 1;
    }

    internal Tab Clone() {
        return new Tab {
            Name = this.Name,
            ChatCodes = this.ChatCodes.ToDictionary(entry => entry.Key, entry => entry.Value),
            DisplayUnread = this.DisplayUnread,
            DisplayTimestamp = this.DisplayTimestamp,
            Channel = this.Channel,
        };
    }
}
