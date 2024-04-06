using System.Diagnostics;
using System.Globalization;
using ChatTwo.Ipc;
using ChatTwo.Resources;
using ChatTwo.Util;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.ClientState.Objects;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
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

    internal Configuration Config { get; }
    internal Commands Commands { get; }
    internal XivCommonBase Common { get; }
    internal TextureCache TextureCache { get; }
    internal GameFunctions.GameFunctions Functions { get; }
    internal Store Store { get; }
    internal IpcManager Ipc { get; }
    internal ExtraChat ExtraChat { get; }
    internal PluginUi Ui { get; }

    internal int DeferredSaveFrames = -1;

    internal DateTime GameStarted { get; }

    #pragma warning disable CS8618
    public Plugin() {
        this.GameStarted = Process.GetCurrentProcess().StartTime.ToUniversalTime();

        this.Config = Interface.GetPluginConfig() as Configuration ?? new Configuration();
        this.Config.Migrate();

        if (this.Config.Tabs.Count == 0) {
            this.Config.Tabs.Add(TabsUtil.VanillaGeneral);
        }

        this.LanguageChanged(Interface.UiLanguage);

        this.Commands = new Commands(this);
        this.Common = new XivCommonBase(Interface);
        this.TextureCache = new TextureCache(TextureProvider);
        this.Functions = new GameFunctions.GameFunctions(this);
        this.Ipc = new IpcManager(Interface);
        this.ExtraChat = new ExtraChat(this);
        this.Ui = new PluginUi(this);
        Ui.BuildFonts();

        this.Store = new Store(this);  // requires Ui

        // let all the other components register, then initialise commands
        this.Commands.Initialise();

        if (Interface.Reason is not PluginLoadReason.Boot) {
            this.Store.FilterAllTabs(false);
        }

        Framework.Update += FrameworkUpdate;
        Interface.UiBuilder.Draw += Ui.Draw;
        Interface.LanguageChanged += LanguageChanged;

        AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "ItemDetail", Ui.ChatLog.PayloadHandler.MoveTooltip);
    }
    #pragma warning restore CS8618

    public void Dispose() {
        AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, "ItemDetail", Ui.ChatLog.PayloadHandler.MoveTooltip);

        Interface.LanguageChanged -= LanguageChanged;
        Interface.UiBuilder.Draw -= Ui.Draw;
        Framework.Update -= FrameworkUpdate;
        GameFunctions.GameFunctions.SetChatInteractable(true);

        this.Ui.Dispose();
        this.ExtraChat.Dispose();
        this.Ipc.Dispose();
        this.Store.Dispose();
        this.Functions.Dispose();
        this.TextureCache.Dispose();
        this.Common.Dispose();
        this.Commands.Dispose();
    }

    internal void SaveConfig() {
        Interface.SavePluginConfig(this.Config);
    }

    internal void LanguageChanged(string langCode) {
        var info = this.Config.LanguageOverride is LanguageOverride.None
            ? new CultureInfo(langCode)
            : new CultureInfo(this.Config.LanguageOverride.Code());

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
