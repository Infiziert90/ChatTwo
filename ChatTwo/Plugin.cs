using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Party;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.IoC;
using Dalamud.Plugin;
using XivCommon;

namespace ChatTwo;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class Plugin : IDalamudPlugin {
    public string Name => "Chat 2";

    [PluginService]
    internal DalamudPluginInterface Interface { get; init; }

    [PluginService]
    internal ChatGui ChatGui { get; init; }

    [PluginService]
    internal ClientState ClientState { get; init; }

    [PluginService]
    internal CommandManager CommandManager { get; init; }

    [PluginService]
    internal DataManager DataManager { get; init; }

    [PluginService]
    internal Framework Framework { get; init; }

    [PluginService]
    internal GameGui GameGui { get; init; }

    [PluginService]
    internal ObjectTable ObjectTable { get; init; }

    [PluginService]
    internal PartyList PartyList { get; init; }

    [PluginService]
    internal SigScanner SigScanner { get; init; }

    [PluginService]
    internal TargetManager TargetManager { get; init; }

    internal Configuration Config { get; }
    internal XivCommonBase Common { get; }
    internal TextureCache TextureCache { get; }
    internal GameFunctions Functions { get; }
    internal Store Store { get; }
    internal PluginUi Ui { get; }

    #pragma warning disable CS8618
    public Plugin() {
        this.Config = this.Interface!.GetPluginConfig() as Configuration ?? new Configuration();
        this.Common = new XivCommonBase();
        this.TextureCache = new TextureCache(this.DataManager!);
        this.Functions = new GameFunctions(this);
        this.Store = new Store(this);
        this.Ui = new PluginUi(this);

        this.Framework!.Update += this.FrameworkUpdate;
    }
    #pragma warning restore CS8618

    public void Dispose() {
        this.Framework.Update -= this.FrameworkUpdate;
        GameFunctions.SetChatInteractable(true);

        this.Ui.Dispose();
        this.Store.Dispose();
        this.Functions.Dispose();
        this.TextureCache.Dispose();
        this.Common.Dispose();
    }

    internal void SaveConfig() {
        this.Interface.SavePluginConfig(this.Config);
    }

    private static readonly string[] ChatAddonNames = {
        "ChatLog",
        "ChatLogPanel_0",
        "ChatLogPanel_1",
        "ChatLogPanel_2",
        "ChatLogPanel_3",
    };

    private void FrameworkUpdate(Framework framework) {
        if (!this.Config.HideChat) {
            return;
        }

        foreach (var name in ChatAddonNames) {
            if (GameFunctions.IsAddonInteractable(name)) {
                GameFunctions.SetAddonInteractable(name, false);
            }
        }
    }
}
