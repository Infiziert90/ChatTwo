using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace ChatTwo.Ui;

public class DebuggerWindow : Window
{
    private readonly Plugin Plugin;
    private readonly ChatLogWindow ChatLogWindow;

    public DebuggerWindow(Plugin plugin) : base($"Debugger###chat2-debugger")
    {
        Plugin = plugin;
        ChatLogWindow = plugin.ChatLogWindow;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(475, 600),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        RespectCloseHotkey = false;
        DisableWindowSounds = true;

        #if DEBUG
        Plugin.Commands.Register("/chat2Debugger").Execute += Toggle;
        #endif
    }

    public void Dispose()
    {
        #if DEBUG
        Plugin.Commands.Register("/chat2Debugger").Execute -= Toggle;
        #endif
    }

    private void Toggle(string _, string __) => Toggle();

    public override void Draw()
    {
        ImGui.TextUnformatted($"Current Cursor Pos: {ChatLogWindow.CursorPos}");
    }
}
