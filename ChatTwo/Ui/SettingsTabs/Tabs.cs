using ChatTwo.Code;
using ChatTwo.Resources;
using ChatTwo.Util;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;

namespace ChatTwo.Ui.SettingsTabs;

internal sealed class Tabs : ISettingsTab
{
    private readonly Plugin Plugin;
    private Configuration Mutable { get; }

    public string Name => Language.Options_Tabs_Tab + "###tabs-tabs";

    private int ToOpen = -2;

    internal Tabs(Plugin plugin, Configuration mutable)
    {
        Plugin = plugin;
        Mutable = mutable;
    }

    public void Draw(bool changed)
    {
        const string addTabPopup = "add-tab-popup";

        if (ImGuiUtil.IconButton(FontAwesomeIcon.Plus, tooltip: Language.Options_Tabs_Add))
            ImGui.OpenPopup(addTabPopup);

        using (var popup = ImRaii.Popup(addTabPopup))
        {
            if (popup)
            {
                if (ImGui.Selectable(Language.Options_Tabs_NewTab))
                    Mutable.Tabs.Add(new Tab());

                ImGui.Separator();

                if (ImGui.Selectable(string.Format(Language.Options_Tabs_Preset, Language.Tabs_Presets_General)))
                    Mutable.Tabs.Add(TabsUtil.VanillaGeneral);

                if (ImGui.Selectable(string.Format(Language.Options_Tabs_Preset, Language.Tabs_Presets_Event)))
                    Mutable.Tabs.Add(TabsUtil.VanillaEvent);

                if (ImGui.Selectable(string.Format(Language.Options_Tabs_Preset, Language.Tabs_Presets_Tell)))
                    Mutable.Tabs.Add(TabsUtil.VanillaTellExclusive);
            }
        }

        var toRemove = -1;
        var doOpens = ToOpen > -2;
        for (var i = 0; i < Mutable.Tabs.Count; i++)
        {
            var tab = Mutable.Tabs[i];

            if (doOpens)
                ImGui.SetNextItemOpen(i == ToOpen);

            using var treeNode = ImRaii.TreeNode($"{tab.Name}###tab-{i}");
            if (!treeNode.Success)
                continue;

            using var pushedId = ImRaii.PushId($"tab-{i}");

            if (ImGuiUtil.IconButton(FontAwesomeIcon.TrashAlt, tooltip: Language.Options_Tabs_Delete))
            {
                toRemove = i;
                ToOpen = -1;
            }

            ImGui.SameLine();

            if (ImGuiUtil.IconButton(FontAwesomeIcon.ArrowUp, tooltip: Language.Options_Tabs_MoveUp) && i > 0)
            {
                (Mutable.Tabs[i - 1], Mutable.Tabs[i]) = (Mutable.Tabs[i], Mutable.Tabs[i - 1]);
                ToOpen = i - 1;
            }

            ImGui.SameLine();

            if (ImGuiUtil.IconButton(FontAwesomeIcon.ArrowDown, tooltip: Language.Options_Tabs_MoveDown) && i < Mutable.Tabs.Count - 1)
            {
                (Mutable.Tabs[i + 1], Mutable.Tabs[i]) = (Mutable.Tabs[i], Mutable.Tabs[i + 1]);
                ToOpen = i + 1;
            }

            ImGui.InputText(Language.Options_Tabs_Name, ref tab.Name, 512, ImGuiInputTextFlags.EnterReturnsTrue);
            ImGui.Checkbox(Language.Options_Tabs_ShowTimestamps, ref tab.DisplayTimestamp);
            ImGui.Checkbox(Language.Options_Tabs_PopOut, ref tab.PopOut);
            if (tab.PopOut)
            {
                using var _ = ImRaii.PushIndent(10.0f);
                ImGui.Checkbox(Language.Options_Tabs_IndependentOpacity, ref tab.IndependentOpacity);
                if (tab.IndependentOpacity)
                    ImGuiUtil.DragFloatVertical(Language.Options_Tabs_Opacity, ref tab.Opacity, 0.25f, 0f, 100f, $"{tab.Opacity:N2}%%", ImGuiSliderFlags.AlwaysClamp);

                ImGui.Checkbox(Language.Options_Tabs_IndependentHide, ref tab.IndependentHide);
                if (tab.IndependentHide)
                {
                    using var __ = ImRaii.PushIndent(10.0f);
                    ImGuiUtil.OptionCheckbox(ref tab.HideDuringCutscenes, Language.Options_HideDuringCutscenes_Name);
                    ImGui.Spacing();

                    ImGuiUtil.OptionCheckbox(ref tab.HideWhenNotLoggedIn, Language.Options_HideWhenNotLoggedIn_Name);
                    ImGui.Spacing();

                    ImGuiUtil.OptionCheckbox(ref tab.HideWhenUiHidden, Language.Options_HideWhenUiHidden_Name);
                    ImGui.Spacing();

                    ImGuiUtil.OptionCheckbox(ref tab.HideInLoadingScreens, Language.Options_HideInLoadingScreens_Name);
                    ImGui.Spacing();

                    ImGuiUtil.OptionCheckbox(ref tab.HideInBattle, Language.Options_HideInBattle_Name);
                    ImGui.Spacing();
                }

                ImGuiUtil.OptionCheckbox(ref tab.CanMove, Language.Popout_CanMove_Name);
                ImGui.Spacing();

                ImGuiUtil.OptionCheckbox(ref tab.CanResize, Language.Popout_CanResize_Name);
                ImGui.Spacing();
            }

            using (var combo = ImGuiUtil.BeginComboVertical(Language.Options_Tabs_UnreadMode, tab.UnreadMode.Name()))
            {
                if (combo.Success)
                {
                    foreach (var mode in Enum.GetValues<UnreadMode>())
                    {
                        if (ImGui.Selectable(mode.Name(), tab.UnreadMode == mode))
                            tab.UnreadMode = mode;

                        if (mode.Tooltip() is { } tooltip && ImGui.IsItemHovered())
                            ImGuiUtil.Tooltip(tooltip);
                    }
                }
            }

            if (Mutable.HideWhenInactive)
                ImGui.Checkbox(Language.Options_Tabs_InactivityBehaviour, ref tab.UnhideOnActivity);

            ImGui.Checkbox(Language.Options_Tabs_NoInput, ref tab.InputDisabled);
            if (!tab.InputDisabled)
            {
                var input = tab.Channel?.ToChatType().Name() ?? Language.Options_Tabs_NoInputChannel;
                using (var combo = ImGuiUtil.BeginComboVertical(Language.Options_Tabs_InputChannel, input))
                {
                    if (combo.Success)
                    {
                        if (ImGui.Selectable(Language.Options_Tabs_NoInputChannel, tab.Channel == null))
                            tab.Channel = null;

                        foreach (var channel in Enum.GetValues<InputChannel>())
                            if (ImGui.Selectable(channel.ToChatType().Name(), tab.Channel == channel))
                                tab.Channel = channel;
                    }
                }

                var player = Plugin.ObjectTable.LocalPlayer;
                if (tab.Channel == InputChannel.Tell && player != null)
                {
                    ImGui.Checkbox(Language.Options_Tabs_SenderMessages, ref tab.AllSenderMessages);
                    ImGuiUtil.HelpText(Language.Options_Help_SenderMessages);

                    var worlds = Sheets.WorldsOnDatacenter(player).OrderByDescending(world => world.DataCenter.RowId).ThenBy(world => world.Name.ToString()).ToList();

                    using (ImRaii.ItemWidth(ImGui.GetWindowWidth() / 3f))
                    {
                        ImGui.Text(Language.Options_Header_Target);
                        ImGui.SameLine();

                        var name = tab.TellTarget.Name;
                        if (ImGui.InputText("##targetInput", ref name, 21))
                            tab.TellTarget.Name = name;

                        ImGui.SameLine();

                        var selectedWorld = worlds.FindIndex(world => world.RowId == tab.TellTarget.World);
                        if (selectedWorld == -1)
                            selectedWorld = 0;

                        using (var combo = ImRaii.Combo("###player-world", worlds[selectedWorld].Name.ToString()))
                        {
                            if (combo.Success)
                            {
                                var lastDc = worlds.First().DataCenter.RowId;
                                foreach (var (idx, world) in worlds.Index())
                                {
                                    if (ImGui.Selectable(world.Name.ToString(), selectedWorld == idx))
                                    {
                                        selectedWorld = idx;
                                        tab.TellTarget.World = worlds[selectedWorld].RowId;
                                    }

                                    if (lastDc == world.DataCenter.RowId)
                                        continue;

                                    lastDc = world.DataCenter.RowId;
                                    ImGui.Separator();
                                }
                            }
                        }
                    }

                    var target = (Plugin.TargetManager.SoftTarget ?? Plugin.TargetManager.Target) as IPlayerCharacter;
                    using (ImRaii.Disabled(target == null))
                    {
                        if (ImGui.Button("Set to target") && target != null)
                            tab.TellTarget.FromTarget(target);
                    }
                }
            }

            ImGuiUtil.ChannelSelector(Language.Options_Tabs_Channels, tab.SelectedChannels);
            ImGuiUtil.ExtraChatSelector(Language.Options_Tabs_ExtraChatChannels, ref tab.ExtraChatAll, tab.ExtraChatChannels);
        }

        if (toRemove > -1)
        {
            Mutable.Tabs.RemoveAt(toRemove);
            Plugin.WantedTab = 0;
        }

        if (doOpens)
            ToOpen = -2;
    }
}
