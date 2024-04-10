using ChatTwo.Resources;
using ChatTwo.Util;
using ImGuiNET;

namespace ChatTwo.Ui.SettingsTabs;

internal sealed class Miscellaneous : ISettingsTab {
    private Configuration Mutable { get; }

    public string Name => Language.Options_Miscellaneous_Tab + "###tabs-miscellaneous";

    public Miscellaneous(Configuration mutable) {
        Mutable = mutable;
    }

    public void Draw(bool changed) {
        if (ImGuiUtil.BeginComboVertical(Language.Options_Language_Name, Mutable.LanguageOverride.Name())) {
            foreach (var language in Enum.GetValues<LanguageOverride>()) {
                if (ImGui.Selectable(language.Name())) {
                    Mutable.LanguageOverride = language;
                }
            }

            ImGui.EndCombo();
        }

        ImGuiUtil.HelpText(string.Format(Language.Options_Language_Description, Plugin.PluginName));
        ImGui.Spacing();

        if (ImGuiUtil.BeginComboVertical(Language.Options_CommandHelpSide_Name, Mutable.CommandHelpSide.Name())) {
            foreach (var side in Enum.GetValues<CommandHelpSide>()) {
                if (ImGui.Selectable(side.Name(), Mutable.CommandHelpSide == side)) {
                    Mutable.CommandHelpSide = side;
                }
            }

            ImGui.EndCombo();
        }

        ImGuiUtil.HelpText(string.Format(Language.Options_CommandHelpSide_Description, Plugin.PluginName));
        ImGui.Spacing();

        if (ImGuiUtil.BeginComboVertical(Language.Options_KeybindMode_Name, Mutable.KeybindMode.Name())) {
            foreach (var mode in Enum.GetValues<KeybindMode>()) {
                if (ImGui.Selectable(mode.Name(), Mutable.KeybindMode == mode)) {
                    Mutable.KeybindMode = mode;
                }

                if (ImGui.IsItemHovered()) {
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted(mode.Tooltip());
                    ImGui.EndTooltip();
                }
            }

            ImGui.EndCombo();
        }

        ImGuiUtil.HelpText(string.Format(Language.Options_KeybindMode_Description, Plugin.PluginName));
        ImGui.Spacing();

        ImGui.Checkbox(Language.Options_SortAutoTranslate_Name, ref Mutable.SortAutoTranslate);
        ImGuiUtil.HelpText(Language.Options_SortAutoTranslate_Description);
        ImGui.Spacing();
    }
}
