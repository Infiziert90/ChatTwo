using System.Numerics;
using ChatTwo.Resources;
using ChatTwo.Ui.SettingsTabs;
using ChatTwo.Util;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ImGuiNET;

namespace ChatTwo.Ui;

public sealed class SettingsWindow : Window
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

        Tabs = new List<ISettingsTab>
        {
            new Display(Mutable),
            new ChatLog(Plugin, Mutable),
            new Emote(Plugin, Mutable),
            new Preview(Plugin, Mutable),
            new Ui.SettingsTabs.Fonts(Mutable),
            new ChatColours(Plugin, Mutable),
            new Tabs(Plugin, Mutable),
            new Database(Plugin, Mutable),
            new Miscellaneous(Mutable),
            new Changelog(Mutable),
            new About(),
        };

        RespectCloseHotkey = false;
        DisableWindowSounds = true;

        Initialise();

        Plugin.Commands.Register("/chat2", "Perform various actions with Chat 2.").Execute += Command;
        Plugin.Interface.UiBuilder.OpenConfigUi += Toggle;
    }

    public void Dispose()
    {
        Plugin.Interface.UiBuilder.OpenConfigUi -= Toggle;
        Plugin.Commands.Register("/chat2").Execute -= Command;
    }

    private void Command(string command, string args)
    {
        if (string.IsNullOrWhiteSpace(args))
            Toggle();
    }

    private void Initialise()
    {
        Mutable.UpdateFrom(Plugin.Config, false);
    }

    public override void Draw()
    {
        if (ImGui.IsWindowAppearing())
            Initialise();

        using (var table = ImRaii.Table("##chat2-settings-table", 2))
        {
            if (table)
            {
                ImGui.TableSetupColumn("tab", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("settings", ImGuiTableColumnFlags.WidthStretch);

                ImGui.TableNextColumn();

                var changed = false;
                for (var i = 0; i < Tabs.Count; i++)
                {
                    if (!ImGui.Selectable($"{Tabs[i].Name}###tab-{i}", CurrentTab == i))
                        continue;

                    CurrentTab = i;
                    changed = true;
                }

                ImGui.TableNextColumn();

                var height = ImGui.GetContentRegionAvail().Y - ImGui.GetStyle().FramePadding.Y * 2 - ImGui.GetStyle().ItemSpacing.Y
                             - ImGui.GetStyle().ItemInnerSpacing.Y * 2 - ImGui.CalcTextSize("A").Y;
                using var child = ImRaii.Child("##chat2-settings", new Vector2(-1, height));
                if (child)
                    Tabs[CurrentTab].Draw(changed);
            }
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

        const string buttonLabel = "Anna's Ko-fi";
        const string buttonLabel2 = "Infi's Ko-fi";

        using (ImRaii.PushColor(ImGuiCol.Button, ColourUtil.RgbaToAbgr(0xFF5E5BFF)))
        using (ImRaii.PushColor(ImGuiCol.ButtonHovered, ColourUtil.RgbaToAbgr(0xFF7775FF)))
        using (ImRaii.PushColor(ImGuiCol.ButtonActive, ColourUtil.RgbaToAbgr(0xFF4542FF)))
        using (ImRaii.PushColor(ImGuiCol.Text, 0xFFFFFFFF))
        {
            var buttonWidth = ImGui.CalcTextSize(buttonLabel).X + ImGui.GetStyle().FramePadding.X * 2;
            var buttonWidth2 = ImGui.CalcTextSize(buttonLabel2).X + ImGui.GetStyle().FramePadding.X * 2;
            ImGui.SameLine(ImGui.GetContentRegionAvail().X - buttonWidth - buttonWidth2);

            if (ImGui.Button(buttonLabel2))
                Dalamud.Utility.Util.OpenLink("https://ko-fi.com/infiii");

            ImGui.SameLine();

            if (ImGui.Button(buttonLabel))
                Dalamud.Utility.Util.OpenLink("https://ko-fi.com/lojewalo");
        }

        if (!save)
            return;

        // calculate all conditions before updating config
        var hideChanged = !Mutable.HideChat && Mutable.HideChat != Plugin.Config.HideChat;
        var languageChanged = Mutable.LanguageOverride != Plugin.Config.LanguageOverride;
        var fontChanged = Mutable.GlobalFontV2 != Plugin.Config.GlobalFontV2
                          || Mutable.JapaneseFontV2 != Plugin.Config.JapaneseFontV2
                          || Mutable.ItalicFontV2 != Plugin.Config.ItalicFontV2
                          || Mutable.ExtraGlyphRanges != Plugin.Config.ExtraGlyphRanges;
        var fontSizeChanged = Math.Abs(Mutable.SymbolsFontSizeV2 - Plugin.Config.SymbolsFontSizeV2) > 0.001
                          || Math.Abs(Mutable.FontSizeV2 - Plugin.Config.FontSizeV2) > 0.001;

        Plugin.Config.UpdateFrom(Mutable, true);

        // save after 60 frames have passed, which should hopefully not
        // commit any changes that cause a crash
        Plugin.DeferredSaveFrames = 60;
        Plugin.MessageManager.ClearAllTabs();
        Plugin.MessageManager.FilterAllTabsAsync();

        if (fontChanged || fontSizeChanged)
            Plugin.FontManager.BuildFonts();

        if (languageChanged)
            Plugin.LanguageChanged(Plugin.Interface.UiLanguage);

        if (hideChanged)
            GameFunctions.GameFunctions.SetChatInteractable(true);

        if (Plugin.Config.ShowEmotes)
            Task.Run(EmoteCache.LoadData);

        Initialise();
    }
}
