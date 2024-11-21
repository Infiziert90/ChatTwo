using System.Numerics;
using System.Text;
using ChatTwo.Code;
using ChatTwo.GameFunctions.Types;
using ChatTwo.Resources;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.FontIdentifier;
using Dalamud.Interface.ImGuiFontChooserDialog;
using Dalamud.Interface.Style;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;

namespace ChatTwo.Util;

internal static class ImGuiUtil
{
    private static Plugin Plugin = null!;

    public static void Initialize(Plugin plugin)
    {
        Plugin = plugin;
    }

    private static readonly ImGuiMouseButton[] Buttons =
    [
        ImGuiMouseButton.Left,
        ImGuiMouseButton.Middle,
        ImGuiMouseButton.Right
    ];

    private static Payload? Hovered;
    private static Payload? LastLink;
    private static readonly List<(Vector2, Vector2)> PayloadBounds = [];

    internal static void PostPayload(Chunk chunk, PayloadHandler? handler)
    {
        var payload = chunk.Link;
        if (payload != null && ImGui.IsItemHovered())
        {
            Hovered = payload;
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            handler?.Hover(payload);
        }
        else if (!ReferenceEquals(Hovered, payload))
        {
            Hovered = null;
        }

        if (handler == null)
            return;

        foreach (var button in Buttons)
            if (ImGui.IsItemClicked(button))
                handler.Click(chunk, payload, button);
    }

    internal static unsafe void WrapText(string csText, Chunk chunk, PayloadHandler? handler, Vector4 defaultText, float lineWidth)
    {
        void Text(byte* text, byte* textEnd)
        {
            var oldPos = ImGui.GetCursorScreenPos();

            ImGuiNative.igTextUnformatted(text, textEnd);
            PostPayload(chunk, handler);

            if (!ReferenceEquals(LastLink, chunk.Link))
                PayloadBounds.Clear();

            LastLink = chunk.Link;

            if (Hovered != null && ReferenceEquals(Hovered, chunk.Link))
            {
                defaultText.W = 0.25f;
                var actualCol = ColourUtil.Vector4ToAbgr(defaultText);
                ImGui.GetWindowDrawList().AddRectFilled(oldPos, oldPos + ImGui.GetItemRectSize(), actualCol);

                foreach (var (start, size) in PayloadBounds)
                    ImGui.GetWindowDrawList().AddRectFilled(start, start + size, actualCol);

                PayloadBounds.Clear();
            }

            if (Hovered == null && chunk.Link != null)
                PayloadBounds.Add((oldPos, ImGui.GetItemRectSize()));
        }

        if (csText.Length == 0)
            return;

        foreach (var part in csText.Split(["\r\n", "\r", "\n"], StringSplitOptions.None))
        {
            var bytes = Encoding.UTF8.GetBytes(part);
            fixed (byte* rawText = bytes)
            {
                var text = rawText;
                var textEnd = text + bytes.Length;

                // empty string
                if (text == null)
                {
                    ImGui.TextUnformatted("");
                    continue;
                }

                var widthLeft = ImGui.GetContentRegionAvail().X;
                var endPrevLine = ImGuiNative.ImFont_CalcWordWrapPositionA(ImGui.GetFont().NativePtr, ImGuiHelpers.GlobalScale, text, textEnd, widthLeft);
                if (endPrevLine == null)
                    continue;

                var firstSpace = FindFirstSpace(text, textEnd);
                var properBreak = firstSpace <= endPrevLine;
                if (properBreak)
                {
                    Text(text, endPrevLine);
                }
                else
                {
                    if (lineWidth == 0f)
                    {
                        ImGui.TextUnformatted("");
                    }
                    else
                    {
                        // check if the next bit is longer than the entire line width
                        var wrapPos = ImGuiNative.ImFont_CalcWordWrapPositionA(ImGui.GetFont().NativePtr, ImGuiHelpers.GlobalScale, text, firstSpace, lineWidth);

                        // only go to next line is it's going to wrap at the space
                        if (wrapPos >= firstSpace)
                            ImGui.TextUnformatted("");
                    }
                }

                widthLeft = ImGui.GetContentRegionAvail().X;
                while (endPrevLine < textEnd)
                {
                    if (properBreak)
                        text = endPrevLine;

                    // skip a space at start of line
                    if (*text == ' ')
                        ++text;

                    var newEnd = ImGuiNative.ImFont_CalcWordWrapPositionA(ImGui.GetFont().NativePtr, ImGuiHelpers.GlobalScale, text, textEnd, widthLeft);
                    if (properBreak && newEnd == endPrevLine)
                        break;

                    endPrevLine = newEnd;
                    if (endPrevLine == null)
                    {
                        ImGui.TextUnformatted("");
                        ImGui.TextUnformatted("");
                        break;
                    }

                    Text(text, endPrevLine);

                    if (!properBreak)
                    {
                        properBreak = true;
                        widthLeft = ImGui.GetContentRegionAvail().X;
                    }
                }
            }
        }
    }

    private static unsafe byte* FindFirstSpace(byte* text, byte* textEnd)
    {
        for (var i = text; i < textEnd; i++)
            if (char.IsWhiteSpace((char) *i))
                return i;

        return textEnd;
    }

    internal static bool IconButton(FontAwesomeIcon icon, string? id = null, string? tooltip = null, int width = 0)
    {
        var label = icon.ToIconString();
        if (id != null)
            label += $"##{id}";

        Plugin.FontManager.FontAwesome.Push();
        var size = new Vector2(0, 0);
        if (width > 0)
            size.X = width - 2 * ImGui.GetStyle().CellPadding.X;

        var ret = ImGui.Button(label, size);
        Plugin.FontManager.FontAwesome.Pop();

        if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            Tooltip(tooltip);

        return ret;
    }

    internal static bool OptionCheckbox(ref bool value, string label, string? description = null)
    {
        var ret = ImGui.Checkbox(label, ref value);
        if (description != null)
            HelpText(description);

        return ret;
    }

    internal static void HelpText(string text)
    {
        var colour = ImGui.GetStyle().Colors[(int) ImGuiCol.TextDisabled];

        using (TextWrapPos())
        using (ImRaii.PushColor(ImGuiCol.Text, colour))
        {
            ImGui.TextUnformatted(text);
        }
    }

    internal static void WarningText(string text, bool wrap = true)
    {
        var style = StyleModel.GetConfiguredStyle() ?? StyleModel.GetFromCurrent();
        var dalamudOrange = style.BuiltInColors?.DalamudOrange;

        using (TextWrapPos(wrap))
        using (ImRaii.PushColor(ImGuiCol.Text, dalamudOrange ?? Vector4.Zero, dalamudOrange != null))
        {
            ImGui.TextUnformatted(text);
        }
    }

    internal static ImRaii.IEndObject BeginComboVertical(string label, string previewValue, ImGuiComboFlags flags = ImGuiComboFlags.None)
    {
        ImGui.TextUnformatted(label);
        ImGui.SetNextItemWidth(-1);
        return ImRaii.Combo($"##{label}", previewValue, flags);
    }

    internal static bool DragFloatVertical(string label, ref float value, float vSpeed = 1.0f, float vMin = float.MinValue, float vMax = float.MaxValue, string? format = null, ImGuiSliderFlags flags = ImGuiSliderFlags.None)
    {
        ImGui.TextUnformatted(label);
        ImGui.SetNextItemWidth(-1);
        return ImGui.DragFloat($"##{label}", ref value, vSpeed, vMin, vMax, format, flags);
    }

    internal static bool DragFloatVertical(string label, string description, ref float value, float vSpeed = 1.0f, float vMin = float.MinValue, float vMax = float.MaxValue, string? format = null, ImGuiSliderFlags flags = ImGuiSliderFlags.None)
    {
        ImGui.TextUnformatted(label);
        ImGui.SetNextItemWidth(-1);
        var r = ImGui.DragFloat($"##{label}", ref value, vSpeed, vMin, vMax, format, flags);
        HelpText(description);

        return r;
    }

    internal static bool InputIntVertical(string label, string description, ref int value, int step = 1, int stepFast = 100, ImGuiInputTextFlags flags = ImGuiInputTextFlags.None)
    {
        ImGui.TextUnformatted(label);
        ImGui.SetNextItemWidth(-1);
        var r = ImGui.InputInt($"##{label}", ref value, step, stepFast, flags);
        HelpText(description);

        return r;
    }

    internal static void Tooltip(string tooltip)
    {
        using (ImRaii.Tooltip())
        using (ImRaii.TextWrapPos(ImGui.GetFontSize() * 35.0f))
        {
            ImGui.TextUnformatted(tooltip);
        }
    }

    public static SingleFontChooserDialog? FontChooser(string label, SingleFontSpec font, bool checkbox, ref bool checkboxValue, Predicate<IFontFamilyId>? exclusion = null, string? preview = null)
    {
        using var id = ImRaii.PushId(label);

        ImGui.TextUnformatted(label);
        if (checkbox)
        {
            ImGui.Checkbox("##enabled", ref checkboxValue);
            ImGui.SameLine();
        }

        var fontFamily = font.FontId.Family.EnglishName;
        var fontStyle = font.FontId.EnglishName;
        fontStyle = fontStyle.Equals(fontFamily) ? "" : $" - {fontStyle}";

        var buttonText = $"{fontFamily}{fontStyle} ({font.SizePt}pt)";
        if (!ImGui.Button($"{buttonText}##{label}"))
            return null;

        var chooser = SingleFontChooserDialog.CreateAuto((UiBuilder) Plugin.Interface.UiBuilder);
        chooser.SelectedFont = font;
        if (exclusion is not null)
            chooser.FontFamilyExcludeFilter = exclusion;
        if (preview is not null)
            chooser.PreviewText = preview;

        return chooser;
    }

    public static void FontSizeCombo(string label, ref float currentSize)
    {
        ImGui.TextUnformatted(label);
        ImGui.SetNextItemWidth(-1);
        using var combo = ImRaii.Combo($"##{label}", $"{currentSize:###.##}pt");
        if (combo)
        {
            foreach (var size in FontManager.AxisFontSizeList)
                if (ImGui.Selectable($"{size:###.##}pt", currentSize.Equals(size)))
                    currentSize = size;
        }
    }

    public static bool Button(string id, FontAwesomeIcon icon, bool disabled)
    {
        using (ImRaii.Disabled(disabled))
        {
            return ImGuiComponents.IconButton(id, icon);
        }
    }

    internal static bool CtrlShiftButton(string label, string tooltip = "")
    {
        var ctrlShiftHeld = ImGui.GetIO() is { KeyCtrl: true, KeyShift: true };

        bool ret;
        using (ImRaii.Disabled(!ctrlShiftHeld))
            ret = ImGui.Button(label) && ctrlShiftHeld;

        if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            Tooltip(tooltip);

        return ret;
    }

    internal static bool CtrlShiftButtonColored(string label, string tooltip = "")
    {
        var ctrlShiftHeld = ImGui.GetIO() is { KeyCtrl: true, KeyShift: true };

        var colorNormal = new Vector4(0.780f, 0.245f, 0.245f, 1.0f);
        var colorHovered = new Vector4(0.7f, 0.0f, 0.0f, 1.0f);
        using (ImRaii.PushColor(ImGuiCol.Button, colorNormal))
        using (ImRaii.PushColor(ImGuiCol.ButtonHovered, colorHovered))
        {
            var ret = ImGui.Button(label) && ctrlShiftHeld;

            if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                Tooltip(tooltip);

            return ret;
        }
    }

    internal static void KeybindInput(string id, ref ConfigKeyBind? keybind)
    {
        var idUint = ImGui.GetID(id);

        using var pushedId = ImRaii.PushId(id);
        if (ImGui.GetStateStorage().GetBool(idUint))
        {
            var io = ImGui.GetIO();
            var currentMods = ModifierFlag.None;
            var modString = "";
            if (io.KeyCtrl)
            {
                currentMods |= ModifierFlag.Ctrl;
                modString += Language.Keybind_Modifier_Ctrl + " + ";
            }
            if (io.KeyShift)
            {
                currentMods |= ModifierFlag.Shift;
                modString += Language.Keybind_Modifier_Shift + " + ";
            }
            if (io.KeyAlt)
            {
                currentMods |= ModifierFlag.Alt;
                modString += Language.Keybind_Modifier_Alt + " + ";
            }

            var text = $"{modString}... ({Language.Keybind_EscToClear})";
            using (ImRaii.PushColor(ImGuiCol.TextSelectedBg, Vector4.Zero))
            {
                ImGui.SetKeyboardFocusHere();
                ImGui.InputText(id + "##keybind", ref text, 0, ImGuiInputTextFlags.ReadOnly);
            }

            if (ImGui.IsKeyPressed(ImGuiKey.Escape))
            {
                keybind = null;
                ImGui.GetStateStorage().SetBool(idUint, false);
                return;
            }

            foreach (var vk in Enum.GetValues(typeof(VirtualKey)).Cast<VirtualKey>())
            {
                if (vk is VirtualKey.NO_KEY or VirtualKey.CONTROL or VirtualKey.LCONTROL or VirtualKey.RCONTROL or VirtualKey.SHIFT or VirtualKey.LSHIFT or VirtualKey.RSHIFT or VirtualKey.MENU or VirtualKey.LMENU or VirtualKey.RMENU)
                    continue;

                if (!TryToImGui(vk, out var imKey) || !ImGui.IsKeyPressed(imKey))
                    continue;

                keybind = new ConfigKeyBind
                {
                    Modifier = currentMods,
                    Key = vk
                };
                ImGui.GetStateStorage().SetBool(idUint, false);
                return;
            }
        }
        else
        {
            var text = $"({Language.Keybind_None})";
            if (keybind != null)
                text = keybind.ToString();
            if (ImGui.Button(text, new Vector2(-1, 0)))
                ImGui.GetStateStorage().SetBool(idUint, true);
        }
    }

    public static void DrawArrows(ref int selected, int min, int max, float spacing, int id = 0)
    {
        // Prevents changing values from triggering EndDisable
        var isMin = selected == min;
        var isMax = selected == max;

        ImGui.SameLine(0, spacing);
        using (ImRaii.Disabled(isMin))
        {
            if (IconButton(FontAwesomeIcon.ArrowLeft, id.ToString()))
                selected--;
        }

        ImGui.SameLine(0, spacing);
        using (ImRaii.Disabled(isMax))
        {
            if (IconButton(FontAwesomeIcon.ArrowRight, id+1.ToString()))
                selected++;
        }
    }

    public static void CenterText(string text, float indent = 0.0f)
    {
        indent *= ImGuiHelpers.GlobalScale;
        ImGui.SameLine(((ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(text).X) * 0.5f) + indent);
        ImGui.TextUnformatted(text);
    }

    internal static bool TryToImGui(this VirtualKey key, out ImGuiKey result)
    {
        result = key switch
        {
            VirtualKey.NO_KEY => ImGuiKey.None,
            VirtualKey.BACK => ImGuiKey.Backspace,
            VirtualKey.TAB => ImGuiKey.Tab,
            VirtualKey.RETURN => ImGuiKey.Enter,
            VirtualKey.SHIFT => ImGuiKey.ModShift,
            VirtualKey.CONTROL => ImGuiKey.ModCtrl,
            VirtualKey.MENU => ImGuiKey.ModAlt,
            VirtualKey.PAUSE => ImGuiKey.Pause,
            VirtualKey.CAPITAL => ImGuiKey.CapsLock,
            VirtualKey.ESCAPE => ImGuiKey.Escape,
            VirtualKey.SPACE => ImGuiKey.Space,
            VirtualKey.PRIOR => ImGuiKey.PageUp,
            VirtualKey.NEXT => ImGuiKey.PageDown,
            VirtualKey.END => ImGuiKey.End,
            VirtualKey.HOME => ImGuiKey.Home,
            VirtualKey.LEFT => ImGuiKey.LeftArrow,
            VirtualKey.UP => ImGuiKey.UpArrow,
            VirtualKey.RIGHT => ImGuiKey.RightArrow,
            VirtualKey.DOWN => ImGuiKey.DownArrow,
            VirtualKey.SNAPSHOT => ImGuiKey.PrintScreen,
            VirtualKey.INSERT => ImGuiKey.Insert,
            VirtualKey.DELETE => ImGuiKey.Delete,
            VirtualKey.KEY_0 => ImGuiKey._0,
            VirtualKey.KEY_1 => ImGuiKey._1,
            VirtualKey.KEY_2 => ImGuiKey._2,
            VirtualKey.KEY_3 => ImGuiKey._3,
            VirtualKey.KEY_4 => ImGuiKey._4,
            VirtualKey.KEY_5 => ImGuiKey._5,
            VirtualKey.KEY_6 => ImGuiKey._6,
            VirtualKey.KEY_7 => ImGuiKey._7,
            VirtualKey.KEY_8 => ImGuiKey._8,
            VirtualKey.KEY_9 => ImGuiKey._9,
            VirtualKey.A => ImGuiKey.A,
            VirtualKey.B => ImGuiKey.B,
            VirtualKey.C => ImGuiKey.C,
            VirtualKey.D => ImGuiKey.D,
            VirtualKey.E => ImGuiKey.E,
            VirtualKey.F => ImGuiKey.F,
            VirtualKey.G => ImGuiKey.G,
            VirtualKey.H => ImGuiKey.H,
            VirtualKey.I => ImGuiKey.I,
            VirtualKey.J => ImGuiKey.J,
            VirtualKey.K => ImGuiKey.K,
            VirtualKey.L => ImGuiKey.L,
            VirtualKey.M => ImGuiKey.M,
            VirtualKey.N => ImGuiKey.N,
            VirtualKey.O => ImGuiKey.O,
            VirtualKey.P => ImGuiKey.P,
            VirtualKey.Q => ImGuiKey.Q,
            VirtualKey.R => ImGuiKey.R,
            VirtualKey.S => ImGuiKey.S,
            VirtualKey.T => ImGuiKey.T,
            VirtualKey.U => ImGuiKey.U,
            VirtualKey.V => ImGuiKey.V,
            VirtualKey.W => ImGuiKey.W,
            VirtualKey.X => ImGuiKey.X,
            VirtualKey.Y => ImGuiKey.Y,
            VirtualKey.Z => ImGuiKey.Z,
            VirtualKey.LWIN => ImGuiKey.LeftSuper,
            VirtualKey.RWIN => ImGuiKey.RightSuper,
            VirtualKey.NUMPAD0 => ImGuiKey.Keypad0,
            VirtualKey.NUMPAD1 => ImGuiKey.Keypad1,
            VirtualKey.NUMPAD2 => ImGuiKey.Keypad2,
            VirtualKey.NUMPAD3 => ImGuiKey.Keypad3,
            VirtualKey.NUMPAD4 => ImGuiKey.Keypad4,
            VirtualKey.NUMPAD5 => ImGuiKey.Keypad5,
            VirtualKey.NUMPAD6 => ImGuiKey.Keypad6,
            VirtualKey.NUMPAD7 => ImGuiKey.Keypad7,
            VirtualKey.NUMPAD8 => ImGuiKey.Keypad8,
            VirtualKey.NUMPAD9 => ImGuiKey.Keypad9,
            VirtualKey.MULTIPLY => ImGuiKey.KeypadMultiply,
            VirtualKey.ADD => ImGuiKey.KeypadAdd,
            VirtualKey.SUBTRACT => ImGuiKey.KeypadSubtract,
            VirtualKey.DECIMAL => ImGuiKey.KeypadDecimal,
            VirtualKey.DIVIDE => ImGuiKey.KeypadDivide,
            VirtualKey.F1 => ImGuiKey.F1,
            VirtualKey.F2 => ImGuiKey.F2,
            VirtualKey.F3 => ImGuiKey.F3,
            VirtualKey.F4 => ImGuiKey.F4,
            VirtualKey.F5 => ImGuiKey.F5,
            VirtualKey.F6 => ImGuiKey.F6,
            VirtualKey.F7 => ImGuiKey.F7,
            VirtualKey.F8 => ImGuiKey.F8,
            VirtualKey.F9 => ImGuiKey.F9,
            VirtualKey.F10 => ImGuiKey.F10,
            VirtualKey.F11 => ImGuiKey.F11,
            VirtualKey.F12 => ImGuiKey.F12,
            VirtualKey.NUMLOCK => ImGuiKey.NumLock,
            VirtualKey.SCROLL => ImGuiKey.ScrollLock,
            VirtualKey.OEM_NEC_EQUAL => ImGuiKey.KeypadEqual,
            VirtualKey.LSHIFT => ImGuiKey.LeftShift,
            VirtualKey.RSHIFT => ImGuiKey.RightShift,
            VirtualKey.LCONTROL => ImGuiKey.LeftCtrl,
            VirtualKey.RCONTROL => ImGuiKey.RightCtrl,
            VirtualKey.LMENU => ImGuiKey.LeftAlt,
            VirtualKey.RMENU => ImGuiKey.RightAlt,
            VirtualKey.OEM_1 => ImGuiKey.Semicolon,
            VirtualKey.OEM_PLUS => ImGuiKey.Equal,
            VirtualKey.OEM_COMMA => ImGuiKey.Comma,
            VirtualKey.OEM_MINUS => ImGuiKey.Minus,
            VirtualKey.OEM_PERIOD => ImGuiKey.Period,
            VirtualKey.OEM_2 => ImGuiKey.Slash,
            VirtualKey.OEM_3 => ImGuiKey.GraveAccent,
            VirtualKey.OEM_4 => ImGuiKey.LeftBracket,
            VirtualKey.OEM_5 => ImGuiKey.Backslash,
            VirtualKey.OEM_6 => ImGuiKey.RightBracket,
            VirtualKey.OEM_7 => ImGuiKey.Apostrophe,
            _ => 0,
        };

        return result != 0 || key == VirtualKey.NO_KEY;
    }

    public struct EndUnconditionally(Action endAction, bool success) : ImRaii.IEndObject
    {
        public bool Success { get; } = success;

        private bool Disposed { get; set; } = false;
        private Action EndAction { get; } = endAction;

        public void Dispose()
        {
            if (!Disposed)
            {
                EndAction();
                Disposed = true;
            }
        }
    }

    // Use end-function only on success.
    private struct EndConditionally(Action endAction, bool success) : ImRaii.IEndObject
    {
        public bool Success { get; } = success;

        private bool Disposed { get; set; } = false;
        private Action EndAction { get; } = endAction;

        public void Dispose()
        {
            if (Disposed)
                return;

            if (Success)
                EndAction();

            Disposed = true;
        }
    }

    public static ImRaii.IEndObject TextWrapPos()
    {
        ImGui.PushTextWrapPos();
        return new EndUnconditionally(ImGui.PopTextWrapPos, true);
    }

    public static ImRaii.IEndObject TextWrapPos(bool condition)
    {
        if (!condition)
            return new EndUnconditionally(Nop, false);

        ImGui.PushTextWrapPos();
        return new EndUnconditionally(ImGui.PopTextWrapPos, true);
    }

    public static ImRaii.IEndObject Menu(string label)
    {
        return new EndConditionally(ImGui.EndMenu, ImGui.BeginMenu(label));
    }

    public static void ChannelSelector(string headerText, Dictionary<ChatType, ChatSource> chatCodes)
    {
        using var channelNode = ImRaii.TreeNode(headerText);
        if (!channelNode.Success)
            return;

        foreach (var (header, types) in ChatTypeExt.SortOrder)
        {
            using var headerNode = ImRaii.TreeNode(header);
            if (!headerNode.Success)
                continue;

            foreach (var type in types)
            {
                if (type.IsGm())
                    continue;

                var enabled = chatCodes.ContainsKey(type);
                if (ImGui.Checkbox($"##{type.Name()}", ref enabled))
                {
                    if (enabled)
                        chatCodes[type] = ChatSourceExt.All;
                    else
                        chatCodes.Remove(type);
                }

                ImGui.SameLine();

                if (!type.HasSource())
                {
                    ImGui.TextUnformatted(type.Name());
                    continue;
                }

                using var typeNode = ImRaii.TreeNode($"{type.Name()}");
                if (!typeNode.Success)
                    continue;

                chatCodes.TryGetValue(type, out var sourcesEnum);
                var sources = (uint)sourcesEnum;

                foreach (var source in Enum.GetValues<ChatSource>())
                    if (ImGui.CheckboxFlags(source.Name(), ref sources, (uint)source))
                        chatCodes[type] = (ChatSource)sources;
            }
        }
    }

    public static void ExtraChatSelector(string headerText, ref bool all, HashSet<Guid> extraChatChannels)
    {
        if (Plugin.ExtraChat.ChannelNames.Count <= 0)
            return;

        using var extraTree = ImRaii.TreeNode(headerText);
        if (!extraTree.Success)
            return;

        ImGui.Checkbox(Language.Options_Tabs_ExtraChatAll, ref all);
        ImGui.Separator();

        using var _ = ImRaii.Disabled(all);
        foreach (var (id, name) in Plugin.ExtraChat.ChannelNames)
        {
            var enabled = extraChatChannels.Contains(id);
            if (!ImGui.Checkbox($"{name}##ec-{id}", ref enabled))
                continue;

            if (enabled)
                extraChatChannels.Add(id);
            else
                extraChatChannels.Remove(id);
        }
    }

    public static void WrappedTextWithColor(Vector4 color, string text)
    {
        using (ImRaii.PushColor(ImGuiCol.Text, color))
        {
            ImGui.TextWrapped(text);
        }
    }

    // Used to avoid pops if condition is false for Push.
    private static void Nop() { }
}
