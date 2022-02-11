using System.Diagnostics;
using System.Numerics;
using ChatTwo.Resources;
using ChatTwo.Ui.SettingsTabs;
using ChatTwo.Util;
using Dalamud.Game.Command;
using Dalamud.Interface;
using ImGuiNET;

namespace ChatTwo.Ui;

internal sealed class Settings : IUiComponent {
    private PluginUi Ui { get; }

    private Configuration Mutable { get; }
    private List<ISettingsTab> Tabs { get; }

    private int _currentTab;

    internal Settings(PluginUi ui) {
        this.Ui = ui;
        this.Mutable = new Configuration();

        this.Tabs = new List<ISettingsTab> {
            new Display(this.Mutable),
            new Ui.SettingsTabs.Fonts(this.Mutable),
            new ChatColours(this.Mutable, this.Ui.Plugin),
            new Tabs(this.Mutable),
            new Miscellaneous(this.Mutable),
            new About(),
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

        ImGui.SetNextWindowSize(new Vector2(475, 600) * ImGuiHelpers.GlobalScale, ImGuiCond.FirstUseEver);

        var name = string.Format(Language.Settings_Title, this.Ui.Plugin.Name);
        if (!ImGui.Begin($"{name}###chat2-settings", ref this.Ui.SettingsVisible)) {
            ImGui.End();
            return;
        }

        if (ImGui.IsWindowAppearing()) {
            this.Initialise();
        }

        if (ImGui.BeginTable("##chat2-settings-table", 2)) {
            ImGui.TableSetupColumn("tab", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("settings", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextColumn();

            for (var i = 0; i < this.Tabs.Count; i++) {
                if (ImGui.Selectable($"{this.Tabs[i].Name}###tab-{i}", this._currentTab == i)) {
                    this._currentTab = i;
                }
            }

            ImGui.TableNextColumn();

            var height = ImGui.GetContentRegionAvail().Y
                         - ImGui.GetStyle().FramePadding.Y * 2
                         - ImGui.GetStyle().ItemSpacing.Y
                         - ImGui.GetStyle().ItemInnerSpacing.Y * 2
                         - ImGui.CalcTextSize("A").Y;
            if (ImGui.BeginChild("##chat2-settings", new Vector2(-1, height))) {
                this.Tabs[this._currentTab].Draw();
                ImGui.EndChild();
            }

            ImGui.EndTable();
        }

        ImGui.Separator();

        var save = ImGui.Button(Language.Settings_Save);

        ImGui.SameLine();

        if (ImGui.Button(Language.Settings_SaveAndClose)) {
            save = true;
            this.Ui.SettingsVisible = false;
        }

        ImGui.SameLine();

        if (ImGui.Button(Language.Settings_Discard)) {
            this.Ui.SettingsVisible = false;
        }

        var buttonLabel = string.Format(Language.Settings_Kofi, this.Ui.Plugin.Name);

        ImGui.PushStyleColor(ImGuiCol.Button, ColourUtil.RgbaToAbgr(0xFF5E5BFF));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ColourUtil.RgbaToAbgr(0xFF7775FF));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, ColourUtil.RgbaToAbgr(0xFF4542FF));
        ImGui.PushStyleColor(ImGuiCol.Text, 0xFFFFFFFF);

        try {
            var buttonWidth = ImGui.CalcTextSize(buttonLabel).X + ImGui.GetStyle().FramePadding.X * 2;
            ImGui.SameLine(ImGui.GetContentRegionAvail().X - buttonWidth);

            if (ImGui.Button(buttonLabel)) {
                Process.Start(new ProcessStartInfo("https://ko-fi.com/ascclemens") {
                    UseShellExecute = true,
                });
            }
        } finally {
            ImGui.PopStyleColor(4);
        }

        ImGui.End();

        if (save) {
            var config = this.Ui.Plugin.Config;

            var hideChatChanged = this.Mutable.HideChat != this.Ui.Plugin.Config.HideChat;
            var fontChanged = this.Mutable.GlobalFont != this.Ui.Plugin.Config.GlobalFont
                              || this.Mutable.JapaneseFont != this.Ui.Plugin.Config.JapaneseFont;
            var fontSizeChanged = Math.Abs(this.Mutable.FontSize - this.Ui.Plugin.Config.FontSize) > 0.001
                                  || Math.Abs(this.Mutable.JapaneseFontSize - this.Ui.Plugin.Config.JapaneseFontSize) > 0.001
                                  || Math.Abs(this.Mutable.SymbolsFontSize - this.Ui.Plugin.Config.SymbolsFontSize) > 0.001;
            var langChanged = this.Mutable.LanguageOverride != this.Ui.Plugin.Config.LanguageOverride;

            config.UpdateFrom(this.Mutable);

            // save after 60 frames have passed, which should hopefully not
            // commit any changes that cause a crash
            this.Ui.Plugin.DeferredSaveFrames = 60;

            this.Ui.Plugin.Store.FilterAllTabs(false);

            if (fontChanged || fontSizeChanged) {
                this.Ui.Plugin.Interface.UiBuilder.RebuildFonts();
            }

            if (langChanged) {
                this.Ui.Plugin.LanguageChanged(this.Ui.Plugin.Interface.UiLanguage);
            }

            if (!this.Mutable.HideChat && hideChatChanged) {
                GameFunctions.GameFunctions.SetChatInteractable(true);
            }

            this.Initialise();
        }
    }
}
