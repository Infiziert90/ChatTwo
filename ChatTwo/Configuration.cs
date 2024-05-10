using ChatTwo.Code;
using ChatTwo.Resources;
using ChatTwo.Ui;
using Dalamud.Configuration;
using ImGuiNET;

namespace ChatTwo;

[Serializable]
internal class Configuration : IPluginConfiguration
{
    private const int LatestVersion = 5;

    public int Version { get; set; } = LatestVersion;

    public bool HideChat = true;
    public bool HideDuringCutscenes = true;
    public bool HideWhenNotLoggedIn = true;
    public bool HideWhenUiHidden = true;
    public bool HideInLoadingScreens;
    public bool HideInBattle;
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
    public bool SortAutoTranslate;
    public bool CollapseDuplicateMessages;
    public bool PlaySounds = true;
    public bool KeepInputFocus = true;
    public int MaxLinesToRender = 10_000;

    public bool ShowEmotes = true;
    public HashSet<string> BlockedEmotes = [];

    public bool FontsEnabled = true;
    public ExtraGlyphRanges ExtraGlyphRanges = 0;
    public float FontSize = 17f;
    public float JapaneseFontSize = 17f;
    public float SymbolsFontSize = 17f;
    public string GlobalFont = Fonts.GlobalFonts[0].Name;
    public string JapaneseFont = Fonts.JapaneseFonts[0].Item1;

    public float TooltipOffset;
    public float WindowAlpha = 100f;
    public Dictionary<ChatType, uint> ChatColours = new();
    public List<Tab> Tabs = new();

    public bool OverrideStyle;
    public string? ChosenStyle;

    internal void UpdateFrom(Configuration other, bool backToOriginal)
    {
        if (backToOriginal)
            foreach (var tab in Tabs.Where(t => t.PopOut))
                tab.PopOut = false;

        HideChat = other.HideChat;
        HideDuringCutscenes = other.HideDuringCutscenes;
        HideWhenNotLoggedIn = other.HideWhenNotLoggedIn;
        HideWhenUiHidden = other.HideWhenUiHidden;
        HideInLoadingScreens = other.HideInLoadingScreens;
        HideInBattle = other.HideInBattle;
        NativeItemTooltips = other.NativeItemTooltips;
        PrettierTimestamps = other.PrettierTimestamps;
        MoreCompactPretty = other.MoreCompactPretty;
        HideSameTimestamps = other.HideSameTimestamps;
        ShowNoviceNetwork = other.ShowNoviceNetwork;
        SidebarTabView = other.SidebarTabView;
        CommandHelpSide = other.CommandHelpSide;
        KeybindMode = other.KeybindMode;
        LanguageOverride = other.LanguageOverride;
        CanMove = other.CanMove;
        CanResize = other.CanResize;
        ShowTitleBar = other.ShowTitleBar;
        ShowPopOutTitleBar = other.ShowPopOutTitleBar;
        DatabaseBattleMessages = other.DatabaseBattleMessages;
        LoadPreviousSession = other.LoadPreviousSession;
        FilterIncludePreviousSessions = other.FilterIncludePreviousSessions;
        SortAutoTranslate = other.SortAutoTranslate;
        CollapseDuplicateMessages = other.CollapseDuplicateMessages;
        PlaySounds = other.PlaySounds;
        KeepInputFocus = other.KeepInputFocus;
        MaxLinesToRender = other.MaxLinesToRender;
        ShowEmotes = other.ShowEmotes;
        BlockedEmotes = other.BlockedEmotes;
        FontsEnabled = other.FontsEnabled;
        ExtraGlyphRanges = other.ExtraGlyphRanges;
        FontSize = other.FontSize;
        JapaneseFontSize = other.JapaneseFontSize;
        SymbolsFontSize = other.SymbolsFontSize;
        GlobalFont = other.GlobalFont;
        JapaneseFont = other.JapaneseFont;
        TooltipOffset = other.TooltipOffset;
        WindowAlpha = other.WindowAlpha;
        ChatColours = other.ChatColours.ToDictionary(entry => entry.Key, entry => entry.Value);
        Tabs = other.Tabs.Select(t => t.Clone()).ToList();
        OverrideStyle = other.OverrideStyle;
        ChosenStyle = other.ChosenStyle;
    }
}

[Serializable]
internal enum UnreadMode
{
    All,
    Unseen,
    None,
}

internal static class UnreadModeExt
{
    internal static string Name(this UnreadMode mode) => mode switch
    {
        UnreadMode.All => Language.UnreadMode_All,
        UnreadMode.Unseen => Language.UnreadMode_Unseen,
        UnreadMode.None => Language.UnreadMode_None,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    internal static string? Tooltip(this UnreadMode mode) => mode switch
    {
        UnreadMode.All => Language.UnreadMode_All_Tooltip,
        UnreadMode.Unseen => Language.UnreadMode_Unseen_Tooltip,
        UnreadMode.None => Language.UnreadMode_None_Tooltip,
        _ => null,
    };
}

[Serializable]
internal class Tab
{
    public string Name = Language.Tab_DefaultName;
    public Dictionary<ChatType, ChatSource> ChatCodes = new();
    public bool ExtraChatAll;
    public HashSet<Guid> ExtraChatChannels = [];

    public UnreadMode UnreadMode = UnreadMode.Unseen;
    public bool DisplayTimestamp = true;
    public InputChannel? Channel;
    public bool PopOut;
    public bool IndependentOpacity;
    public float Opacity = 100f;
    public bool InputDisabled;

    [NonSerialized]
    public uint Unread;

    [NonSerialized]
    public SemaphoreSlim MessagesMutex = new(1, 1);

    [NonSerialized]
    public List<Message> Messages = [];
    [NonSerialized]
    public HashSet<Guid> TrackedMessageIds = [];

    [NonSerialized]
    public InputChannel? PreviousChannel;

    [NonSerialized]
    public Guid Identifier = Guid.NewGuid();

    ~Tab()
    {
        MessagesMutex.Dispose();
    }

    internal bool Contains(Message message)
    {
        return TrackedMessageIds.Contains(message.Id);
    }

    internal bool Matches(Message message)
    {
        if (message.ExtraChatChannel != Guid.Empty)
            return ExtraChatAll || ExtraChatChannels.Contains(message.ExtraChatChannel);

        return message.Code.Type.IsGm()
               || ChatCodes.TryGetValue(message.Code.Type, out var sources)
               && (message.Code.Source is 0 or (ChatSource) 1
                   || sources.HasFlag(message.Code.Source));
    }

    internal void AddMessage(Message message, bool unread = true) {
        if (Contains(message))
            return;

        MessagesMutex.Wait();
        TrackedMessageIds.Add(message.Id);
        Messages.Add(message);
        while (Messages.Count > MessageManager.MessageDisplayLimit)
        {
            TrackedMessageIds.Remove(Messages[0].Id);
            Messages.RemoveAt(0);
        }
        MessagesMutex.Release();

        if (unread)
            Unread += 1;
    }

    internal void Clear()
    {
        MessagesMutex.Wait();
        Messages.Clear();
        TrackedMessageIds.Clear();
        MessagesMutex.Release();
    }

    internal Tab Clone()
    {
        return new Tab
        {
            Name = Name,
            ChatCodes = ChatCodes.ToDictionary(entry => entry.Key, entry => entry.Value),
            ExtraChatAll = ExtraChatAll,
            ExtraChatChannels = ExtraChatChannels.ToHashSet(),
            UnreadMode = UnreadMode,
            DisplayTimestamp = DisplayTimestamp,
            Channel = Channel,
            PopOut = PopOut,
            IndependentOpacity = IndependentOpacity,
            Opacity = Opacity,
            Identifier = Identifier,
            InputDisabled = InputDisabled,
        };
    }
}

[Serializable]
internal enum CommandHelpSide
{
    None,
    Left,
    Right,
}

internal static class CommandHelpSideExt
{
    internal static string Name(this CommandHelpSide side) => side switch
    {
        CommandHelpSide.None => Language.CommandHelpSide_None,
        CommandHelpSide.Left => Language.CommandHelpSide_Left,
        CommandHelpSide.Right => Language.CommandHelpSide_Right,
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, null),
    };
}

[Serializable]
internal enum KeybindMode
{
    Flexible,
    Strict,
}

internal static class KeybindModeExt
{
    internal static string Name(this KeybindMode mode) => mode switch
    {
        KeybindMode.Flexible => Language.KeybindMode_Flexible_Name,
        KeybindMode.Strict => Language.KeybindMode_Strict_Name,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    internal static string? Tooltip(this KeybindMode mode) => mode switch
    {
        KeybindMode.Flexible => Language.KeybindMode_Flexible_Tooltip,
        KeybindMode.Strict => Language.KeybindMode_Strict_Tooltip,
        _ => null,
    };
}

[Serializable]
internal enum LanguageOverride
{
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

internal static class LanguageOverrideExt
{
    internal static string Name(this LanguageOverride mode) => mode switch
    {
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

    internal static string Code(this LanguageOverride mode) => mode switch
    {
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
internal enum ExtraGlyphRanges
{
    ChineseFull = 1 << 0,
    ChineseSimplifiedCommon = 1 << 1,
    Cyrillic = 1 << 2,
    Japanese = 1 << 3,
    Korean = 1 << 4,
    Thai = 1 << 5,
    Vietnamese = 1 << 6,
}

internal static class ExtraGlyphRangesExt
{
    internal static string Name(this ExtraGlyphRanges ranges) => ranges switch
    {
        ExtraGlyphRanges.ChineseFull => Language.ExtraGlyphRanges_ChineseFull_Name,
        ExtraGlyphRanges.ChineseSimplifiedCommon => Language.ExtraGlyphRanges_ChineseSimplifiedCommon_Name,
        ExtraGlyphRanges.Cyrillic => Language.ExtraGlyphRanges_Cyrillic_Name,
        ExtraGlyphRanges.Japanese => Language.ExtraGlyphRanges_Japanese_Name,
        ExtraGlyphRanges.Korean => Language.ExtraGlyphRanges_Korean_Name,
        ExtraGlyphRanges.Thai => Language.ExtraGlyphRanges_Thai_Name,
        ExtraGlyphRanges.Vietnamese => Language.ExtraGlyphRanges_Vietnamese_Name,
        _ => throw new ArgumentOutOfRangeException(nameof(ranges), ranges, null),
    };

    internal static IntPtr Range(this ExtraGlyphRanges ranges) => ranges switch
    {
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
