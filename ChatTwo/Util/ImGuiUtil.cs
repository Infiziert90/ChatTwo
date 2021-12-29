using System.Text;
using ImGuiNET;

namespace ChatTwo.Util; 

internal static class ImGuiUtil {
    internal static unsafe void WrapText(string csText) {
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

                ImGuiNative.igTextUnformatted(text, endPrevLine);

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

                    ImGuiNative.igTextUnformatted(text, endPrevLine);
                }
            }
        }
    }
}
