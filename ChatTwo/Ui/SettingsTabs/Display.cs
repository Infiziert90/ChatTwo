using ChatTwo.Resources;
using ChatTwo.Util;
using Dalamud.Interface.Style;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;

namespace ChatTwo.Ui.SettingsTabs;

internal sealed class Display : ISettingsTab
{
    private Configuration Mutable { get; }

    public string Name => Language.Options_Display_Tab + "###tabs-display";

    internal Display(Configuration mutable)
    {
        Mutable = mutable;
    }

    public void Draw(bool changed)
    {
        using var wrap = ImGuiUtil.TextWrapPos();

        ImGuiUtil.OptionCheckbox(ref Mutable.HideChat, Language.Options_HideChat_Name, Language.Options_HideChat_Description);
        ImGui.Spacing();

        ImGuiUtil.OptionCheckbox(ref Mutable.HideDuringCutscenes, Language.Options_HideDuringCutscenes_Name, string.Format(Language.Options_HideDuringCutscenes_Description, Plugin.PluginName));
        ImGui.Spacing();

        ImGuiUtil.OptionCheckbox(ref Mutable.HideWhenNotLoggedIn, Language.Options_HideWhenNotLoggedIn_Name, string.Format(Language.Options_HideWhenNotLoggedIn_Description, Plugin.PluginName));
        ImGui.Spacing();

        ImGuiUtil.OptionCheckbox(ref Mutable.HideWhenUiHidden, Language.Options_HideWhenUiHidden_Name, string.Format(Language.Options_HideWhenUiHidden_Description, Plugin.PluginName));
        ImGui.Spacing();

        ImGuiUtil.OptionCheckbox(ref Mutable.HideInLoadingScreens, Language.Options_HideInLoadingScreens_Name, string.Format(Language.Options_HideInLoadingScreens_Description, Plugin.PluginName));
        ImGui.Spacing();

        ImGuiUtil.OptionCheckbox(ref Mutable.HideInBattle, Language.Options_HideInBattle_Name, Language.Options_HideInBattle_Description);
        ImGui.Spacing();

        ImGuiUtil.OptionCheckbox(ref Mutable.PrettierTimestamps, Language.Options_PrettierTimestamps_Name, Language.Options_PrettierTimestamps_Description);

        if (Mutable.PrettierTimestamps)
        {
            ImGui.TreePush();
            ImGuiUtil.OptionCheckbox(ref Mutable.MoreCompactPretty, Language.Options_MoreCompactPretty_Name, Language.Options_MoreCompactPretty_Description);
            ImGuiUtil.OptionCheckbox(ref Mutable.HideSameTimestamps, Language.Options_HideSameTimestamps_Name, Language.Options_HideSameTimestamps_Description);
            ImGui.TreePop();
        }
        ImGui.Spacing();

        ImGuiUtil.OptionCheckbox(ref Mutable.CollapseDuplicateMessages, Language.Options_CollapseDuplicateMessages_Name, Language.Options_CollapseDuplicateMessages_Description);
        ImGui.Spacing();
    }
}
