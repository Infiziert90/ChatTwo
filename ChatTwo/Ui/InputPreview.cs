using System.Numerics;
using System.Text;
using ChatTwo.Code;
using ChatTwo.Util;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace ChatTwo.Ui;

public class InputPreview : Window
{
    private ChatLogWindow LogWindow { get; }

    private float Height;
    private float AppliedHeight;

    internal InputPreview(ChatLogWindow logWindow) : base("##chat2-inputpreview")
    {
        LogWindow = logWindow;

        Flags = ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoInputs;

        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        IsOpen = true;
    }

    public override void PreDraw()
    {
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        // Sizes don't use much precision
        if (AppliedHeight == Height)
            return;

        AppliedHeight = Height;

        var width = LogWindow.LastWindowSize.X;
        var pos = LogWindow.LastWindowPos;

        Size = new Vector2(width, Height);

        Position = pos with { Y = pos.Y - Height };
        PositionCondition = ImGuiCond.Always;
    }

    public override bool DrawConditions()
    {
        return !string.IsNullOrEmpty(LogWindow.Chat);
    }

    public override void Draw()
    {
        var bytes = Encoding.UTF8.GetBytes(LogWindow.Chat.Trim());
        AutoTranslate.ReplaceWithPayload(Plugin.DataManager, ref bytes);

        var chunks = ChunkUtil.ToChunks(SeString.Parse(bytes), ChunkSource.Content, ChatType.Say).ToList();
        var encodedChunks = Message.FakeMessage(chunks, new ChatCode((ushort) XivChatType.Say));

        LogWindow.DrawChunks(encodedChunks.Content);

        // WindowPadding applies to top and bottom, so we take it 2 times
        Height = ImGui.GetCursorPosY() + ImGui.GetStyle().WindowPadding.Y * 2;
    }
}
