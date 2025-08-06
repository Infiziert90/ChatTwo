using System.Numerics;
using ChatTwo.Util;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;

namespace ChatTwo.Ui;

public class CommandHelpWindow : Window {
    private ChatLogWindow LogWindow { get; }
    private TextCommand? Command { get; set; }

    internal CommandHelpWindow(ChatLogWindow logWindow) : base($"command help##chat2-commandhelp")
    {
        LogWindow = logWindow;

        Flags = ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.AlwaysAutoResize;

        RespectCloseHotkey = false;
        DisableWindowSounds = true;
    }

    // Sets IsOpen to true if it should be drawn
    public void UpdateContent(TextCommand command)
    {
        Command = command;

        var width = 350;
        var scaledWidth = width * ImGuiHelpers.GlobalScale;
        var pos = LogWindow.LastWindowPos;
        switch (Plugin.Config.CommandHelpSide) {
            case CommandHelpSide.Right:
                pos.X += LogWindow.LastWindowSize.X;
                break;
            case CommandHelpSide.Left:
                pos.X -= scaledWidth;
                break;
            case CommandHelpSide.None:
            default:
                IsOpen = false;
                return;
        }

        Position = pos;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(width, 0),
            MaximumSize = LogWindow.LastWindowSize with { X = width }
        };

        IsOpen = true;
    }

    public override void Draw()
    {
        if (Command == null)
            return;

        LogWindow.DrawChunks(ChunkUtil.ToChunks(Command.Value.Description.ToDalamudString(), ChunkSource.None, null).ToList());
    }
}
