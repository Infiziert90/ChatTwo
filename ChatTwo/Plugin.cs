using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ChatTwo.Http;
using ChatTwo.Ipc;
using ChatTwo.Resources;
using ChatTwo.Ui;
using ChatTwo.Util;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;

namespace ChatTwo;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class Plugin : IDalamudPlugin
{
    internal const string PluginName = "Chat 2";

    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IDalamudPluginInterface Interface { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IKeyState KeyState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IPartyList PartyList { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
    [PluginService] internal static IGameConfig GameConfig { get; private set; } = null!;
    [PluginService] internal static INotificationManager Notification { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;

    internal static Configuration Config = null!;

    public readonly WindowSystem WindowSystem = new(PluginName);
    public SettingsWindow SettingsWindow { get; }
    public ChatLogWindow ChatLogWindow { get; }
    public DbViewer DbViewer { get; }
    public InputPreview InputPreview { get; }
    public CommandHelpWindow CommandHelpWindow { get; }
    public SeStringDebugger SeStringDebugger { get; }
    public DebuggerWindow DebuggerWindow { get; }

    internal Commands Commands { get; }
    internal GameFunctions.GameFunctions Functions { get; }
    internal MessageManager MessageManager { get; }
    internal IpcManager Ipc { get; }
    internal ExtraChat ExtraChat { get; }
    internal TypingIpc TypingIpc { get; }
    internal FontManager FontManager { get; }

    internal ServerCore ServerCore { get; }

    internal int DeferredSaveFrames = -1;

    internal DateTime GameStarted { get; }

    // Tab management needs to happen outside the chatlog window class for access reasons
    internal int LastTab { get; set; }
    internal int? WantedTab { get; set; }
    internal Tab CurrentTab
    {
        get
        {
            var i = LastTab;
            return i > -1 && i < Config.Tabs.Count ? Config.Tabs[i] : new Tab();
        }
    }

    public Plugin()
    {
        try
        {
            GameStarted = Process.GetCurrentProcess().StartTime.ToUniversalTime();

            Config = Interface.GetPluginConfig() as Configuration ?? new Configuration();

            if (Config.Tabs.Count == 0)
                Config.Tabs.Add(TabsUtil.VanillaGeneral);
            Config.InactivityHideChannels ??= TabsUtil.AllChannels();

            LanguageChanged(Interface.UiLanguage);
            ImGuiUtil.Initialize(this);

            // Functions calls this in its ctor if the player is already logged in
            ServerCore = new ServerCore(this);

            Commands = new Commands(this);
            Functions = new GameFunctions.GameFunctions(this);
            Ipc = new IpcManager();
            TypingIpc = new TypingIpc(this);
            ExtraChat = new ExtraChat(this);
            FontManager = new FontManager();

            ChatLogWindow = new ChatLogWindow(this);
            SettingsWindow = new SettingsWindow(this);
            DbViewer = new DbViewer(this);
            InputPreview = new InputPreview(ChatLogWindow);
            CommandHelpWindow = new CommandHelpWindow(ChatLogWindow);
            SeStringDebugger = new SeStringDebugger(this);
            DebuggerWindow = new DebuggerWindow(this);

            WindowSystem.AddWindow(ChatLogWindow);
            WindowSystem.AddWindow(SettingsWindow);
            WindowSystem.AddWindow(DbViewer);
            WindowSystem.AddWindow(InputPreview);
            WindowSystem.AddWindow(CommandHelpWindow);
            WindowSystem.AddWindow(SeStringDebugger);
            WindowSystem.AddWindow(DebuggerWindow);

            FontManager.BuildFonts();

            Interface.UiBuilder.DisableCutsceneUiHide = true;
            Interface.UiBuilder.DisableGposeUiHide = true;

            MessageManager = new MessageManager(this); // requires Ui

            // let all the other components register, then initialize commands
            Commands.Initialise();

            if (Interface.Reason is not PluginLoadReason.Boot)
                MessageManager.FilterAllTabsAsync();

            Framework.Update += FrameworkUpdate;
            Interface.UiBuilder.Draw += Draw;
            Interface.LanguageChanged += LanguageChanged;

            if (Config.ShowEmotes)
                Task.Run(EmoteCache.LoadData);

            #if !DEBUG
            // Avoid 300ms hitch when sending first message by preloading the
            // auto-translate cache. Don't do this in debug because it makes
            // profiling difficult.
            AutoTranslate.PreloadCache();
            #endif

            // Automatically start the webserver if requested
            if (Config.WebinterfaceAutoStart)
            {
                Task.Run(() =>
                {
                    ServerCore.Start();
                    ServerCore.Run();
                });
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Plugin load threw an error, turning off plugin");
            Dispose();

            // Re-throw the exception to fail the plugin load.
            throw;
        }
    }

    // Suppressing this warning because Dispose() is called in Plugin() if the
    // load fails, so some values may not be initialized.
    [SuppressMessage("ReSharper", "ConditionalAccessQualifierIsNonNullableAccordingToAPIContract")]
    public void Dispose()
    {
        Interface.LanguageChanged -= LanguageChanged;
        Interface.UiBuilder.Draw -= Draw;
        Framework.Update -= FrameworkUpdate;
        GameFunctions.GameFunctions.SetChatInteractable(true);

        WindowSystem?.RemoveAllWindows();
        ChatLogWindow?.Dispose();
        DbViewer?.Dispose();
        InputPreview?.Dispose();
        SettingsWindow?.Dispose();
        DebuggerWindow?.Dispose();
        SeStringDebugger?.Dispose();

        TypingIpc?.Dispose();
        ExtraChat?.Dispose();
        Ipc?.Dispose();
        MessageManager?.DisposeAsync().AsTask().Wait();
        Functions?.Dispose();
        Commands?.Dispose();

        EmoteCache.Dispose();
        ServerCore?.DisposeAsync().AsTask().Wait();
    }

    private void Draw()
    {
        ChatLogWindow.BeginFrame();

        if (Config.HideInLoadingScreens && Condition[ConditionFlag.BetweenAreas])
        {
            ChatLogWindow.FinalizeFrame();
            TypingIpc?.Update();
            return;
        }

        ChatLogWindow.HideStateCheck();

        Interface.UiBuilder.DisableUserUiHide = !Config.HideWhenUiHidden;
        ChatLogWindow.DefaultText = ImGui.GetStyle().Colors[(int) ImGuiCol.Text];

        using ((Config.FontsEnabled ? FontManager.RegularFont : FontManager.Axis).Push())
        {
            WindowSystem.Draw();
        }

        ChatLogWindow.FinalizeFrame();
        TypingIpc?.Update();
    }

    internal void SaveConfig()
    {
        Interface.SavePluginConfig(Config);
    }

    internal void LanguageChanged(string langCode)
    {
        var info = Config.LanguageOverride is LanguageOverride.None
            ? new CultureInfo(langCode)
            : new CultureInfo(Config.LanguageOverride.Code());

        Language.Culture = info;
    }

    private static readonly string[] ChatAddonNames =
    [
        "ChatLog",
        "ChatLogPanel_0",
        "ChatLogPanel_1",
        "ChatLogPanel_2",
        "ChatLogPanel_3"
    ];

    private void FrameworkUpdate(IFramework framework)
    {
        if (DeferredSaveFrames >= 0 && DeferredSaveFrames-- == 0)
            SaveConfig();

        if (!Config.HideChat)
            return;

        foreach (var name in ChatAddonNames)
            if (GameFunctions.GameFunctions.IsAddonInteractable(name))
                GameFunctions.GameFunctions.SetAddonInteractable(name, false);
    }

    public static bool InBattle => Condition[ConditionFlag.InCombat];
    public static bool GposeActive => Condition[ConditionFlag.WatchingCutscene];
    public static bool CutsceneActive => Condition[ConditionFlag.OccupiedInCutSceneEvent] || Condition[ConditionFlag.WatchingCutscene78];
}
