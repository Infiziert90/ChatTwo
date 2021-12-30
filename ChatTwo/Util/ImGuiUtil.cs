using System.Text;
using Dalamud.Game.Text.SeStringHandling;
using ImGuiNET;

namespace ChatTwo.Util;

internal static class ImGuiUtil {
    private static readonly ImGuiMouseButton[] Buttons = {
        ImGuiMouseButton.Left,
        ImGuiMouseButton.Middle,
        ImGuiMouseButton.Right,
    };

    internal static void PostPayload(Payload? payload, PayloadHandler? handler) {
        if (payload != null && ImGui.IsItemHovered()) {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            handler?.Hover(payload);
        }

        if (payload == null || handler == null) {
            return;
        }

        foreach (var button in Buttons) {
            if (ImGui.IsItemClicked(button)) {
                handler.Click(payload, button);
            }
        }
    }

    internal static unsafe void WrapText(string csText, Payload? payload, PayloadHandler? handler) {
        void Text(byte* text, byte* textEnd) {
            ImGuiNative.igTextUnformatted(text, textEnd);
            PostPayload(payload, handler);
        }

        foreach (var part in csText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)) {
            var bytes = Encoding.UTF8.GetBytes(part);
            fixed (byte* rawText = bytes) {
                var text = rawText;
                var textEnd = text + bytes.Length;

                // idk how this is possible, but it is, I guess
                if (text == null) {
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
                        break;
                    }

                    Text(text, endPrevLine);
                }
            }
        }
    }
}
