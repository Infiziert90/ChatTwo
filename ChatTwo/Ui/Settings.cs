using System.Numerics;
using ChatTwo.Ui.SettingsTabs;
using Dalamud.Game.Command;
using ImGuiNET;

namespace ChatTwo.Ui;

internal sealed class Settings : IUiComponent {
    private PluginUi Ui { get; }

    private Configuration Mutable { get; }
    private List<ISettingsTab> Tabs { get; }

    internal Settings(PluginUi ui) {
        this.Ui = ui;
        this.Mutable = new Configuration();

        this.Tabs = new List<ISettingsTab> {
            new Display(this.Mutable),
            new ChatColours(this.Mutable),
            new Tabs(this.Mutable),
        };

        this.Ui.Plugin.CommandManager.AddHandler("/chat2", new CommandInfo(this.Command) {
            HelpMessage = "Toggle the Chat 2 settings",
        });
    }

    public void Dispose() {
        this.Ui.Plugin.CommandManager.RemoveHandler("/chat2");
    }

    private void Command(string command, string args) {
        this.Ui.SettingsVisible ^= true;
    }

    private void Initialise() {
        this.Mutable.UpdateFrom(this.Ui.Plugin.Config);
    }

    public void Draw() {
        if (!this.Ui.SettingsVisible) {
            return;
        }

        if (!ImGui.Begin($"{this.Ui.Plugin.Name} settings", ref this.Ui.SettingsVisible)) {
            ImGui.End();
            return;
        }

        if (ImGui.IsWindowAppearing()) {
            this.Initialise();
        }

        if (ImGui.BeginTabBar("settings-tabs")) {
            foreach (var settingsTab in this.Tabs) {
                if (!ImGui.BeginTabItem(settingsTab.Name)) {
                    continue;
                }

                var height = ImGui.GetContentRegionAvail().Y
                             - ImGui.GetStyle().FramePadding.Y * 2
                             - ImGui.GetStyle().ItemSpacing.Y
                             - ImGui.GetStyle().ItemInnerSpacing.Y * 2
                             - ImGui.CalcTextSize("A").Y;
                if (ImGui.BeginChild("##chat2-settings", new Vector2(-1, height))) {
                    settingsTab.Draw();
                    ImGui.EndChild();
                }

                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.Separator();

        var save = ImGui.Button("Save");

        ImGui.SameLine();

        if (ImGui.Button("Save and close")) {
            save = true;
            this.Ui.SettingsVisible = false;
        }

        ImGui.SameLine();

        if (ImGui.Button("Discard")) {
            this.Ui.SettingsVisible = false;
        }

        ImGui.End();

        if (save) {
            var config = this.Ui.Plugin.Config;

            var hideChatChanged = this.Mutable.HideChat != this.Ui.Plugin.Config.HideChat;
            var fontSizeChanged = Math.Abs(this.Mutable.FontSize - this.Ui.Plugin.Config.FontSize) > 0.001;

            config.UpdateFrom(this.Mutable);

            this.Ui.Plugin.SaveConfig();

            this.Ui.Plugin.Store.FilterAllTabs(false);

            if (fontSizeChanged) {
                this.Ui.Plugin.Interface.UiBuilder.RebuildFonts();
            }

            if (!this.Mutable.HideChat && hideChatChanged) {
                GameFunctions.GameFunctions.SetChatInteractable(true);
            }

            this.Initialise();
        }
    }
}
