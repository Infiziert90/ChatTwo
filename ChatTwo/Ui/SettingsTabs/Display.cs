using System.Drawing;
using System.Drawing.Text;
using ChatTwo.Resources;
using ChatTwo.Util;
using ImGuiNET;

namespace ChatTwo.Ui.SettingsTabs;

internal sealed class Display : ISettingsTab {
    private Configuration Mutable { get; }
    private List<FontFamily> Fonts { get; } = new();
    private List<string> JpFonts { get; set; } = new();

    public string Name => Language.Options_Display_Tab + "###tabs-display";

    internal Display(Configuration mutable) {
        this.Mutable = mutable;
        this.UpdateFonts();
    }

    private void UpdateFonts() {
        this.Fonts.Clear();

        var fonts = new InstalledFontCollection();
        foreach (var font in fonts.Families) {
            this.Fonts.Add(font);
        }

        this.JpFonts = Ui.Fonts.GetJpFonts();
    }

    public void Draw() {
        if (ImGui.IsWindowAppearing()) {
            this.UpdateFonts();
        }

        ImGuiUtil.OptionCheckbox(ref this.Mutable.HideChat, Language.Options_HideChat_Name, Language.Options_HideChat_Description);
        ImGuiUtil.OptionCheckbox(ref this.Mutable.HideDuringCutscenes, Language.Options_HideDuringCutscenes_Name, Language.Options_HideDuringCutscenes_Description);
        ImGuiUtil.OptionCheckbox(ref this.Mutable.NativeItemTooltips, Language.Options_NativeItemTooltips_Name, Language.Options_NativeItemTooltips_Description);
        ImGuiUtil.OptionCheckbox(ref this.Mutable.SidebarTabView, Language.Options_SidebarTabView_Name, Language.Options_SidebarTabView_Description);
        ImGuiUtil.OptionCheckbox(ref this.Mutable.PrettierTimestamps, Language.Options_PrettierTimestamps_Name, Language.Options_PrettierTimestamps_Description);

        if (this.Mutable.PrettierTimestamps) {
            ImGui.TreePush();
            ImGuiUtil.OptionCheckbox(ref this.Mutable.MoreCompactPretty, Language.Options_MoreCompactPretty_Name, Language.Options_MoreCompactPretty_Description);
            ImGui.TreePop();
        }

        ImGuiUtil.OptionCheckbox(ref this.Mutable.ShowNoviceNetwork, Language.Options_ShowNoviceNetwork_Name, Language.Options_ShowNoviceNetwork_Description);

        if (ImGui.BeginCombo("Font", this.Mutable.GlobalFont)) {
            foreach (var font in Ui.Fonts.GlobalFonts) {
                if (ImGui.Selectable(font.Name, this.Mutable.GlobalFont == font.Name)) {
                    this.Mutable.GlobalFont = font.Name;
                }

                if (ImGui.IsWindowAppearing() && this.Mutable.GlobalFont == font.Name) {
                    ImGui.SetScrollHereY(0.5f);
                }
            }

            ImGui.Separator();

            foreach (var family in this.Fonts) {
                if (!family.IsStyleAvailable(FontStyle.Italic)) {
                    continue;
                }

                if (ImGui.Selectable(family.Name, this.Mutable.GlobalFont == family.Name)) {
                    this.Mutable.GlobalFont = family.Name;
                }

                if (ImGui.IsWindowAppearing() && this.Mutable.GlobalFont == family.Name) {
                    ImGui.SetScrollHereY(0.5f);
                }
            }

            ImGui.EndCombo();
        }

        if (ImGui.BeginCombo("Japanese font", this.Mutable.JapaneseFont)) {
            foreach (var (name, _) in Ui.Fonts.JapaneseFonts) {
                if (ImGui.Selectable(name, this.Mutable.JapaneseFont == name)) {
                    this.Mutable.JapaneseFont = name;
                }

                if (ImGui.IsWindowAppearing() && this.Mutable.JapaneseFont == name) {
                    ImGui.SetScrollHereY(0.5f);
                }
            }

            // ImGui.Separator();
            //
            // foreach (var family in this.JpFonts) {
            //     if (ImGui.Selectable(family, this.Mutable.JapaneseFont == family)) {
            //         this.Mutable.JapaneseFont = family;
            //     }
            //
            //     if (ImGui.IsWindowAppearing() && this.Mutable.JapaneseFont == family) {
            //         ImGui.SetScrollHereY(0.5f);
            //     }
            // }

            ImGui.EndCombo();
        }

        ImGui.DragFloat(Language.Options_FontSize_Name, ref this.Mutable.FontSize, .0125f, 12f, 36f, $"{this.Mutable.FontSize:N1}");
        if (ImGui.DragFloat(Language.Options_WindowOpacity_Name, ref this.Mutable.WindowAlpha, .0025f, 0f, 1f, $"{this.Mutable.WindowAlpha * 100f:N2}%%")) {
            switch (this.Mutable.WindowAlpha) {
                case > 1f and <= 100f:
                    this.Mutable.WindowAlpha /= 100f;
                    break;
                case < 0f or > 100f:
                    this.Mutable.WindowAlpha = 1f;
                    break;
            }
        }

        ImGuiUtil.OptionCheckbox(ref this.Mutable.CanMove, Language.Options_CanMove_Name);
        ImGuiUtil.OptionCheckbox(ref this.Mutable.CanResize, Language.Options_CanResize_Name);
        ImGuiUtil.OptionCheckbox(ref this.Mutable.ShowTitleBar, Language.Options_ShowTitleBar_Name);
    }
}
