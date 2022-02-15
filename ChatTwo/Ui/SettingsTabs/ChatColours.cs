using ChatTwo.Code;
using ChatTwo.Resources;
using ChatTwo.Util;
using Dalamud.Interface;
using ImGuiNET;

namespace ChatTwo.Ui.SettingsTabs;

internal sealed class ChatColours : ISettingsTab {
    private Configuration Mutable { get; }
    private Plugin Plugin { get; }

    public string Name => Language.Options_ChatColours_Tab + "###tabs-chat-colours";

    internal ChatColours(Configuration mutable, Plugin plugin) {
        this.Mutable = mutable;
        this.Plugin = plugin;

        #if DEBUG
        var sortable = ChatTypeExt.SortOrder
            .SelectMany(entry => entry.Item2)
            .Where(type => !type.IsGm())
            .ToHashSet();
        var total = Enum.GetValues<ChatType>().Where(type => !type.IsGm()).ToHashSet();
        if (sortable.Count != total.Count) {
            Dalamud.Logging.PluginLog.Warning($"There are {sortable.Count} sortable channels, but there are {total.Count} total channels.");
            total.ExceptWith(sortable);
            foreach (var missing in total) {
                Dalamud.Logging.PluginLog.Log($"Missing {missing}");
            }
        }
        #endif
    }

    public void Draw(bool changed) {
        foreach (var (_, types) in ChatTypeExt.SortOrder) {
            foreach (var type in types) {
                if (ImGuiUtil.IconButton(FontAwesomeIcon.UndoAlt, $"{type}", Language.Options_ChatColours_Reset)) {
                    this.Mutable.ChatColours.Remove(type);
                }

                ImGui.SameLine();

                if (ImGuiUtil.IconButton(FontAwesomeIcon.LongArrowAltDown, $"{type}", Language.Options_ChatColours_Import)) {
                    var gameColour = this.Plugin.Functions.Chat.GetChannelColour(type);
                    this.Mutable.ChatColours[type] = gameColour ?? type.DefaultColour() ?? 0;
                }

                ImGui.SameLine();

                var vec = this.Mutable.ChatColours.TryGetValue(type, out var colour)
                    ? ColourUtil.RgbaToVector3(colour)
                    : ColourUtil.RgbaToVector3(type.DefaultColour() ?? 0);
                if (ImGui.ColorEdit3(type.Name(), ref vec, ImGuiColorEditFlags.NoInputs)) {
                    this.Mutable.ChatColours[type] = ColourUtil.Vector3ToRgba(vec);
                }
            }
        }

        ImGui.TreePop();
    }
}
