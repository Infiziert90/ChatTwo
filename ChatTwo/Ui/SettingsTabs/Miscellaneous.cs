using ChatTwo.Resources;
using ChatTwo.Util;
using Dalamud.Bindings.ImGui;

namespace ChatTwo.Ui.SettingsTabs;

internal sealed class Miscellaneous(Configuration mutable) : ISettingsTab
{
    private Configuration Mutable { get; } = mutable;
    public string Name => Language.Options_Miscellaneous_Tab + "###tabs-miscellaneous";

    public void Draw(bool changed)
    {
        using (var combo = ImGuiUtil.BeginComboVertical(Language.Options_Language_Name, Mutable.LanguageOverride.Name()))
        {
            if (combo.Success)
            {
                foreach (var language in Enum.GetValues<LanguageOverride>())
                    if (ImGui.Selectable(language.Name()))
                        Mutable.LanguageOverride = language;
            }
        }

        ImGuiUtil.HelpText(string.Format(Language.Options_Language_Description, Plugin.PluginName));
        ImGui.Spacing();

        using (var combo = ImGuiUtil.BeginComboVertical(Language.Options_CommandHelpSide_Name, Mutable.CommandHelpSide.Name()))
        {
            if (combo.Success)
            {
                foreach (var side in Enum.GetValues<CommandHelpSide>())
                    if (ImGui.Selectable(side.Name(), Mutable.CommandHelpSide == side))
                        Mutable.CommandHelpSide = side;
            }
        }

        ImGuiUtil.HelpText(string.Format(Language.Options_CommandHelpSide_Description, Plugin.PluginName));
        ImGui.Spacing();

        using (var combo = ImGuiUtil.BeginComboVertical(Language.Options_KeybindMode_Name, Mutable.KeybindMode.Name()))
        {
            if (combo.Success)
            {
                foreach (var mode in Enum.GetValues<KeybindMode>())
                {
                    if (ImGui.Selectable(mode.Name(), Mutable.KeybindMode == mode))
                        Mutable.KeybindMode = mode;

                    if (ImGui.IsItemHovered())
                        ImGuiUtil.Tooltip(mode.Tooltip() ?? "");
                }
            }
        }

        ImGuiUtil.HelpText(string.Format(Language.Options_KeybindMode_Description, Plugin.PluginName));
        ImGui.Spacing();

        ImGui.Checkbox(Language.Options_SortAutoTranslate_Name, ref Mutable.SortAutoTranslate);
        ImGuiUtil.HelpText(Language.Options_SortAutoTranslate_Description);
        ImGui.Spacing();
    }
}
