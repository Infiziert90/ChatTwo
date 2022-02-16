using System.Numerics;
using System.Text;
using Dalamud.Game.Text.SeStringHandling;
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

    private static Payload? _hovered;
    private static Payload? _lastLink;
    private static readonly List<(Vector2, Vector2)> PayloadBounds = new();

    internal static void PostPayload(Chunk chunk, PayloadHandler? handler) {
        var payload = chunk.Link;
        if (payload != null && ImGui.IsItemHovered()) {
            _hovered = payload;
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            handler?.Hover(payload);
        } else if (!ReferenceEquals(_hovered, payload)) {
            _hovered = null;
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

    internal static unsafe void WrapText(string csText, Chunk chunk, PayloadHandler? handler, Vector4 defaultText, float lineWidth) {
        void Text(byte* text, byte* textEnd) {
            var oldPos = ImGui.GetCursorScreenPos();

            ImGuiNative.igTextUnformatted(text, textEnd);
            PostPayload(chunk, handler);

            if (!ReferenceEquals(_lastLink, chunk.Link)) {
                PayloadBounds.Clear();
            }

            _lastLink = chunk.Link;

            if (_hovered != null && ReferenceEquals(_hovered, chunk.Link)) {
                defaultText.W = 0.25f;
                var actualCol = ColourUtil.Vector4ToAbgr(defaultText);
                ImGui.GetWindowDrawList().AddRectFilled(oldPos, oldPos + ImGui.GetItemRectSize(), actualCol);

                foreach (var (start, size) in PayloadBounds) {
                    ImGui.GetWindowDrawList().AddRectFilled(start, start + size, actualCol);
                }

                PayloadBounds.Clear();
            }

            if (_hovered == null && chunk.Link != null) {
                PayloadBounds.Add((oldPos, ImGui.GetItemRectSize()));
            }
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
                    continue;
                }

                var widthLeft = ImGui.GetContentRegionAvail().X;
                var endPrevLine = ImGuiNative.ImFont_CalcWordWrapPositionA(ImGui.GetFont().NativePtr, ImGuiHelpers.GlobalScale, text, textEnd, widthLeft);
                if (endPrevLine == null) {
                    continue;
                }

                var firstSpace = FindFirstSpace(text, textEnd);
                var properBreak = firstSpace <= endPrevLine;
                if (properBreak) {
                    Text(text, endPrevLine);
                } else {
                    if (lineWidth == 0f) {
                        ImGui.TextUnformatted("");
                    } else {
                        // check if the next bit is longer than the entire line width
                        var wrapPos = ImGuiNative.ImFont_CalcWordWrapPositionA(ImGui.GetFont().NativePtr, ImGuiHelpers.GlobalScale, text, firstSpace, lineWidth);
                        if (wrapPos >= firstSpace) {
                            // only go to next line is it's going to wrap at the space
                            ImGui.TextUnformatted("");
                        }
                    }
                }

                widthLeft = ImGui.GetContentRegionAvail().X;
                while (endPrevLine < textEnd) {
                    if (properBreak) {
                        text = endPrevLine;
                    }

                    if (*text == ' ') {
                        ++text;
                    } // skip a space at start of line

                    var newEnd = ImGuiNative.ImFont_CalcWordWrapPositionA(ImGui.GetFont().NativePtr, ImGuiHelpers.GlobalScale, text, textEnd, widthLeft);
                    if (properBreak && newEnd == endPrevLine) {
                        break;
                    }

                    endPrevLine = newEnd;
                    if (endPrevLine == null) {
                        ImGui.TextUnformatted("");
                        ImGui.TextUnformatted("");
                        break;
                    }

                    Text(text, endPrevLine);

                    if (!properBreak) {
                        properBreak = true;
                        widthLeft = ImGui.GetContentRegionAvail().X;
                    }
                }
            }
        }
    }

    private static unsafe byte* FindFirstSpace(byte* text, byte* textEnd) {
        for (var i = text; i < textEnd; i++) {
            if (char.IsWhiteSpace((char) *i)) {
                return i;
            }
        }

        return textEnd;
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

        ImGui.PushTextWrapPos();
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();

        if (dalamudOrange != null) {
            ImGui.PopStyleColor();
        }
    }

    internal static bool BeginComboVertical(string label, string previewValue, ImGuiComboFlags flags = ImGuiComboFlags.None) {
        ImGui.TextUnformatted(label);
        ImGui.SetNextItemWidth(-1);
        return ImGui.BeginCombo($"##{label}", previewValue, flags);
    }

    internal static bool DragFloatVertical(string label, ref float value, float vSpeed = 1.0f, float vMin = float.MinValue, float vMax = float.MaxValue, string? format = null, ImGuiSliderFlags flags = ImGuiSliderFlags.None) {
        ImGui.TextUnformatted(label);
        ImGui.SetNextItemWidth(-1);
        return ImGui.DragFloat($"##{label}", ref value, vSpeed, vMin, vMax, format, flags);
    }
}
