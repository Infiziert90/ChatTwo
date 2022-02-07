using ChatTwo.Code;
using ChatTwo.Resources;
using ChatTwo.Ui;
using Dalamud.Configuration;
using Dalamud.Logging;

namespace ChatTwo;

[Serializable]
internal class Configuration : IPluginConfiguration {
    private const int LatestVersion = 3;

    public int Version { get; set; } = LatestVersion;

    public bool HideChat = true;
    public bool HideDuringCutscenes = true;
    public bool HideWhenNotLoggedIn = true;
    public bool HideWhenUiHidden = true;
    public bool NativeItemTooltips = true;
    public bool PrettierTimestamps = true;
    public bool MoreCompactPretty;
    public bool ShowNoviceNetwork;
    public bool SidebarTabView;
    public CommandHelpSide CommandHelpSide = CommandHelpSide.None;
    public bool CanMove = true;
    public bool CanResize = true;
    public bool ShowTitleBar;

    public float FontSize = 17f;
    public float JapaneseFontSize = 17f;
    public float SymbolsFontSize = 17f;
    public string GlobalFont = Fonts.GlobalFonts[0].Name;
    public string JapaneseFont = Fonts.JapaneseFonts[0].Item1;

    public float WindowAlpha = 1f;
    public Dictionary<ChatType, uint> ChatColours = new();
    public List<Tab> Tabs = new();

    internal void UpdateFrom(Configuration other) {
        this.HideChat = other.HideChat;
        this.HideDuringCutscenes = other.HideDuringCutscenes;
        this.HideWhenNotLoggedIn = other.HideWhenNotLoggedIn;
        this.HideWhenUiHidden = other.HideWhenUiHidden;
        this.NativeItemTooltips = other.NativeItemTooltips;
        this.PrettierTimestamps = other.PrettierTimestamps;
        this.MoreCompactPretty = other.MoreCompactPretty;
        this.ShowNoviceNetwork = other.ShowNoviceNetwork;
        this.SidebarTabView = other.SidebarTabView;
        this.CommandHelpSide = other.CommandHelpSide;
        this.CanMove = other.CanMove;
        this.CanResize = other.CanResize;
        this.ShowTitleBar = other.ShowTitleBar;
        this.FontSize = other.FontSize;
        this.JapaneseFontSize = other.JapaneseFontSize;
        this.SymbolsFontSize = other.SymbolsFontSize;
        this.GlobalFont = other.GlobalFont;
        this.JapaneseFont = other.JapaneseFont;
        this.WindowAlpha = other.WindowAlpha;
        this.ChatColours = other.ChatColours.ToDictionary(entry => entry.Key, entry => entry.Value);
        this.Tabs = other.Tabs.Select(t => t.Clone()).ToList();
    }

    public void Migrate() {
        var loop = true;
        while (loop && this.Version < LatestVersion) {
            switch (this.Version) {
                case 1: {
                    this.Version = 2;

                    foreach (var tab in this.Tabs) {
                        #pragma warning disable CS0618
                        tab.UnreadMode = tab.DisplayUnread ? UnreadMode.Unseen : UnreadMode.None;
                        #pragma warning restore CS0618
                    }

                    break;
                }
                case 2:
                    this.Version = 3;

                    this.JapaneseFontSize = this.FontSize;
                    this.SymbolsFontSize = this.FontSize;
                    break;
                default:
                    PluginLog.Warning($"Couldn't migrate config version {this.Version}");
                    loop = false;
                    break;
            }
        }
    }
}

[Serializable]
internal enum UnreadMode {
    All,
    Unseen,
    None,
}

internal static class UnreadModeExt {
    internal static string Name(this UnreadMode mode) => mode switch {
        UnreadMode.All => Language.UnreadMode_All,
        UnreadMode.Unseen => Language.UnreadMode_Unseen,
        UnreadMode.None => Language.UnreadMode_None,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    internal static string? Tooltip(this UnreadMode mode) => mode switch {
        UnreadMode.All => Language.UnreadMode_All_Tooltip,
        UnreadMode.Unseen => Language.UnreadMode_Unseen_Tooltip,
        UnreadMode.None => Language.UnreadMode_None_Tooltip,
        _ => null,
    };
}

[Serializable]
internal class Tab {
    public string Name = Language.Tab_DefaultName;
    public Dictionary<ChatType, ChatSource> ChatCodes = new();

    [Obsolete("Use UnreadMode instead")]
    public bool DisplayUnread = true;

    public UnreadMode UnreadMode = UnreadMode.Unseen;
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
        return message.Code.Type.IsGm() || this.ChatCodes.TryGetValue(message.Code.Type, out var sources) && (message.Code.Source is 0 or (ChatSource) 1 || sources.HasFlag(message.Code.Source));
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
            #pragma warning disable CS0618
            DisplayUnread = this.DisplayUnread,
            #pragma warning restore CS0618
            UnreadMode = this.UnreadMode,
            DisplayTimestamp = this.DisplayTimestamp,
            Channel = this.Channel,
        };
    }
}

[Serializable]
internal enum CommandHelpSide {
    None,
    Left,
    Right,
}

internal static class CommandHelpSideExt {
    internal static string Name(this CommandHelpSide side) => side switch {
        CommandHelpSide.None => Language.CommandHelpSide_None,
        CommandHelpSide.Left => Language.CommandHelpSide_Left,
        CommandHelpSide.Right => Language.CommandHelpSide_Right,
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, null),
    };
}
