using ChatTwo.Code;
using ChatTwo.Util;
using ImGuiNET;

namespace ChatTwo.Ui.SettingsTabs;

internal sealed class ChatColours : ISettingsTab {
    private Configuration Mutable { get; }

    public string Name => "Chat colours";

    internal ChatColours(Configuration mutable) {
        this.Mutable = mutable;
    }

    public void Draw() {
        foreach (var type in Enum.GetValues<ChatType>()) {
            if (ImGui.Button($"Default##{type}")) {
                this.Mutable.ChatColours.Remove(type);
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
