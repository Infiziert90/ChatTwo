using System.Numerics;
using ChatTwo.Code;
using ChatTwo.GameFunctions.Types;
using ChatTwo.Util;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Config;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using ImGuiNET;
using ModifierFlag = ChatTwo.GameFunctions.Types.ModifierFlag;

using ModifierFlag = ChatTwo.GameFunctions.Types.ModifierFlag;

namespace ChatTwo.GameFunctions;

internal enum KeyboardSource {
    Game,
    ImGui
}

internal unsafe class KeybindManager : IDisposable {
    private Plugin Plugin { get; }

    internal bool DirectChat;
    private long LastRefresh;

    private readonly Dictionary<string, Keybind> Keybinds = new();
    private static readonly IReadOnlyDictionary<string, ChannelSwitchInfo> KeybindsToIntercept = new Dictionary<string, ChannelSwitchInfo>
    {
        ["CMD_CHAT"] = new(null),
        ["CMD_COMMAND"] = new(null, text: "/"),
        ["CMD_REPLY"] = new(InputChannel.Tell, rotate: RotateMode.Forward),
        ["CMD_REPLY_REV"] = new(InputChannel.Tell, rotate: RotateMode.Reverse),
        ["CMD_SAY"] = new(InputChannel.Say),
        ["CMD_YELL"] = new(InputChannel.Yell),
        ["CMD_SHOUT"] = new(InputChannel.Shout),
        ["CMD_PARTY"] = new(InputChannel.Party),
        ["CMD_ALLIANCE"] = new(InputChannel.Alliance),
        ["CMD_FREECOM"] = new(InputChannel.FreeCompany),
        ["PVPTEAM_CHAT"] = new(InputChannel.PvpTeam),
        ["CMD_CWLINKSHELL"] = new(InputChannel.CrossLinkshell1, rotate: RotateMode.Forward),
        ["CMD_CWLINKSHELL_REV"] = new(InputChannel.CrossLinkshell1, rotate: RotateMode.Reverse),
        ["CMD_CWLINKSHELL_1"] = new(InputChannel.CrossLinkshell1),
        ["CMD_CWLINKSHELL_2"] = new(InputChannel.CrossLinkshell2),
        ["CMD_CWLINKSHELL_3"] = new(InputChannel.CrossLinkshell3),
        ["CMD_CWLINKSHELL_4"] = new(InputChannel.CrossLinkshell4),
        ["CMD_CWLINKSHELL_5"] = new(InputChannel.CrossLinkshell5),
        ["CMD_CWLINKSHELL_6"] = new(InputChannel.CrossLinkshell6),
        ["CMD_CWLINKSHELL_7"] = new(InputChannel.CrossLinkshell7),
        ["CMD_CWLINKSHELL_8"] = new(InputChannel.CrossLinkshell8),
        ["CMD_LINKSHELL"] = new(InputChannel.Linkshell1, rotate: RotateMode.Forward),
        ["CMD_LINKSHELL_REV"] = new(InputChannel.Linkshell1, rotate: RotateMode.Reverse),
        ["CMD_LINKSHELL_1"] = new(InputChannel.Linkshell1),
        ["CMD_LINKSHELL_2"] = new(InputChannel.Linkshell2),
        ["CMD_LINKSHELL_3"] = new(InputChannel.Linkshell3),
        ["CMD_LINKSHELL_4"] = new(InputChannel.Linkshell4),
        ["CMD_LINKSHELL_5"] = new(InputChannel.Linkshell5),
        ["CMD_LINKSHELL_6"] = new(InputChannel.Linkshell6),
        ["CMD_LINKSHELL_7"] = new(InputChannel.Linkshell7),
        ["CMD_LINKSHELL_8"] = new(InputChannel.Linkshell8),
        ["CMD_BEGINNER"] = new(InputChannel.NoviceNetwork),
        ["CMD_REPLY_ALWAYS"] = new(InputChannel.Tell, true, RotateMode.Forward),
        ["CMD_REPLY_REV_ALWAYS"] = new(InputChannel.Tell, true, RotateMode.Reverse),
        ["CMD_SAY_ALWAYS"] = new(InputChannel.Say, true),
        ["CMD_YELL_ALWAYS"] = new(InputChannel.Yell, true),
        ["CMD_PARTY_ALWAYS"] = new(InputChannel.Party, true),
        ["CMD_ALLIANCE_ALWAYS"] = new(InputChannel.Alliance, true),
        ["CMD_FREECOM_ALWAYS"] = new(InputChannel.FreeCompany, true),
        ["PVPTEAM_CHAT_ALWAYS"] = new(InputChannel.PvpTeam, true),
        ["CMD_CWLINKSHELL_ALWAYS"] = new(InputChannel.CrossLinkshell1, true, RotateMode.Forward),
        ["CMD_CWLINKSHELL_ALWAYS_REV"] = new(InputChannel.CrossLinkshell1, true, RotateMode.Reverse),
        ["CMD_CWLINKSHELL_1_ALWAYS"] = new(InputChannel.CrossLinkshell1, true),
        ["CMD_CWLINKSHELL_2_ALWAYS"] = new(InputChannel.CrossLinkshell2, true),
        ["CMD_CWLINKSHELL_3_ALWAYS"] = new(InputChannel.CrossLinkshell3, true),
        ["CMD_CWLINKSHELL_4_ALWAYS"] = new(InputChannel.CrossLinkshell4, true),
        ["CMD_CWLINKSHELL_5_ALWAYS"] = new(InputChannel.CrossLinkshell5, true),
        ["CMD_CWLINKSHELL_6_ALWAYS"] = new(InputChannel.CrossLinkshell6, true),
        ["CMD_CWLINKSHELL_7_ALWAYS"] = new(InputChannel.CrossLinkshell7, true),
        ["CMD_CWLINKSHELL_8_ALWAYS"] = new(InputChannel.CrossLinkshell8, true),
        ["CMD_LINKSHELL_ALWAYS"] = new(InputChannel.Linkshell1, true, RotateMode.Forward),
        ["CMD_LINKSHELL_REV_ALWAYS"] = new(InputChannel.Linkshell1, true, RotateMode.Reverse),
        ["CMD_LINKSHELL_1_ALWAYS"] = new(InputChannel.Linkshell1, true),
        ["CMD_LINKSHELL_2_ALWAYS"] = new(InputChannel.Linkshell2, true),
        ["CMD_LINKSHELL_3_ALWAYS"] = new(InputChannel.Linkshell3, true),
        ["CMD_LINKSHELL_4_ALWAYS"] = new(InputChannel.Linkshell4, true),
        ["CMD_LINKSHELL_5_ALWAYS"] = new(InputChannel.Linkshell5, true),
        ["CMD_LINKSHELL_6_ALWAYS"] = new(InputChannel.Linkshell6, true),
        ["CMD_LINKSHELL_7_ALWAYS"] = new(InputChannel.Linkshell7, true),
        ["CMD_LINKSHELL_8_ALWAYS"] = new(InputChannel.Linkshell8, true),
        ["CMD_BEGINNER_ALWAYS"] = new(InputChannel.NoviceNetwork, true)
    };

    // List of keys that can be used as a part of keybinds while the chat is
    // focused WITHOUT modifiers. All other keys can only be used if their
    // configured keybind contains modifiers. This allows for using e.g. F11 to
    // change chat channel while typing.
    private static readonly IReadOnlyCollection<VirtualKey> ModifierlessChatKeys = new[]
    {
        // VirtualKey.NO_KEY,
        // VirtualKey.LBUTTON,
        // VirtualKey.RBUTTON,
        // VirtualKey.CANCEL,
        // VirtualKey.MBUTTON,
        // VirtualKey.XBUTTON1,
        // VirtualKey.XBUTTON2,
        // VirtualKey.BACK,
        // VirtualKey.TAB, // handled by ChatLogWindow
        // VirtualKey.CLEAR,
        // VirtualKey.RETURN, // handled by imgui
        // VirtualKey.SHIFT,
        // VirtualKey.CONTROL,
        // VirtualKey.MENU,
        VirtualKey.PAUSE,
        // VirtualKey.CAPITAL,
        // VirtualKey.KANA,
        // VirtualKey.HANGUL,
        // VirtualKey.JUNJA,
        // VirtualKey.FINAL,
        // VirtualKey.HANJA,
        // VirtualKey.KANJI,
        VirtualKey.ESCAPE,
        // VirtualKey.CONVERT,
        // VirtualKey.NONCONVERT,
        // VirtualKey.ACCEPT,
        // VirtualKey.MODECHANGE,
        // VirtualKey.SPACE,
        VirtualKey.PRIOR,
        VirtualKey.NEXT,
        // VirtualKey.END,
        // VirtualKey.HOME,
        // VirtualKey.LEFT,  // handled by imgui
        // VirtualKey.UP,    // handled by ChatLogWindow
        // VirtualKey.RIGHT, // handled by imgui
        // VirtualKey.DOWN,  // handled by ChatLogWindow
        // VirtualKey.SELECT,
        VirtualKey.PRINT,
        VirtualKey.EXECUTE,
        VirtualKey.SNAPSHOT,
        // VirtualKey.INSERT,
        // VirtualKey.DELETE,
        VirtualKey.HELP,
        // VirtualKey.KEY_0,
        // VirtualKey.KEY_1,
        // VirtualKey.KEY_2,
        // VirtualKey.KEY_3,
        // VirtualKey.KEY_4,
        // VirtualKey.KEY_5,
        // VirtualKey.KEY_6,
        // VirtualKey.KEY_7,
        // VirtualKey.KEY_8,
        // VirtualKey.KEY_9,
        // VirtualKey.A,
        // VirtualKey.B,
        // VirtualKey.C,
        // VirtualKey.D,
        // VirtualKey.E,
        // VirtualKey.F,
        // VirtualKey.G,
        // VirtualKey.H,
        // VirtualKey.I,
        // VirtualKey.J,
        // VirtualKey.K,
        // VirtualKey.L,
        // VirtualKey.M,
        // VirtualKey.N,
        // VirtualKey.O,
        // VirtualKey.P,
        // VirtualKey.Q,
        // VirtualKey.R,
        // VirtualKey.S,
        // VirtualKey.T,
        // VirtualKey.U,
        // VirtualKey.V,
        // VirtualKey.W,
        // VirtualKey.X,
        // VirtualKey.Y,
        // VirtualKey.Z,
        // VirtualKey.LWIN,
        // VirtualKey.RWIN,
        VirtualKey.APPS,
        VirtualKey.SLEEP,
        // VirtualKey.NUMPAD0,
        // VirtualKey.NUMPAD1,
        // VirtualKey.NUMPAD2,
        // VirtualKey.NUMPAD3,
        // VirtualKey.NUMPAD4,
        // VirtualKey.NUMPAD5,
        // VirtualKey.NUMPAD6,
        // VirtualKey.NUMPAD7,
        // VirtualKey.NUMPAD8,
        // VirtualKey.NUMPAD9,
        // VirtualKey.MULTIPLY,
        // VirtualKey.ADD,
        // VirtualKey.SEPARATOR,
        // VirtualKey.SUBTRACT,
        // VirtualKey.DECIMAL,
        // VirtualKey.DIVIDE,
        VirtualKey.F1,
        VirtualKey.F2,
        VirtualKey.F3,
        VirtualKey.F4,
        VirtualKey.F5,
        VirtualKey.F6,
        VirtualKey.F7,
        VirtualKey.F8,
        VirtualKey.F9,
        VirtualKey.F10,
        VirtualKey.F11,
        VirtualKey.F12,
        VirtualKey.F13,
        VirtualKey.F14,
        VirtualKey.F15,
        VirtualKey.F16,
        VirtualKey.F17,
        VirtualKey.F18,
        VirtualKey.F19,
        VirtualKey.F20,
        VirtualKey.F21,
        VirtualKey.F22,
        VirtualKey.F23,
        VirtualKey.F24,
        // VirtualKey.NUMLOCK,
        // VirtualKey.SCROLL,
        // VirtualKey.OEM_FJ_JISHO,
        // VirtualKey.OEM_NEC_EQUAL,
        // VirtualKey.OEM_FJ_MASSHOU,
        // VirtualKey.OEM_FJ_TOUROKU,
        // VirtualKey.OEM_FJ_LOYA,
        // VirtualKey.OEM_FJ_ROYA,
        // VirtualKey.LSHIFT,
        // VirtualKey.RSHIFT,
        // VirtualKey.LCONTROL,
        // VirtualKey.RCONTROL,
        // VirtualKey.LMENU,
        // VirtualKey.RMENU,
        VirtualKey.BROWSER_BACK,
        VirtualKey.BROWSER_FORWARD,
        VirtualKey.BROWSER_REFRESH,
        VirtualKey.BROWSER_STOP,
        VirtualKey.BROWSER_SEARCH,
        VirtualKey.BROWSER_FAVORITES,
        VirtualKey.BROWSER_HOME,
        VirtualKey.VOLUME_MUTE,
        VirtualKey.VOLUME_DOWN,
        VirtualKey.VOLUME_UP,
        VirtualKey.MEDIA_NEXT_TRACK,
        VirtualKey.MEDIA_PREV_TRACK,
        VirtualKey.MEDIA_STOP,
        VirtualKey.MEDIA_PLAY_PAUSE,
        VirtualKey.LAUNCH_MAIL,
        VirtualKey.LAUNCH_MEDIA_SELECT,
        VirtualKey.LAUNCH_APP1,
        VirtualKey.LAUNCH_APP2,
        // VirtualKey.OEM_1,
        // VirtualKey.OEM_PLUS,
        // VirtualKey.OEM_COMMA,
        // VirtualKey.OEM_MINUS,
        // VirtualKey.OEM_PERIOD,
        // VirtualKey.OEM_2,
        // VirtualKey.OEM_3,
        // VirtualKey.OEM_4, // [{
        // VirtualKey.OEM_5, // \"
        // VirtualKey.OEM_6, // ]}
        // VirtualKey.OEM_7, // '"
        // VirtualKey.OEM_8,
        // VirtualKey.OEM_AX,
        // VirtualKey.OEM_102,
        // VirtualKey.ICO_HELP,
        // VirtualKey.ICO_00,
        // VirtualKey.PROCESSKEY,
        // VirtualKey.ICO_CLEAR,
        // VirtualKey.PACKET,
        // VirtualKey.OEM_RESET,
        // VirtualKey.OEM_JUMP,
        // VirtualKey.OEM_PA1,
        // VirtualKey.OEM_PA2,
        // VirtualKey.OEM_PA3,
        // VirtualKey.OEM_WSCTRL,
        // VirtualKey.OEM_CUSEL,
        // VirtualKey.OEM_ATTN,
        // VirtualKey.OEM_FINISH,
        // VirtualKey.OEM_COPY,
        // VirtualKey.OEM_AUTO,
        // VirtualKey.OEM_ENLW,
        // VirtualKey.OEM_BACKTAB,
        // VirtualKey.ATTN,
        // VirtualKey.CRSEL,
        // VirtualKey.EXSEL,
        // VirtualKey.EREOF,
        // VirtualKey.PLAY,
        // VirtualKey.ZOOM,
        // VirtualKey.NONAME,
        // VirtualKey.PA1,
        // VirtualKey.OEM_CLEAR,
    };

    internal KeybindManager(Plugin plugin)
    {
        Plugin = plugin;
        Plugin.GameInteropProvider.InitializeFromAttributes(this);

        // Handle keybinds from the game on every tick.
        Plugin.Framework.Update += HandleKeybinds;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= HandleKeybinds;
    }

    private void UpdateKeybinds()
    {
        foreach (var name in KeybindsToIntercept.Keys)
            Keybinds[name] = GetKeybind(name);
    }

    private static ModifierFlag GetModifiers(KeyboardSource source)
    {
        var modifierState = ModifierFlag.None;
        if (source == KeyboardSource.Game)
        {
            if (Plugin.KeyState[VirtualKey.MENU])
                modifierState |= ModifierFlag.Alt;
            if (Plugin.KeyState[VirtualKey.CONTROL])
                modifierState |= ModifierFlag.Ctrl;
            if (Plugin.KeyState[VirtualKey.SHIFT])
                modifierState |= ModifierFlag.Shift;
            return modifierState;
        }

        if (ImGui.GetIO().KeyAlt)
            modifierState |= ModifierFlag.Alt;
        if (ImGui.GetIO().KeyCtrl)
            modifierState |= ModifierFlag.Ctrl;
        if (ImGui.GetIO().KeyShift)
            modifierState |= ModifierFlag.Shift;

        return modifierState;
    }

    private static bool KeyPressed(KeyboardSource source, VirtualKey key)
    {
        if (key == VirtualKey.NO_KEY)
            return false;

        if (!Plugin.KeyState.IsVirtualKeyValid(key))
            return false;

        if (source == KeyboardSource.Game)
            return Plugin.KeyState[key];

        return key.TryToImGui(out var imguiKey) && ImGui.IsKeyPressed(imguiKey);
    }

    private static bool ComboPressed(KeyboardSource source, VirtualKey key, ModifierFlag modifier, ModifierFlag? modifierState = null, bool modifiersOnly = false)
    {
        // When we're in an input, we don't want to process any keybinds that
        // don't have a modifier (or only use shift) and are not explicitly
        // whitelisted.
        if (modifiersOnly && !ModifierlessChatKeys.Contains(key) && modifier is ModifierFlag.None or ModifierFlag.Shift)
            return false;

        modifierState ??= GetModifiers(source);
        var modifierPressed = Plugin.Config.KeybindMode switch
        {
            KeybindMode.Strict => modifier == modifierState.Value,
            KeybindMode.Flexible => modifierState.Value.HasFlag(modifier),
            _ => false
        };

        return KeyPressed(source, key) && modifierPressed;
    }

    private static bool ConfigKeybindPressed(KeyboardSource source, ConfigKeyBind? bind, ModifierFlag? modifierState = null, bool modifiersOnly = false)
    {
        return bind != null && ComboPressed(source, bind.Key, bind.Modifier, modifierState: modifierState, modifiersOnly: modifiersOnly);
    }

    private void HandleKeybinds(IFramework _ ) => HandleKeybinds(KeyboardSource.Game);

    internal void HandleKeybinds(KeyboardSource source, bool ignoreChatOpen = false, bool modifiersOnly = false)
    {
        // Refresh current keybinds every 5s
        if (LastRefresh + 5 * 1000 < Environment.TickCount64)
        {
            UpdateKeybinds();
            DirectChat = Plugin.GameConfig.TryGet(UiControlOption.DirectChat, out bool option) && option;
            LastRefresh = Environment.TickCount64;
        }

        // Vanilla text input has focus
        if (RaptureAtkModule.Instance()->AtkModule.IsTextInputActive())
            return;

        var modifierState = GetModifiers(source);

        // Test for custom keybinds for changing chat tabs before checking
        // vanilla keybinds.
        if (ConfigKeybindPressed(source, Plugin.Config.ChatTabForward))
        {
            Plugin.KeyState[Plugin.Config.ChatTabForward!.Key] = false;
            Plugin.ChatLogWindow.ChangeTabDelta(1);
            return;
        }
        if (ConfigKeybindPressed(source, Plugin.Config.ChatTabBackward))
        {
            Plugin.KeyState[Plugin.Config.ChatTabBackward!.Key] = false;
            Plugin.ChatLogWindow.ChangeTabDelta(-1);
            return;
        }

        // Only process the active combo with the most modifiers.
        var currentBest = (VirtualKey.NO_KEY, "", 0);
        foreach (var (toIntercept, keybind) in Keybinds)
        {
            if (toIntercept is "CMD_CHAT" or "CMD_COMMAND" && (ignoreChatOpen || DirectChat))
                continue;

            void Intercept(VirtualKey vk, ModifierFlag modifier)
            {
                if (!ComboPressed(source, vk, modifier, modifierState: modifierState, modifiersOnly: modifiersOnly))
                    return;

                var bits = BitOperations.PopCount((uint) modifier);
                if (bits < currentBest.Item3)
                    return;

                currentBest = (vk, toIntercept, bits);
            }

            Intercept(keybind.Key1, keybind.Modifier1);
            Intercept(keybind.Key2, keybind.Modifier2);
        }

        if (currentBest.Item1 == VirtualKey.NO_KEY)
            return;

        Plugin.KeyState[currentBest.Item1] = false;
        if (!KeybindsToIntercept.TryGetValue(currentBest.Item2, out var info))
            return;

        try
        {
            TellReason? reason = info.Channel == InputChannel.Tell ? TellReason.Reply : null;
            Plugin.ChatLogWindow.Activated(new ChatActivatedArgs(info) { TellReason = reason, });
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error in chat Activated event");
        }
    }

    private static Keybind GetKeybind(string id)
    {
        var outData = new UIInputData.Keybind();
        var idString = Utf8String.FromString(id);
        UIInputData.Instance()->GetKeybind(idString, &outData);
        idString->Dtor(true);

        var key1 = RemapInvalidVirtualKey((VirtualKey) outData.Key);
        var key2 = RemapInvalidVirtualKey((VirtualKey) outData.AltKey);
        return new Keybind
        {
            Key1 = key1,
            Modifier1 = (ModifierFlag) outData.Modifier,
            Key2 = key2,
            Modifier2 = (ModifierFlag) outData.AltModifier,
        };
    }

    private static VirtualKey RemapInvalidVirtualKey(VirtualKey key)
    {
        return key switch
        {
            VirtualKey.F23 => VirtualKey.OEM_2,   // /?
            (VirtualKey) 140 => VirtualKey.OEM_7, // '"
            _ => key
        };
    }
}