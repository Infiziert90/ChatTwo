using ChatTwo.Code;
using ChatTwo.Resources;
using ChatTwo.Util;
using Dalamud.Interface;
using ImGuiNET;

namespace ChatTwo.Ui.SettingsTabs;

internal sealed class Tabs : ISettingsTab {
    private Configuration Mutable { get; }

    public string Name => Language.Options_Tabs_Tab + "###tabs-tabs";

    private int _toOpen = -2;

    internal Tabs(Configuration mutable) {
        this.Mutable = mutable;
    }

    public void Draw(bool changed) {
        const string addTabPopup = "add-tab-popup";

        if (ImGuiUtil.IconButton(FontAwesomeIcon.Plus, tooltip: Language.Options_Tabs_Add)) {
            ImGui.OpenPopup(addTabPopup);
        }

        if (ImGui.BeginPopup(addTabPopup)) {
            if (ImGui.Selectable(Language.Options_Tabs_NewTab)) {
                this.Mutable.Tabs.Add(new Tab());
            }

            ImGui.Separator();

            if (ImGui.Selectable(string.Format(Language.Options_Tabs_Preset, Language.Tabs_Presets_General))) {
                this.Mutable.Tabs.Add(TabsUtil.VanillaGeneral);
            }

            if (ImGui.Selectable(string.Format(Language.Options_Tabs_Preset, Language.Tabs_Presets_Event))) {
                this.Mutable.Tabs.Add(TabsUtil.VanillaEvent);
            }

            ImGui.EndPopup();
        }

        var toRemove = -1;
        var doOpens = this._toOpen > -2;
        for (var i = 0; i < this.Mutable.Tabs.Count; i++) {
            var tab = this.Mutable.Tabs[i];

            if (doOpens) {
                ImGui.SetNextItemOpen(i == this._toOpen);
            }

            if (ImGui.TreeNodeEx($"{tab.Name}###tab-{i}")) {
                ImGui.PushID($"tab-{i}");

                if (ImGuiUtil.IconButton(FontAwesomeIcon.TrashAlt, tooltip: Language.Options_Tabs_Delete)) {
                    toRemove = i;
                    this._toOpen = -1;
                }

                ImGui.SameLine();

                if (ImGuiUtil.IconButton(FontAwesomeIcon.ArrowUp, tooltip: Language.Options_Tabs_MoveUp) && i > 0) {
                    (this.Mutable.Tabs[i - 1], this.Mutable.Tabs[i]) = (this.Mutable.Tabs[i], this.Mutable.Tabs[i - 1]);
                    this._toOpen = i - 1;
                }

                ImGui.SameLine();

                if (ImGuiUtil.IconButton(FontAwesomeIcon.ArrowDown, tooltip: Language.Options_Tabs_MoveDown) && i < this.Mutable.Tabs.Count - 1) {
                    (this.Mutable.Tabs[i + 1], this.Mutable.Tabs[i]) = (this.Mutable.Tabs[i], this.Mutable.Tabs[i + 1]);
                    this._toOpen = i + 1;
                }

                ImGui.InputText(Language.Options_Tabs_Name, ref tab.Name, 512, ImGuiInputTextFlags.EnterReturnsTrue);
                ImGui.Checkbox(Language.Options_Tabs_ShowTimestamps, ref tab.DisplayTimestamp);
                ImGui.Checkbox(Language.Options_Tabs_PopOut, ref tab.PopOut);
                if (tab.PopOut) {
                    ImGui.Checkbox(Language.Options_Tabs_IndependentOpacity, ref tab.IndependentOpacity);
                    if (tab.IndependentOpacity) {
                        ImGuiUtil.DragFloatVertical(Language.Options_Tabs_Opacity, ref tab.Opacity, 0.25f, 0f, 100f, $"{tab.Opacity:N2}%%", ImGuiSliderFlags.AlwaysClamp);
                    }
                }

                if (ImGuiUtil.BeginComboVertical(Language.Options_Tabs_UnreadMode, tab.UnreadMode.Name())) {
                    foreach (var mode in Enum.GetValues<UnreadMode>()) {
                        if (ImGui.Selectable(mode.Name(), tab.UnreadMode == mode)) {
                            tab.UnreadMode = mode;
                        }

                        if (mode.Tooltip() is { } tooltip && ImGui.IsItemHovered()) {
                            ImGui.BeginTooltip();
                            ImGui.TextUnformatted(tooltip);
                            ImGui.EndTooltip();
                        }
                    }

                    ImGui.EndCombo();
                }

                var input = tab.Channel?.ToChatType().Name() ?? Language.Options_Tabs_NoInputChannel;
                if (ImGuiUtil.BeginComboVertical(Language.Options_Tabs_InputChannel, input)) {
                    if (ImGui.Selectable(Language.Options_Tabs_NoInputChannel, tab.Channel == null)) {
                        tab.Channel = null;
                    }

                    foreach (var channel in Enum.GetValues<InputChannel>()) {
                        if (ImGui.Selectable(channel.ToChatType().Name(), tab.Channel == channel)) {
                            tab.Channel = channel;
                        }
                    }

                    ImGui.EndCombo();
                }

                if (ImGui.TreeNodeEx(Language.Options_Tabs_Channels)) {
                    foreach (var (header, types) in ChatTypeExt.SortOrder) {
                        if (ImGui.TreeNodeEx(header + $"##{i}")) {
                            foreach (var type in types) {
                                if (type.IsGm()) {
                                    continue;
                                }

                                var enabled = tab.ChatCodes.ContainsKey(type);
                                if (ImGui.Checkbox($"##{type.Name()}-{i}", ref enabled)) {
                                    if (enabled) {
                                        tab.ChatCodes[type] = ChatSourceExt.All;
                                    } else {
                                        tab.ChatCodes.Remove(type);
                                    }
                                }

                                ImGui.SameLine();

                                if (type.HasSource()) {
                                    if (ImGui.TreeNodeEx($"{type.Name()}##{i}")) {
                                        tab.ChatCodes.TryGetValue(type, out var sourcesEnum);
                                        var sources = (uint) sourcesEnum;

                                        foreach (var source in Enum.GetValues<ChatSource>()) {
                                            if (ImGui.CheckboxFlags(source.Name(), ref sources, (uint) source)) {
                                                tab.ChatCodes[type] = (ChatSource) sources;
                                            }
                                        }

                                        ImGui.TreePop();
                                    }
                                } else {
                                    ImGui.TextUnformatted(type.Name());
                                }
                            }

                            ImGui.TreePop();
                        }
                    }

                    ImGui.TreePop();
                }

                ImGui.TreePop();

                ImGui.PopID();
            }
        }

        if (toRemove > -1) {
            this.Mutable.Tabs.RemoveAt(toRemove);
        }

        if (doOpens) {
            this._toOpen = -2;
        }
    }
}
