using ChatTwo.Code;
using ChatTwo.Resources;
using ChatTwo.Ui;
using Dalamud.Configuration;
using Dalamud.Logging;
using ImGuiNET;

namespace ChatTwo;

[Serializable]
internal class Configuration : IPluginConfiguration {
    private const int LatestVersion = 5;
    internal const int LatestDbVersion = 1;

    public int Version { get; set; } = LatestVersion;

    public bool HideChat = true;
    public bool HideDuringCutscenes = true;
    public bool HideWhenNotLoggedIn = true;
    public bool HideWhenUiHidden = true;
    public bool NativeItemTooltips = true;
    public bool PrettierTimestamps = true;
    public bool MoreCompactPretty;
    public bool HideSameTimestamps;
    public bool ShowNoviceNetwork;
    public bool SidebarTabView;
    public CommandHelpSide CommandHelpSide = CommandHelpSide.None;
    public KeybindMode KeybindMode = KeybindMode.Strict;
    public LanguageOverride LanguageOverride = LanguageOverride.None;
    public bool CanMove = true;
    public bool CanResize = true;
    public bool ShowTitleBar;
    public bool ShowPopOutTitleBar = true;
    public bool DatabaseBattleMessages;
    public bool LoadPreviousSession;
    public bool FilterIncludePreviousSessions;
    public bool SharedMode;
    public bool SortAutoTranslate;
    public bool CollapseDuplicateMessages;

    public bool FontsEnabled = true;
    public ExtraGlyphRanges ExtraGlyphRanges = 0;
    public float FontSize = 17f;
    public float JapaneseFontSize = 17f;
    public float SymbolsFontSize = 17f;
    public string GlobalFont = Fonts.GlobalFonts[0].Name;
    public string JapaneseFont = Fonts.JapaneseFonts[0].Item1;

    public float WindowAlpha = 100f;
    public Dictionary<ChatType, uint> ChatColours = new();
    public List<Tab> Tabs = new();

    public uint DatabaseMigration = LatestDbVersion;

    internal void UpdateFrom(Configuration other) {
        this.HideChat = other.HideChat;
        this.HideDuringCutscenes = other.HideDuringCutscenes;
        this.HideWhenNotLoggedIn = other.HideWhenNotLoggedIn;
        this.HideWhenUiHidden = other.HideWhenUiHidden;
        this.NativeItemTooltips = other.NativeItemTooltips;
        this.PrettierTimestamps = other.PrettierTimestamps;
        this.MoreCompactPretty = other.MoreCompactPretty;
        this.HideSameTimestamps = other.HideSameTimestamps;
        this.ShowNoviceNetwork = other.ShowNoviceNetwork;
        this.SidebarTabView = other.SidebarTabView;
        this.CommandHelpSide = other.CommandHelpSide;
        this.KeybindMode = other.KeybindMode;
        this.LanguageOverride = other.LanguageOverride;
        this.CanMove = other.CanMove;
        this.CanResize = other.CanResize;
        this.ShowTitleBar = other.ShowTitleBar;
        this.ShowPopOutTitleBar = other.ShowPopOutTitleBar;
        this.DatabaseBattleMessages = other.DatabaseBattleMessages;
        this.LoadPreviousSession = other.LoadPreviousSession;
        this.FilterIncludePreviousSessions = other.FilterIncludePreviousSessions;
        this.SharedMode = other.SharedMode;
        this.SortAutoTranslate = other.SortAutoTranslate;
        this.CollapseDuplicateMessages = other.CollapseDuplicateMessages;
        this.FontsEnabled = other.FontsEnabled;
        this.ExtraGlyphRanges = other.ExtraGlyphRanges;
        this.FontSize = other.FontSize;
        this.JapaneseFontSize = other.JapaneseFontSize;
        this.SymbolsFontSize = other.SymbolsFontSize;
        this.GlobalFont = other.GlobalFont;
        this.JapaneseFont = other.JapaneseFont;
        this.WindowAlpha = other.WindowAlpha;
        this.ChatColours = other.ChatColours.ToDictionary(entry => entry.Key, entry => entry.Value);
        this.Tabs = other.Tabs.Select(t => t.Clone()).ToList();
        this.DatabaseMigration = other.DatabaseMigration;
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
                case 3:
                    this.Version = 4;

                    this.WindowAlpha *= 100f;
                    break;
                case 4:
                    this.Version = 5;

                    foreach (var tab in this.Tabs) {
                        tab.ExtraChatAll = true;
                    }

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
    public bool ExtraChatAll;
    public HashSet<Guid> ExtraChatChannels = new();

    [Obsolete("Use UnreadMode instead")]
    public bool DisplayUnread = true;

    public UnreadMode UnreadMode = UnreadMode.Unseen;
    public bool DisplayTimestamp = true;
    public InputChannel? Channel;
    public bool PopOut;
    public bool IndependentOpacity;
    public float Opacity = 100f;

    [NonSerialized]
    public uint Unread;

    [NonSerialized]
    public SemaphoreSlim MessagesMutex = new(1, 1);

    [NonSerialized]
    public List<Message> Messages = new();

    ~Tab() {
        this.MessagesMutex.Dispose();
    }

    internal bool Matches(Message message) {
        if (message.ExtraChatChannel != Guid.Empty) {
            return this.ExtraChatAll || this.ExtraChatChannels.Contains(message.ExtraChatChannel);
        }

        return message.Code.Type.IsGm()
               || this.ChatCodes.TryGetValue(message.Code.Type, out var sources) && (message.Code.Source is 0 or (ChatSource) 1 || sources.HasFlag(message.Code.Source));
    }

    internal void AddMessage(Message message, bool unread = true) {
        this.MessagesMutex.Wait();
        this.Messages.Add(message);
        while (this.Messages.Count > Store.MessagesLimit) {
            this.Messages.RemoveAt(0);
        }

        this.MessagesMutex.Release();

        if (unread) {
            this.Unread += 1;
        }
    }

    internal void Clear() {
        this.MessagesMutex.Wait();
        this.Messages.Clear();
        this.MessagesMutex.Release();
    }

    internal Tab Clone() {
        return new Tab {
            Name = this.Name,
            ChatCodes = this.ChatCodes.ToDictionary(entry => entry.Key, entry => entry.Value),
            ExtraChatAll = this.ExtraChatAll,
            ExtraChatChannels = this.ExtraChatChannels.ToHashSet(),
            #pragma warning disable CS0618
            DisplayUnread = this.DisplayUnread,
            #pragma warning restore CS0618
            UnreadMode = this.UnreadMode,
            DisplayTimestamp = this.DisplayTimestamp,
            Channel = this.Channel,
            PopOut = this.PopOut,
            IndependentOpacity = this.IndependentOpacity,
            Opacity = this.Opacity,
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

[Serializable]
internal enum KeybindMode {
    Flexible,
    Strict,
}

internal static class KeybindModeExt {
    internal static string Name(this KeybindMode mode) => mode switch {
        KeybindMode.Flexible => Language.KeybindMode_Flexible_Name,
        KeybindMode.Strict => Language.KeybindMode_Strict_Name,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    internal static string? Tooltip(this KeybindMode mode) => mode switch {
        KeybindMode.Flexible => Language.KeybindMode_Flexible_Tooltip,
        KeybindMode.Strict => Language.KeybindMode_Strict_Tooltip,
        _ => null,
    };
}

[Serializable]
internal enum LanguageOverride {
    None,
    ChineseSimplified,
    ChineseTraditional,
    Dutch,
    English,
    French,
    German,
    Greek,

    // Italian,
    Japanese,

    // Korean,
    // Norwegian,
    PortugueseBrazil,
    Romanian,
    Russian,
    Spanish,
    Swedish,
}

internal static class LanguageOverrideExt {
    internal static string Name(this LanguageOverride mode) => mode switch {
        LanguageOverride.None => Language.LanguageOverride_None,
        LanguageOverride.ChineseSimplified => "简体中文",
        LanguageOverride.ChineseTraditional => "繁體中文",
        LanguageOverride.Dutch => "Nederlands",
        LanguageOverride.English => "English",
        LanguageOverride.French => "Français",
        LanguageOverride.German => "Deutsch",
        LanguageOverride.Greek => "Ελληνικά",
        // LanguageOverride.Italian => "Italiano",
        LanguageOverride.Japanese => "日本語",
        // LanguageOverride.Korean => "한국어 (Korean)",
        // LanguageOverride.Norwegian => "Norsk",
        LanguageOverride.PortugueseBrazil => "Português do Brasil",
        LanguageOverride.Romanian => "Română",
        LanguageOverride.Russian => "Русский",
        LanguageOverride.Spanish => "Español",
        LanguageOverride.Swedish => "Svenska",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    internal static string Code(this LanguageOverride mode) => mode switch {
        LanguageOverride.None => "",
        LanguageOverride.ChineseSimplified => "zh-hans",
        LanguageOverride.ChineseTraditional => "zh-hant",
        LanguageOverride.Dutch => "nl",
        LanguageOverride.English => "en",
        LanguageOverride.French => "fr",
        LanguageOverride.German => "de",
        LanguageOverride.Greek => "el",
        // LanguageOverride.Italian => "it",
        LanguageOverride.Japanese => "ja",
        // LanguageOverride.Korean => "ko",
        // LanguageOverride.Norwegian => "no",
        LanguageOverride.PortugueseBrazil => "pt-br",
        LanguageOverride.Romanian => "ro",
        LanguageOverride.Russian => "ru",
        LanguageOverride.Spanish => "es",
        LanguageOverride.Swedish => "sv",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };
}

[Serializable]
[Flags]
internal enum ExtraGlyphRanges {
    ChineseFull = 1 << 0,
    ChineseSimplifiedCommon = 1 << 1,
    Cyrillic = 1 << 2,
    Japanese = 1 << 3,
    Korean = 1 << 4,
    Thai = 1 << 5,
    Vietnamese = 1 << 6,
}

internal static class ExtraGlyphRangesExt {
    internal static string Name(this ExtraGlyphRanges ranges) => ranges switch {
        ExtraGlyphRanges.ChineseFull => Language.ExtraGlyphRanges_ChineseFull_Name,
        ExtraGlyphRanges.ChineseSimplifiedCommon => Language.ExtraGlyphRanges_ChineseSimplifiedCommon_Name,
        ExtraGlyphRanges.Cyrillic => Language.ExtraGlyphRanges_Cyrillic_Name,
        ExtraGlyphRanges.Japanese => Language.ExtraGlyphRanges_Japanese_Name,
        ExtraGlyphRanges.Korean => Language.ExtraGlyphRanges_Korean_Name,
        ExtraGlyphRanges.Thai => Language.ExtraGlyphRanges_Thai_Name,
        ExtraGlyphRanges.Vietnamese => Language.ExtraGlyphRanges_Vietnamese_Name,
        _ => throw new ArgumentOutOfRangeException(nameof(ranges), ranges, null),
    };

    internal static IntPtr Range(this ExtraGlyphRanges ranges) => ranges switch {
        ExtraGlyphRanges.ChineseFull => ImGui.GetIO().Fonts.GetGlyphRangesChineseFull(),
        ExtraGlyphRanges.ChineseSimplifiedCommon => ImGui.GetIO().Fonts.GetGlyphRangesChineseSimplifiedCommon(),
        ExtraGlyphRanges.Cyrillic => ImGui.GetIO().Fonts.GetGlyphRangesCyrillic(),
        ExtraGlyphRanges.Japanese => ImGui.GetIO().Fonts.GetGlyphRangesJapanese(),
        ExtraGlyphRanges.Korean => ImGui.GetIO().Fonts.GetGlyphRangesKorean(),
        ExtraGlyphRanges.Thai => ImGui.GetIO().Fonts.GetGlyphRangesThai(),
        ExtraGlyphRanges.Vietnamese => ImGui.GetIO().Fonts.GetGlyphRangesVietnamese(),
        _ => throw new ArgumentOutOfRangeException(nameof(ranges), ranges, null),
    };
}
