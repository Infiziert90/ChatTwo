using System.Numerics;
using ChatTwo.Util;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace ChatTwo.Ui;

public class CommandHelpWindow : Window {
    private ChatLogWindow LogWindow { get; }
    private TextCommand? Command { get; set; }

    internal CommandHelpWindow(ChatLogWindow logWindow) : base($"command help##chat2-commandhelp")
    {
        LogWindow = logWindow;

        Flags = ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.AlwaysAutoResize;
    }

    public void UpdateContent(TextCommand command)
    {
        Command = command;

        var width = 350;
        var scaledWidth = width * ImGuiHelpers.GlobalScale;
        var pos = LogWindow.LastWindowPos;
        switch (LogWindow.Plugin.Config.CommandHelpSide) {
            case CommandHelpSide.Right:
                pos.X += LogWindow.LastWindowSize.X;
                break;
            case CommandHelpSide.Left:
                pos.X -= scaledWidth;
                break;
            case CommandHelpSide.None:
            default:
                return;
        }

        Position = pos;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(width, 0),
            MaximumSize = LogWindow.LastWindowSize with { X = width }
        };
    }

    public override void Draw()
    {
        if (Command == null)
            return;

        LogWindow.DrawChunks(ChunkUtil.ToChunks(Command.Description.ToDalamudString(), ChunkSource.None, null).ToList());
    }
}
