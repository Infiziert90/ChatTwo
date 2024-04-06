using System.Numerics;
using ChatTwo.Resources;
using ChatTwo.Ui.SettingsTabs;
using ChatTwo.Util;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ImGuiNET;

namespace ChatTwo.Ui;

public sealed class SettingsWindow : Window, IUiComponent
{
    private readonly Plugin Plugin;

    private Configuration Mutable { get; }
    private List<ISettingsTab> Tabs { get; }
    private int CurrentTab;

    internal SettingsWindow(Plugin plugin) : base($"{Language.Settings_Title.Format(Plugin.PluginName)}###chat2-settings")
    {
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(475, 600),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Plugin = plugin;
        Mutable = new Configuration();

        Tabs = new List<ISettingsTab> {
            new Display(Mutable),
            new Ui.SettingsTabs.Fonts(Mutable),
            new ChatColours(Mutable, Plugin),
            new Tabs(Plugin, Mutable),
            new Database(Mutable, Plugin.Store),
            new Miscellaneous(Mutable),
            new About(),
        };

        Initialise();

        Plugin.Commands.Register("/chat2", "Perform various actions with Chat 2.").Execute += Command;
        Plugin.Interface.UiBuilder.OpenConfigUi += Toggle;
    }

    public void Dispose() {
        Plugin.Interface.UiBuilder.OpenConfigUi -= Toggle;
        Plugin.Commands.Register("/chat2").Execute -= Command;
    }

    private void Command(string command, string args) {
        if (string.IsNullOrWhiteSpace(args))
            Toggle();
    }

    private void Initialise() {
        Mutable.UpdateFrom(Plugin.Config);
    }

    public override void Draw()
    {
        if (ImGui.IsWindowAppearing())
            Initialise();

        if (ImGui.BeginTable("##chat2-settings-table", 2)) {
            ImGui.TableSetupColumn("tab", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("settings", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextColumn();

            var changed = false;
            for (var i = 0; i < Tabs.Count; i++) {
                if (ImGui.Selectable($"{Tabs[i].Name}###tab-{i}", CurrentTab == i)) {
                    CurrentTab = i;
                    changed = true;
                }
            }

            ImGui.TableNextColumn();

            var height = ImGui.GetContentRegionAvail().Y
                         - ImGui.GetStyle().FramePadding.Y * 2
                         - ImGui.GetStyle().ItemSpacing.Y
                         - ImGui.GetStyle().ItemInnerSpacing.Y * 2
                         - ImGui.CalcTextSize("A").Y;
            if (ImGui.BeginChild("##chat2-settings", new Vector2(-1, height))) {
                Tabs[CurrentTab].Draw(changed);
                ImGui.EndChild();
            }

            ImGui.EndTable();
        }

        ImGui.Separator();

        var save = ImGui.Button(Language.Settings_Save);

        ImGui.SameLine();

        if (ImGui.Button(Language.Settings_SaveAndClose)) {
            save = true;
            IsOpen = false;
        }

        ImGui.SameLine();

        if (ImGui.Button(Language.Settings_Discard)) {
            IsOpen = false;
        }

        var buttonLabel = "Anna's Ko-fi";
        var buttonLabel2 = "Infi's Ko-fi";

        ImGui.PushStyleColor(ImGuiCol.Button, ColourUtil.RgbaToAbgr(0xFF5E5BFF));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ColourUtil.RgbaToAbgr(0xFF7775FF));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, ColourUtil.RgbaToAbgr(0xFF4542FF));
        ImGui.PushStyleColor(ImGuiCol.Text, 0xFFFFFFFF);

        try {
            var buttonWidth = ImGui.CalcTextSize(buttonLabel).X + ImGui.GetStyle().FramePadding.X * 2;
            var buttonWidth2 = ImGui.CalcTextSize(buttonLabel2).X + ImGui.GetStyle().FramePadding.X * 2;
            ImGui.SameLine(ImGui.GetContentRegionAvail().X - buttonWidth - buttonWidth2);

            if (ImGui.Button(buttonLabel2)) {
                Dalamud.Utility.Util.OpenLink("https://ko-fi.com/infiii");
            }

            ImGui.SameLine();

            if (ImGui.Button(buttonLabel)) {
                Dalamud.Utility.Util.OpenLink("https://ko-fi.com/lojewalo");
            }
        } finally {
            ImGui.PopStyleColor(4);
        }

        if (save) {
            var config = Plugin.Config;

            var hideChatChanged = Mutable.HideChat != Plugin.Config.HideChat;
            var fontChanged = Mutable.GlobalFont != Plugin.Config.GlobalFont
                              || Mutable.JapaneseFont != Plugin.Config.JapaneseFont
                              || Mutable.ExtraGlyphRanges != Plugin.Config.ExtraGlyphRanges;
            var fontSizeChanged = Math.Abs(Mutable.FontSize - Plugin.Config.FontSize) > 0.001
                                  || Math.Abs(Mutable.JapaneseFontSize - Plugin.Config.JapaneseFontSize) > 0.001
                                  || Math.Abs(Mutable.SymbolsFontSize - Plugin.Config.SymbolsFontSize) > 0.001;
            var langChanged = Mutable.LanguageOverride != Plugin.Config.LanguageOverride;
            var sharedChanged = Mutable.SharedMode != Plugin.Config.SharedMode;

            config.UpdateFrom(Mutable);

            // save after 60 frames have passed, which should hopefully not
            // commit any changes that cause a crash
            Plugin.DeferredSaveFrames = 60;

            Plugin.Store.FilterAllTabs(false);

            if (fontChanged || fontSizeChanged) {
                Plugin.FontManager.BuildFonts();
            }

            if (langChanged) {
                Plugin.LanguageChanged(Plugin.Interface.UiLanguage);
            }

            if (sharedChanged) {
                Plugin.Store.Reconnect();
            }

            if (!Mutable.HideChat && hideChatChanged) {
                GameFunctions.GameFunctions.SetChatInteractable(true);
            }

            Initialise();
        }
    }
}
