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
        Mutable = mutable;
        UpdateFonts();
    }

    private void UpdateFonts() {
        GlobalFonts = Ui.Fonts.GetFonts();
        JpFonts = Ui.Fonts.GetJpFonts();
    }

    public void Draw(bool changed) {
        if (changed) {
            UpdateFonts();
        }

        ImGui.PushTextWrapPos();

        ImGui.Checkbox(Language.Options_FontsEnabled, ref Mutable.FontsEnabled);
        ImGui.Spacing();

        if (Mutable.FontsEnabled) {
            if (ImGuiUtil.BeginComboVertical(Language.Options_Font_Name, Mutable.GlobalFont)) {
                foreach (var font in Ui.Fonts.GlobalFonts) {
                    if (ImGui.Selectable(font.Name, Mutable.GlobalFont == font.Name)) {
                        Mutable.GlobalFont = font.Name;
                    }

                    if (ImGui.IsWindowAppearing() && Mutable.GlobalFont == font.Name) {
                        ImGui.SetScrollHereY(0.5f);
                    }
                }

                ImGui.Separator();

                foreach (var name in GlobalFonts) {
                    if (ImGui.Selectable(name, Mutable.GlobalFont == name)) {
                        Mutable.GlobalFont = name;
                    }

                    if (ImGui.IsWindowAppearing() && Mutable.GlobalFont == name) {
                        ImGui.SetScrollHereY(0.5f);
                    }
                }

                ImGui.EndCombo();
            }

            ImGuiUtil.HelpText(string.Format(Language.Options_Font_Description, Plugin.PluginName));
            ImGuiUtil.WarningText(Language.Options_Font_Warning);
            ImGui.Spacing();

            if (ImGuiUtil.BeginComboVertical(Language.Options_JapaneseFont_Name, Mutable.JapaneseFont)) {
                foreach (var (name, _) in Ui.Fonts.JapaneseFonts) {
                    if (ImGui.Selectable(name, Mutable.JapaneseFont == name)) {
                        Mutable.JapaneseFont = name;
                    }

                    if (ImGui.IsWindowAppearing() && Mutable.JapaneseFont == name) {
                        ImGui.SetScrollHereY(0.5f);
                    }
                }

                ImGui.Separator();

                foreach (var family in JpFonts) {
                    if (ImGui.Selectable(family, Mutable.JapaneseFont == family)) {
                        Mutable.JapaneseFont = family;
                    }

                    if (ImGui.IsWindowAppearing() && Mutable.JapaneseFont == family) {
                        ImGui.SetScrollHereY(0.5f);
                    }
                }

                ImGui.EndCombo();
            }

            ImGuiUtil.HelpText(string.Format(Language.Options_JapaneseFont_Description, Plugin.PluginName));
            ImGui.Spacing();

            if (ImGui.CollapsingHeader(Language.Options_ExtraGlyphs_Name)) {
                ImGuiUtil.HelpText(string.Format(Language.Options_ExtraGlyphs_Description, Plugin.PluginName));

                var range = (int) Mutable.ExtraGlyphRanges;
                foreach (var extra in Enum.GetValues<ExtraGlyphRanges>()) {
                    ImGui.CheckboxFlags(extra.Name(), ref range, (int) extra);
                }

                Mutable.ExtraGlyphRanges = (ExtraGlyphRanges) range;
            }

            ImGui.Spacing();
        }

        const float speed = .0125f;
        const float min = 8f;
        const float max = 36f;
        ImGuiUtil.DragFloatVertical(Language.Options_FontSize_Name, ref Mutable.FontSize, speed, min, max, $"{Mutable.FontSize:N1}");
        ImGuiUtil.DragFloatVertical(Language.Options_JapaneseFontSize_Name, ref Mutable.JapaneseFontSize, speed, min, max, $"{Mutable.JapaneseFontSize:N1}");
        ImGuiUtil.DragFloatVertical(Language.Options_SymbolsFontSize_Name, ref Mutable.SymbolsFontSize, speed, min, max, $"{Mutable.SymbolsFontSize:N1}");
        ImGuiUtil.HelpText(Language.Options_SymbolsFontSize_Description);

        ImGui.PopTextWrapPos();
    }
}
