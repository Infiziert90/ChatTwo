using System.Numerics;
using ChatTwo.Util;
using Dalamud.Interface.Utility;
using Dalamud.Utility;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace ChatTwo.Ui;

internal class CommandHelp {
    private ChatLog Log { get; }
    private TextCommand Command { get; }

    internal CommandHelp(ChatLog log, TextCommand command) {
        this.Log = log;
        this.Command = command;
    }

    internal void Draw() {
        var width = 350 * ImGuiHelpers.GlobalScale;

        var pos = this.Log.LastWindowPos;
        switch (this.Log.Ui.Plugin.Config.CommandHelpSide) {
            case CommandHelpSide.Right:
                pos.X += this.Log.LastWindowSize.X;
                break;
            case CommandHelpSide.Left:
                pos.X -= width;
                break;
            case CommandHelpSide.None:
            default:
                return;
        }

        ImGui.SetNextWindowPos(pos);

        ImGui.SetNextWindowSizeConstraints(
            new Vector2(width, 0),
            new Vector2(width, this.Log.LastWindowSize.Y)
        );

        const ImGuiWindowFlags flags = ImGuiWindowFlags.NoSavedSettings
                                       | ImGuiWindowFlags.NoTitleBar
                                       | ImGuiWindowFlags.NoMove
                                       | ImGuiWindowFlags.NoResize
                                       | ImGuiWindowFlags.NoFocusOnAppearing
                                       | ImGuiWindowFlags.AlwaysAutoResize;
        if (!ImGui.Begin($"command help {this.Command.RowId}", flags)) {
            ImGui.End();
            return;
        }

        this.Log.DrawChunks(ChunkUtil.ToChunks(this.Command.Description.ToDalamudString(), ChunkSource.None, null).ToList());

        ImGui.End();
    }
}
