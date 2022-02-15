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
    private static readonly List<(Vector2, Vector2)> _payloadBounds = new();

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

    internal static unsafe void WrapText(string csText, Chunk chunk, PayloadHandler? handler, Vector4 defaultText) {
        void Text(byte* text, byte* textEnd) {
            var oldPos = ImGui.GetCursorScreenPos();

            ImGuiNative.igTextUnformatted(text, textEnd);
            PostPayload(chunk, handler);

            if (!ReferenceEquals(_lastLink, chunk.Link)) {
                _payloadBounds.Clear();
            }

            _lastLink = chunk.Link;

            if (_hovered != null && ReferenceEquals(_hovered, chunk.Link)) {
                defaultText.W = 0.25f;
                var actualCol = ColourUtil.Vector4ToAbgr(defaultText);
                ImGui.GetWindowDrawList().AddRectFilled(oldPos, oldPos + ImGui.GetItemRectSize(), actualCol);

                foreach (var (start, size) in _payloadBounds) {
                    ImGui.GetWindowDrawList().AddRectFilled(start, start + size, actualCol);
                }

                _payloadBounds.Clear();
            }

            if (_hovered == null && chunk.Link != null) {
                _payloadBounds.Add((oldPos, ImGui.GetItemRectSize()));
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
