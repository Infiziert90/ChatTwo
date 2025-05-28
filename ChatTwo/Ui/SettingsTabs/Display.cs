using ChatTwo.Code;
using ChatTwo.Resources;
using ChatTwo.Util;
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

        ImGui.Separator();
        ImGui.Spacing();

        ImGuiUtil.OptionCheckbox(ref Mutable.HideWhenInactive, Language.Options_HideWhenInactive_Name, Language.Options_HideWhenInactive_Description);
        ImGui.Spacing();

        if (Mutable.HideWhenInactive)
        {
            using var _ = ImRaii.PushIndent();
            ImGuiUtil.InputIntVertical(Language.Options_InactivityHideTimeout_Name,
                Language.Options_InactivityHideTimeout_Description, ref Mutable.InactivityHideTimeout, 1, 10);
            // Enforce a minimum of 2 seconds to avoid people soft locking
            // themselves.
            Mutable.InactivityHideTimeout = Math.Max(2, Mutable.InactivityHideTimeout);
            ImGui.Spacing();

            // This setting conflicts with HideInBattle, so it's disabled.
            using (ImRaii.Disabled(Mutable.HideInBattle))
            {
                ImGuiUtil.OptionCheckbox(ref Mutable.InactivityHideActiveDuringBattle,
                    Language.Options_InactivityHideActiveDuringBattle_Name,
                    Language.Options_InactivityHideActiveDuringBattle_Description);
                ImGui.Spacing();
            }

            using var channelTree = ImRaii.TreeNode(Language.Options_InactivityHideChannels_Name);
            if (channelTree.Success)
            {
                if (ImGuiUtil.CtrlShiftButton(Language.Options_InactivityHideChannels_All_Label,
                        Language.Options_InactivityHideChannels_Button_Tooltip))
                {
                    Mutable.InactivityHideChannels = TabsUtil.AllChannels();
                    Mutable.InactivityHideExtraChatAll = true;
                    Mutable.InactivityHideExtraChatChannels = [];
                }

                ImGui.SameLine();
                if (ImGuiUtil.CtrlShiftButton(Language.Options_InactivityHideChannels_None_Label,
                        Language.Options_InactivityHideChannels_Button_Tooltip))
                {
                    Mutable.InactivityHideChannels = new Dictionary<ChatType, ChatSource>();
                    Mutable.InactivityHideExtraChatAll = false;
                    Mutable.InactivityHideExtraChatChannels = [];
                }

                ImGui.Spacing();

                ImGuiUtil.ChannelSelector(Language.Options_Tabs_Channels, Mutable.InactivityHideChannels!);
                ImGuiUtil.ExtraChatSelector(Language.Options_Tabs_ExtraChatChannels,
                    ref Mutable.InactivityHideExtraChatAll, Mutable.InactivityHideExtraChatChannels);
            }
            ImGui.Spacing();
        }

        ImGui.Separator();
        ImGui.Spacing();

        ImGuiUtil.OptionCheckbox(ref Mutable.Use24HourClock, Language.Options_Use24HourClock_Name, Language.Options_Use24HourClock_Description);

        ImGuiUtil.OptionCheckbox(ref Mutable.PrettierTimestamps, Language.Options_PrettierTimestamps_Name, Language.Options_PrettierTimestamps_Description);

        if (Mutable.PrettierTimestamps)
        {
            using var _ = ImRaii.PushIndent();
            ImGuiUtil.OptionCheckbox(ref Mutable.MoreCompactPretty, Language.Options_MoreCompactPretty_Name, Language.Options_MoreCompactPretty_Description);
            ImGuiUtil.OptionCheckbox(ref Mutable.HideSameTimestamps, Language.Options_HideSameTimestamps_Name, Language.Options_HideSameTimestamps_Description);
        }
        ImGui.Spacing();

        ImGuiUtil.OptionCheckbox(ref Mutable.CollapseDuplicateMessages, Language.Options_CollapseDuplicateMessages_Name, Language.Options_CollapseDuplicateMessages_Description);
        if (Mutable.CollapseDuplicateMessages)
        {
            using var _ = ImRaii.PushIndent();
            ImGuiUtil.OptionCheckbox(ref Mutable.CollapseKeepUniqueLinks, Language.Options_CollapseDuplicateMsgUniqueLink_Name, Language.Options_CollapseDuplicateMsgUniqueLink_Description);
        }
        ImGui.Spacing();
    }
}
