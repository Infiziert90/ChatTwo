using System.Text;
using Dalamud.Interface;
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

        if (payload == null || handler == null) {
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

                const float scale = 1.0f;
                var widthLeft = ImGui.GetContentRegionAvail().X;
                var endPrevLine = ImGuiNative.ImFont_CalcWordWrapPositionA(ImGui.GetFont().NativePtr, scale, text, textEnd, widthLeft);
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

                    endPrevLine = ImGuiNative.ImFont_CalcWordWrapPositionA(ImGui.GetFont().NativePtr, scale, text, textEnd, widthLeft);
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

    internal static bool IconButton(FontAwesomeIcon icon, string? id = null) {
        ImGui.PushFont(UiBuilder.IconFont);

        var label = icon.ToIconString();
        if (id != null) {
            label += $"##{id}";
        }

        var ret = ImGui.Button(label);

        ImGui.PopFont();

        return ret;
    }
}
