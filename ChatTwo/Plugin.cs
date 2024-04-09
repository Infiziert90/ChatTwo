using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using ChatTwo.Ipc;
using ChatTwo.Resources;
using ChatTwo.Ui;
using ChatTwo.Util;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Interface.Style;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ImGuiNET;
using XivCommon;

namespace ChatTwo;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class Plugin : IDalamudPlugin {
    internal const string PluginName = "Chat 2";

    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static DalamudPluginInterface Interface { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; set; } = null!;
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

    public const string Authors = "Infi, Anna";
    public static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "Unknown";

    public readonly WindowSystem WindowSystem = new(PluginName);
    public SettingsWindow SettingsWindow { get; }
    public ChatLogWindow ChatLogWindow { get; }
    public CommandHelpWindow CommandHelpWindow { get; }
    public SeStringDebugger SeStringDebugger { get; }

    internal Configuration Config { get; }
    internal Commands Commands { get; }
    internal XivCommonBase Common { get; }
    internal TextureCache TextureCache { get; }
    internal GameFunctions.GameFunctions Functions { get; }
    internal Store Store { get; }
    internal IpcManager Ipc { get; }
    internal ExtraChat ExtraChat { get; }
    internal FontManager FontManager { get; }

    internal int DeferredSaveFrames = -1;

    internal DateTime GameStarted { get; }

    #pragma warning disable CS8618
    public Plugin() {
        GameStarted = Process.GetCurrentProcess().StartTime.ToUniversalTime();

        Config = Interface.GetPluginConfig() as Configuration ?? new Configuration();
        Config.Migrate();

        if (Config.Tabs.Count == 0) {
            Config.Tabs.Add(TabsUtil.VanillaGeneral);
        }

        LanguageChanged(Interface.UiLanguage);

        Commands = new Commands(this);
        Common = new XivCommonBase(Interface);
        TextureCache = new TextureCache(TextureProvider);
        Functions = new GameFunctions.GameFunctions(this);
        Ipc = new IpcManager(Interface);
        ExtraChat = new ExtraChat(this);
        FontManager = new FontManager(this);

        ChatLogWindow = new ChatLogWindow(this);
        SettingsWindow = new SettingsWindow(this);
        CommandHelpWindow = new CommandHelpWindow(ChatLogWindow);
        SeStringDebugger = new SeStringDebugger(this);

        WindowSystem.AddWindow(ChatLogWindow);
        WindowSystem.AddWindow(SettingsWindow);
        WindowSystem.AddWindow(CommandHelpWindow);
        WindowSystem.AddWindow(SeStringDebugger);
        FontManager.BuildFonts();

        Interface.UiBuilder.DisableCutsceneUiHide = true;
        Interface.UiBuilder.DisableGposeUiHide = true;

        Store = new Store(this);  // requires Ui

        // let all the other components register, then initialise commands
        Commands.Initialise();

        if (Interface.Reason is not PluginLoadReason.Boot) {
            Store.FilterAllTabs(false);
        }

        Framework.Update += FrameworkUpdate;
        Interface.UiBuilder.Draw += Draw;
        Interface.LanguageChanged += LanguageChanged;
    }
    #pragma warning restore CS8618

    public void Dispose() {
        Interface.LanguageChanged -= LanguageChanged;
        Interface.UiBuilder.Draw -= Draw;
        Framework.Update -= FrameworkUpdate;
        GameFunctions.GameFunctions.SetChatInteractable(true);

        WindowSystem.RemoveAllWindows();
        ChatLogWindow.Dispose();
        SettingsWindow.Dispose();
        SeStringDebugger.Dispose();

        ExtraChat.Dispose();
        Ipc.Dispose();
        Store.Dispose();
        Functions.Dispose();
        TextureCache.Dispose();
        Common.Dispose();
        Commands.Dispose();
    }

    private void Draw()
    {

        Interface.UiBuilder.DisableUserUiHide = !Config.HideWhenUiHidden;
        ChatLogWindow.DefaultText = ImGui.GetStyle().Colors[(int) ImGuiCol.Text];

        using ((Config.FontsEnabled ? FontManager.RegularFont : FontManager.Axis).Push())
        {
            WindowSystem.Draw();
        }
    }

    internal void SaveConfig() {
        Interface.SavePluginConfig(Config);
    }

    internal void LanguageChanged(string langCode) {
        var info = Config.LanguageOverride is LanguageOverride.None
            ? new CultureInfo(langCode)
            : new CultureInfo(Config.LanguageOverride.Code());

        Language.Culture = info;
    }

    private static readonly string[] ChatAddonNames = {
        "ChatLog",
        "ChatLogPanel_0",
        "ChatLogPanel_1",
        "ChatLogPanel_2",
        "ChatLogPanel_3",
    };

    private void FrameworkUpdate(IFramework framework) {
        if (DeferredSaveFrames >= 0 && DeferredSaveFrames-- == 0) {
            SaveConfig();
        }

        if (!Config.HideChat) {
            return;
        }

        foreach (var name in ChatAddonNames) {
            if (GameFunctions.GameFunctions.IsAddonInteractable(name)) {
                GameFunctions.GameFunctions.SetAddonInteractable(name, false);
            }
        }
    }
}
