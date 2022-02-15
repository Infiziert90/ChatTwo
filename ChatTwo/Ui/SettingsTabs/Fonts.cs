using ChatTwo.Resources;
using ChatTwo.Util;
using ImGuiNET;

namespace ChatTwo.Ui.SettingsTabs;

public class Fonts : ISettingsTab {
    private Configuration Mutable { get; }

    public string Name => Language.Options_Fonts_Tab + "###tabs-fonts";
    private List<string> GlobalFonts { get; set; } = new();
    private List<string> JpFonts { get; set; } = new();

    internal Fonts(Configuration mutable) {
        this.Mutable = mutable;
        this.UpdateFonts();
    }

    private void UpdateFonts() {
        this.GlobalFonts = Ui.Fonts.GetFonts();
        this.JpFonts = Ui.Fonts.GetJpFonts();
    }

    public void Draw(bool changed) {
        if (changed) {
            this.UpdateFonts();
        }

        ImGui.PushTextWrapPos();

        if (ImGuiUtil.BeginComboVertical(Language.Options_Font_Name, this.Mutable.GlobalFont)) {
            foreach (var font in Ui.Fonts.GlobalFonts) {
                if (ImGui.Selectable(font.Name, this.Mutable.GlobalFont == font.Name)) {
                    this.Mutable.GlobalFont = font.Name;
                }

                if (ImGui.IsWindowAppearing() && this.Mutable.GlobalFont == font.Name) {
                    ImGui.SetScrollHereY(0.5f);
                }
            }

            ImGui.Separator();

            foreach (var name in this.GlobalFonts) {
                if (ImGui.Selectable(name, this.Mutable.GlobalFont == name)) {
                    this.Mutable.GlobalFont = name;
                }

                if (ImGui.IsWindowAppearing() && this.Mutable.GlobalFont == name) {
                    ImGui.SetScrollHereY(0.5f);
                }
            }

            ImGui.EndCombo();
        }

        ImGuiUtil.HelpText(string.Format(Language.Options_Font_Description, Plugin.PluginName));
        ImGuiUtil.WarningText(Language.Options_Font_Warning);
        ImGui.Spacing();

        if (ImGuiUtil.BeginComboVertical(Language.Options_JapaneseFont_Name, this.Mutable.JapaneseFont)) {
            foreach (var (name, _) in Ui.Fonts.JapaneseFonts) {
                if (ImGui.Selectable(name, this.Mutable.JapaneseFont == name)) {
                    this.Mutable.JapaneseFont = name;
                }

                if (ImGui.IsWindowAppearing() && this.Mutable.JapaneseFont == name) {
                    ImGui.SetScrollHereY(0.5f);
                }
            }

            ImGui.Separator();

            foreach (var family in this.JpFonts) {
                if (ImGui.Selectable(family, this.Mutable.JapaneseFont == family)) {
                    this.Mutable.JapaneseFont = family;
                }

                if (ImGui.IsWindowAppearing() && this.Mutable.JapaneseFont == family) {
                    ImGui.SetScrollHereY(0.5f);
                }
            }

            ImGui.EndCombo();
        }

        ImGuiUtil.HelpText(string.Format(Language.Options_JapaneseFont_Description, Plugin.PluginName));
        ImGui.Spacing();

        const float speed = .0125f;
        const float min = 8f;
        const float max = 36f;
        ImGuiUtil.DragFloatVertical(Language.Options_FontSize_Name, ref this.Mutable.FontSize, speed, min, max, $"{this.Mutable.FontSize:N1}");
        ImGuiUtil.DragFloatVertical(Language.Options_JapaneseFontSize_Name, ref this.Mutable.JapaneseFontSize, speed, min, max, $"{this.Mutable.JapaneseFontSize:N1}");
        ImGuiUtil.DragFloatVertical(Language.Options_SymbolsFontSize_Name, ref this.Mutable.SymbolsFontSize, speed, min, max, $"{this.Mutable.SymbolsFontSize:N1}");
        ImGuiUtil.HelpText(Language.Options_SymbolsFontSize_Description);

        ImGui.PopTextWrapPos();
    }
}
