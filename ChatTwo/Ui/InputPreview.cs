using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using ChatTwo.Code;
using ChatTwo.Resources;
using ChatTwo.Util;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ImGuiNET;

namespace ChatTwo.Ui;

public partial class InputPreview : Window
{
    private ChatLogWindow LogWindow { get; }

    private bool Drawing;
    private bool HasEvaluation;
    internal float PreviewHeight;

    private int LastLength;
    private Message? PreviewMessage;

    private int CursorPosition;
    private bool NextChunkIsAutoTranslate;

    internal int SelectedCursorPos = -1;

    internal InputPreview(ChatLogWindow logWindow) : base("##chat2-inputpreview")
    {
        LogWindow = logWindow;

        Flags = ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoScrollbar;

        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        IsOpen = true;

        Plugin.Framework.Update += UpdateConditionCheck;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= UpdateConditionCheck;
    }

    private bool ValidDraw => !string.IsNullOrEmpty(LogWindow.Chat) && LogWindow.Chat.Length >= Plugin.Config.PreviewMinimum;
    private void UpdateConditionCheck(IFramework framework)
    {
        Drawing = ValidDraw;
        if (!Drawing)
        {
            LastLength = 0;
            PreviewHeight = 0;
            PreviewMessage = null;
            HasEvaluation = false;

            return;
        }

        if (PreviewMessage == null || LastLength != LogWindow.Chat.Length)
        {
            LastLength = LogWindow.Chat.Length;

            var bytes = Encoding.UTF8.GetBytes(LogWindow.Chat.Trim());
            AutoTranslate.ReplaceWithPayload(ref bytes);

            var chunks = ChunkUtil.ToChunks(SeString.Parse(bytes), ChunkSource.Content, ChatType.Say).ToList();
            PreviewMessage = Message.FakeMessage(chunks, new ChatCode((ushort)XivChatType.Say));
            PreviewMessage.DecodeTextParam();
        }
        HasEvaluation = !Plugin.Config.OnlyPreviewIf || PreviewMessage.Content.Count > 1;
    }

    internal bool IsDrawable => ValidDraw && HasEvaluation;

    private static bool IsWindowMode => Plugin.Config.PreviewPosition is PreviewPosition.Top or PreviewPosition.Bottom;
    public override bool DrawConditions()
    {
        return IsWindowMode && IsDrawable;
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

    public override void Draw()
    {
        CalculatePreview();
        DrawPreview();
    }

    internal void CalculatePreview()
    {
        // We Pre-draw this once to get the actual height :HideThePain:
        PreviewHeight = 0;

        var pos = ImGui.GetCursorPos();
        ImGui.SetCursorPos(new Vector2(-500, -500));
        var before = ImGui.GetCursorPosY();
        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero))
        {
            ImGui.TextUnformatted(Language.Options_Preview_Header);
            DrawChunksPreview(PreviewMessage!.Content);
        }
        var after = ImGui.GetCursorPosY();
        ImGui.SetCursorPos(pos);

        PreviewHeight = after - before;
        PreviewHeight += IsWindowMode ? ImGui.GetStyle().WindowPadding.Y * 2 : 0;
    }

    internal void DrawPreview()
    {
        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero))
        {
            ImGui.TextUnformatted(Language.Options_Preview_Header);

            var handler = LogWindow.HandlerLender.Borrow();
            DrawChunksPreview(PreviewMessage!.Content, handler, unique: 10000);
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

            NextChunkIsAutoTranslate = true;
            var payload = (AutoTranslatePayload) chunk.Link!;
            CursorPosition += $"<at:{payload.Group},{payload.Key}>".Length;

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

            // We only draw a dummy if it is still loading, in case it failed, we draw the actual name
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

        if (NextChunkIsAutoTranslate)
        {
            NextChunkIsAutoTranslate = false;
            ImGuiUtil.WrapText(text.Content, chunk, handler, LogWindow.DefaultText, lineWidth);
            return;
        }

        if (text.Link != null)
        {
            if (text.Link is ItemPayload)
                CursorPosition += "<item>".Length;
            else if (text.Link is MapLinkPayload)
                CursorPosition += "<flag>".Length;
            else if (text.Link is EmotePayload emote)
                CursorPosition += emote.Code.Length;
            else if (text.Link is UriPayload)
                CursorPosition += text.Content.Length;

            ImGuiUtil.WrapText(text.Content, chunk, handler, LogWindow.DefaultText, lineWidth);
            return;
        }

        foreach (var word in WhitespaceRegex().Split(text.Content).Where(s => s != string.Empty))
        {
            var wordSize = ImGui.CalcTextSize(word);
            if (ImGui.GetContentRegionAvail().X < wordSize.X)
                ImGui.NewLine();

            foreach (var letter in word)
            {
                var letterSize = ImGui.CalcTextSize(letter.ToString());

                CursorPosition++;
                if (ImGui.Selectable($"{letter}##{CursorPosition + unique}", false, ImGuiSelectableFlags.None, letterSize))
                {
                    SelectedCursorPos = CursorPosition;
                    LogWindow.FocusedPreview = true;
                }
                ImGui.SameLine();
            }
        }
        ImGui.NewLine();
    }

    [GeneratedRegex(@"(\s)")]
    private static partial Regex WhitespaceRegex();
}
