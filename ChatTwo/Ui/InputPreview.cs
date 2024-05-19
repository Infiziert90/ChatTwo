using System.Numerics;
using System.Reflection;
using System.Text;
using ChatTwo.Code;
using ChatTwo.Resources;
using ChatTwo.Util;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
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

    private bool NextChunkIsAutoTranslate;
    private int CursorPosition;
    public int SelectedCursorPos = -1;

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
                DrawChunksPreview(PreviewMessage.Content);
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
            DrawChunksPreview(PreviewMessage.Content, handler, unique: 10000);
            handler.Draw();
        }
    }

    private void DrawChunksPreview(IReadOnlyList<Chunk> chunks, PayloadHandler? handler = null, float lineWidth = 0f, int unique = 0)
    {
        CursorPosition = 0;

        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        for (var i = 0; i < chunks.Count; i++)
        {
            if (chunks[i] is TextChunk text && string.IsNullOrEmpty(text.Content))
                continue;

            DrawChunkPreview(chunks[i], handler, lineWidth, unique);

            if (i < chunks.Count - 1)
            {
                ImGui.SameLine();
            }
            else if (chunks[i].Link is EmotePayload && Plugin.Config.ShowEmotes)
            {
                // Emote payloads seem to not automatically put newlines, which
                // is an issue when modern mode is disabled.
                ImGui.SameLine();
                // Use default ImGui behavior for newlines.
                ImGui.TextUnformatted("");
            }
        }
    }

    private void DrawChunkPreview(Chunk chunk, PayloadHandler? handler = null, float lineWidth = 0f, int unique = 0)
    {
        if (chunk is IconChunk icon && LogWindow.FontIcon != null)
        {
            LogWindow.DrawIcon(chunk, icon, handler);
            if (icon.Icon != BitmapFontIcon.AutoTranslateBegin)
                return;

            try
            {
                NextChunkIsAutoTranslate = true;
                var key = (uint)typeof(AutoTranslatePayload).GetField("key", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(chunk.Link)!;
                var group = (uint)typeof(AutoTranslatePayload).GetField("group", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(chunk.Link)!;
                CursorPosition += $"<at:{key},{group}>".Length;
            }
            catch
            {
                // Ignore
            }

            return;
        }

        if (chunk is not TextChunk text)
            return;

        if (chunk.Link is EmotePayload emotePayload && Plugin.Config.ShowEmotes)
        {
            var emoteSize = ImGui.CalcTextSize("W");
            emoteSize = emoteSize with { Y = emoteSize.X } * 1.5f;

            // TextWrap doesn't work for emotes, so we have to wrap them manually
            if (ImGui.GetContentRegionAvail().X < emoteSize.X)
                ImGui.NewLine();

            // We only draw a dummy if it is still loading, in the case it failed we draw the actual name
            var image = EmoteCache.GetEmote(emotePayload.Code);
            if (image is { Failed: false })
            {
                if (image.IsLoaded)
                    image.Draw(emoteSize);
                else
                    ImGui.Dummy(emoteSize);

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(emotePayload.Code);

                CursorPosition += emotePayload.Code.Length;
                return;
            }
        }


        if (text.Link != null || NextChunkIsAutoTranslate)
        {
            NextChunkIsAutoTranslate = false;

            if (text.Link is ItemPayload)
                CursorPosition += "<item>".Length;
            else if (text.Link is MapLinkPayload)
                CursorPosition += "<flag>".Length;
            else if (text.Link is EmotePayload emote)
                CursorPosition += emote.Code.Length;

            ImGuiUtil.WrapText(text.Content, chunk, handler, LogWindow.DefaultText, lineWidth);
            return;
        }

        foreach (var letter in text.Content)
        {
            var letterSize = ImGui.CalcTextSize(letter.ToString());
            if (ImGui.GetContentRegionAvail().X < letterSize.X)
                ImGui.NewLine();

            CursorPosition++;
            if (ImGui.Selectable($"{letter}##{CursorPosition + unique}", false, ImGuiSelectableFlags.None, letterSize))
            {
                SelectedCursorPos = CursorPosition;
                LogWindow.KeepFocusedThroughPreview = true;
            }
            ImGui.SameLine();
        }
        ImGui.NewLine();
    }
}
