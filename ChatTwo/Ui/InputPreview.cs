using System.Numerics;
using System.Text;
using ChatTwo.Code;
using ChatTwo.Resources;
using ChatTwo.Util;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace ChatTwo.Ui;

public class InputPreview : Window
{
    private ChatLogWindow LogWindow { get; }

    internal float PreviewHeight;

    private int LastLength;
    private Message? PreviewMessage;

    internal InputPreview(ChatLogWindow logWindow) : base("##chat2-inputpreview")
    {
        LogWindow = logWindow;

        Flags = ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoScrollbar;

        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        IsOpen = true;
    }

    public override void PreDraw()
    {
        var pos = LogWindow.LastWindowPos;
        var size = LogWindow.LastWindowSize;

        Size = size with { Y = PreviewHeight };

        var y = Plugin.Config.PreviewPosition switch
        {
            PreviewPosition.Top => pos.Y - PreviewHeight,
            PreviewPosition.Bottom => pos.Y + size.Y,
            _ => throw new ArgumentOutOfRangeException(nameof(Plugin.Config.PreviewPosition), Plugin.Config.PreviewPosition, null)
        };

        Position = pos with { Y = y };
        PositionCondition = ImGuiCond.Always;
    }

    public override bool DrawConditions()
    {
        return Plugin.Config.PreviewPosition is PreviewPosition.Top or PreviewPosition.Bottom && !string.IsNullOrEmpty(LogWindow.Chat);
    }

    public override void Draw()
    {
        CalculatePreview();
        DrawPreview();
    }

    internal void CalculatePreview()
    {
        // We Pre-draw this once to get the actual height :HideThePain:
        PreviewHeight = 0;
        if (!string.IsNullOrEmpty(LogWindow.Chat))
        {
            if (PreviewMessage == null || LastLength != LogWindow.Chat.Length)
            {
                LastLength = LogWindow.Chat.Length;

                var bytes = Encoding.UTF8.GetBytes(LogWindow.Chat.Trim());
                AutoTranslate.ReplaceWithPayload(ref bytes);

                var chunks = ChunkUtil.ToChunks(SeString.Parse(bytes), ChunkSource.Content, ChatType.Say).ToList();
                PreviewMessage = Message.FakeMessage(chunks, new ChatCode((ushort)XivChatType.Say));
                PreviewMessage.DecodeTextParam();
            }

            var pos = ImGui.GetCursorPos();
            ImGui.SetCursorPos(new Vector2(-500, -500));
            var before = ImGui.GetCursorPosY();
            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero))
            {
                ImGui.TextUnformatted(Language.Options_Preview_Header);
                LogWindow.DrawChunks(PreviewMessage.Content);
            }
            var after = ImGui.GetCursorPosY();
            ImGui.SetCursorPos(pos);

            PreviewHeight = after - before;
            PreviewHeight += Plugin.Config.PreviewPosition is not PreviewPosition.Inside
                ? ImGui.GetStyle().WindowPadding.Y * 2
                : 0;
        }
        else
        {
            LastLength = 0;
            PreviewMessage = null;
        }
    }

    internal void DrawPreview()
    {
        if (PreviewMessage == null)
            return;

        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero))
        {
            ImGui.TextUnformatted(Language.Options_Preview_Header);

            var handler = LogWindow.HandlerLender.Borrow();
            LogWindow.DrawChunks(PreviewMessage.Content, true, handler);
            handler.Draw();
        }
    }
}
