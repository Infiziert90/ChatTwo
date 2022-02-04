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
    }

    public void Draw() {
        foreach (var type in Enum.GetValues<ChatType>()) {
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

        ImGui.TreePop();
    }
}
