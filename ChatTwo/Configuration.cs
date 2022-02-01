using ChatTwo.Code;
using Dalamud.Configuration;

namespace ChatTwo;

[Serializable]
internal class Configuration : IPluginConfiguration {
    public int Version { get; set; } = 1;

    public bool HideChat = true;
    public bool HideDuringCutscenes = true;
    public bool NativeItemTooltips = true;
    public bool PrettierTimestamps = true;
    public bool MoreCompactPretty;
    public bool SidebarTabView;
    public bool CanMove = true;
    public bool CanResize = true;
    public bool ShowTitleBar;
    public float FontSize = 17f;
    public float WindowAlpha = 1f;
    public Dictionary<ChatType, uint> ChatColours = new();
    public List<Tab> Tabs = new();

    internal void UpdateFrom(Configuration other) {
        this.HideChat = other.HideChat;
        this.NativeItemTooltips = other.NativeItemTooltips;
        this.PrettierTimestamps = other.PrettierTimestamps;
        this.MoreCompactPretty = other.MoreCompactPretty;
        this.SidebarTabView = other.SidebarTabView;
        this.CanMove = other.CanMove;
        this.CanResize = other.CanResize;
        this.ShowTitleBar = other.ShowTitleBar;
        this.FontSize = other.FontSize;
        this.WindowAlpha = other.WindowAlpha;
        this.ChatColours = other.ChatColours.ToDictionary(entry => entry.Key, entry => entry.Value);
        this.Tabs = other.Tabs.Select(t => t.Clone()).ToList();
    }
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

    internal void AddMessage(Message message, bool unread = true) {
        this.MessagesMutex.WaitOne();
        this.Messages.Add(message);
        while (this.Messages.Count > Store.MessagesLimit) {
            this.Messages.RemoveAt(0);
        }

        this.MessagesMutex.ReleaseMutex();

        if (unread) {
            this.Unread += 1;
        }
    }

    internal void Clear() {
        this.MessagesMutex.WaitOne();
        this.Messages.Clear();
        this.MessagesMutex.ReleaseMutex();
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
