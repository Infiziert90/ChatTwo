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
        Mutable = mutable;
        Plugin = plugin;

        #if DEBUG
        var sortable = ChatTypeExt.SortOrder
            .SelectMany(entry => entry.Item2)
            // Users can set colours for ExtraChat linkshells in the ExtraChat
            // plugin directly.
            .Where(type => !type.IsGm() && !type.IsExtraChatLinkshell())
            .ToHashSet();
        var total = Enum.GetValues<ChatType>()
            .Where(type => !type.IsGm() && !type.IsExtraChatLinkshell())
            .ToHashSet();
        if (sortable.Count != total.Count) {
            Plugin.Log.Warning($"There are {sortable.Count} sortable channels, but there are {total.Count} total channels.");
            total.ExceptWith(sortable);
            foreach (var missing in total) {
                Plugin.Log.Information($"Missing {missing}");
            }
        }
        #endif
    }

    public void Draw(bool changed) {
        foreach (var (_, types) in ChatTypeExt.SortOrder) {
            foreach (var type in types) {
                if (ImGuiUtil.IconButton(FontAwesomeIcon.UndoAlt, $"{type}", Language.Options_ChatColours_Reset)) {
                    Mutable.ChatColours.Remove(type);
                }

                ImGui.SameLine();

                if (ImGuiUtil.IconButton(FontAwesomeIcon.LongArrowAltDown, $"{type}", Language.Options_ChatColours_Import)) {
                    var gameColour = Plugin.Functions.Chat.GetChannelColour(type);
                    Mutable.ChatColours[type] = gameColour ?? type.DefaultColour() ?? 0;
                }

                ImGui.SameLine();

                var vec = Mutable.ChatColours.TryGetValue(type, out var colour)
                    ? ColourUtil.RgbaToVector3(colour)
                    : ColourUtil.RgbaToVector3(type.DefaultColour() ?? 0);
                if (ImGui.ColorEdit3(type.Name(), ref vec, ImGuiColorEditFlags.NoInputs)) {
                    Mutable.ChatColours[type] = ColourUtil.Vector3ToRgba(vec);
                }
            }
        }
    }
}
