using ChatTwo.Resources;
using ChatTwo.Util;
using ImGuiNET;

namespace ChatTwo.Ui.SettingsTabs;

internal sealed class Miscellaneous : ISettingsTab {
    private Configuration Mutable { get; }

    public string Name => Language.Options_Miscellaneous_Tab + "###tabs-miscellaneous";

    public Miscellaneous(Configuration mutable) {
        this.Mutable = mutable;
    }

    public void Draw() {
        ImGuiUtil.HelpText(string.Format(Language.Options_Language_Description, Plugin.PluginName));
        ImGui.Spacing();

        if (ImGui.BeginCombo(Language.Options_CommandHelpSide_Name, this.Mutable.CommandHelpSide.Name())) {
            foreach (var side in Enum.GetValues<CommandHelpSide>()) {
                if (ImGui.Selectable(side.Name(), this.Mutable.CommandHelpSide == side)) {
                    this.Mutable.CommandHelpSide = side;
                }
            }

            ImGui.EndCombo();
        }

        ImGuiUtil.HelpText(string.Format(Language.Options_CommandHelpSide_Description, Plugin.PluginName));
        ImGui.Spacing();

        if (ImGui.BeginCombo(Language.Options_KeybindMode_Name, this.Mutable.KeybindMode.Name())) {
            foreach (var mode in Enum.GetValues<KeybindMode>()) {
                if (ImGui.Selectable(mode.Name(), this.Mutable.KeybindMode == mode)) {
                    this.Mutable.KeybindMode = mode;
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
    }
}
