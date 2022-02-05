using System.Text;
using Dalamud.Interface;
using Dalamud.Interface.Style;
using ImGuiNET;

namespace ChatTwo.Util;

internal static class ImGuiUtil {
    private static readonly ImGuiMouseButton[] Buttons = {
        ImGuiMouseButton.Left,
        ImGuiMouseButton.Middle,
        ImGuiMouseButton.Right,
    };

    internal static void PostPayload(Chunk chunk, PayloadHandler? handler) {
        var payload = chunk.Link;
        if (payload != null && ImGui.IsItemHovered()) {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            handler?.Hover(payload);
        }

        if (handler == null) {
            return;
        }

        foreach (var button in Buttons) {
            if (ImGui.IsItemClicked(button)) {
                handler.Click(chunk, payload, button);
            }
        }
    }

    internal static unsafe void WrapText(string csText, Chunk chunk, PayloadHandler? handler) {
        void Text(byte* text, byte* textEnd) {
            ImGuiNative.igTextUnformatted(text, textEnd);
            PostPayload(chunk, handler);
        }

        if (csText.Length == 0) {
            return;
        }

        foreach (var part in csText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)) {
            var bytes = Encoding.UTF8.GetBytes(part);
            fixed (byte* rawText = bytes) {
                var text = rawText;
                var textEnd = text + bytes.Length;

                // empty string
                if (text == null) {
                    ImGui.TextUnformatted("");
                    ImGui.TextUnformatted("");
                    return;
                }

                var widthLeft = ImGui.GetContentRegionAvail().X;
                var endPrevLine = ImGuiNative.ImFont_CalcWordWrapPositionA(ImGui.GetFont().NativePtr, ImGuiHelpers.GlobalScale, text, textEnd, widthLeft);
                if (endPrevLine == null) {
                    return;
                }

                Text(text, endPrevLine);

                widthLeft = ImGui.GetContentRegionAvail().X;
                while (endPrevLine < textEnd) {
                    text = endPrevLine;
                    if (*text == ' ') {
                        ++text;
                    } // skip a space at start of line

                    var newEnd = ImGuiNative.ImFont_CalcWordWrapPositionA(ImGui.GetFont().NativePtr, ImGuiHelpers.GlobalScale, text, textEnd, widthLeft);
                    if (newEnd == endPrevLine) {
                        break;
                    }

                    endPrevLine = newEnd;
                    if (endPrevLine == null) {
                        ImGui.TextUnformatted("");
                        ImGui.TextUnformatted("");
                        break;
                    }

                    Text(text, endPrevLine);
                }
            }
        }
    }

    internal static bool IconButton(FontAwesomeIcon icon, string? id = null, string? tooltip = null) {
        ImGui.PushFont(UiBuilder.IconFont);

        var label = icon.ToIconString();
        if (id != null) {
            label += $"##{id}";
        }

        var ret = ImGui.Button(label);

        ImGui.PopFont();

        if (tooltip != null && ImGui.IsItemHovered()) {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted(tooltip);
            ImGui.EndTooltip();
        }

        return ret;
    }

    internal static bool OptionCheckbox(ref bool value, string label, string? description = null) {
        var ret = ImGui.Checkbox(label, ref value);

        if (description != null) {
            HelpText(description);
        }

        return ret;
    }

    internal static void HelpText(string text) {
        var colour = ImGui.GetStyle().Colors[(int) ImGuiCol.TextDisabled];
        ImGui.PushStyleColor(ImGuiCol.Text, colour);
        ImGui.PushTextWrapPos();

        try {
            ImGui.TextUnformatted(text);
        } finally {
            ImGui.PopTextWrapPos();
            ImGui.PopStyleColor();
        }
    }

    internal static void WarningText(string text) {
        var style = StyleModel.GetConfiguredStyle() ?? StyleModel.GetFromCurrent();
        var dalamudOrange = style.BuiltInColors?.DalamudOrange;
        if (dalamudOrange != null) {
            ImGui.PushStyleColor(ImGuiCol.Text, dalamudOrange.Value);
        }

        ImGui.TextUnformatted(text);

        if (dalamudOrange != null) {
            ImGui.PopStyleColor();
        }
    }
}
