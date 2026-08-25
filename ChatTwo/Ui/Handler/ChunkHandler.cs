using System.Numerics;
using ChatTwo.Code;
using ChatTwo.Util;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace ChatTwo.Ui.Handler;

public class ChunkHandler
{
    private readonly Plugin Plugin;

    public ChunkHandler(Plugin plugin)
    {
        Plugin = plugin;
    }

    public void DrawChunks(IReadOnlyList<Chunk> chunks, bool wrap = true, PayloadHandler? handler = null, float lineWidth = 0f)
    {
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero);

        for (var i = 0; i < chunks.Count; i++)
        {
            if (chunks[i] is TextChunk text && string.IsNullOrEmpty(text.Content))
                continue;

            DrawChunk(chunks[i], wrap, handler, lineWidth);

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

    public void DrawIcon(Chunk chunk, IconChunk icon, PayloadHandler? handler)
    {
        if (!IconUtil.GfdFileView.TryGetEntry((uint) icon.Icon, out var entry))
            return;

        var iconTexture = Plugin.TextureProvider.GetFromGame("common/font/fonticon_ps5.tex").GetWrapOrDefault();
        if (iconTexture == null)
            return;

        var texSize = new Vector2(iconTexture.Width, iconTexture.Height);

        var sizeRatio = FontManager.GetFontSize() / entry.Height;
        var size = new Vector2(entry.Width, entry.Height) * sizeRatio * ImGuiHelpers.GlobalScale;

        var uv0 = new Vector2(entry.Left, entry.Top + 170) * 2 / texSize;
        var uv1 = new Vector2(entry.Left + entry.Width, entry.Top + entry.Height + 170) * 2 / texSize;

        ImGui.Image(iconTexture.Handle, size, uv0, uv1);
        ImGuiUtil.PostPayload(chunk, handler);
    }

    public void DrawChunk(Chunk chunk, bool wrap = true, PayloadHandler? handler = null, float lineWidth = 0f)
    {
        if (chunk is IconChunk icon)
        {
            DrawIcon(chunk, icon, handler);
            return;
        }

        if (chunk is not TextChunk text)
            return;

        if (Plugin.Config.ShowEmotes && chunk.Link is EmotePayload or TwemojiPayload)
        {
            var emoteSize = ImGui.CalcTextSize("W");
            emoteSize = emoteSize with { Y = emoteSize.X } * 1.5f;

            // TextWrap doesn't work for emotes, so we have to wrap them manually
            if (ImGui.GetContentRegionAvail().X < emoteSize.X)
                ImGui.NewLine();

            ImGuiDrawable? image = null;
            var emoteCode = string.Empty;
            switch (chunk.Link)
            {
                case EmotePayload emotePayload:
                    image = EmoteCache.GetEmote(emotePayload.Code);
                    emoteCode = emotePayload.Code;
                    break;
                case TwemojiPayload twemojiPayload:
                    image = TwemojiProvider.GetTwemoji(twemojiPayload.Unicode);
                    emoteCode = twemojiPayload.Shortcode;
                    break;
            }

            // We only draw a dummy if it is still loading, in the case it failed we draw the actual name
            if (image is { Failed: false })
            {
                if (image.IsLoaded)
                    image.Draw(emoteSize);
                else
                    ImGui.Dummy(emoteSize);

                if (ImGui.IsItemHovered())
                    ImGuiUtil.Tooltip(emoteCode);

                return;
            }
        }

        var color = text.Color;
        if (color == null && text.FallbackColor != null)
        {
            var type = text.FallbackColor.Value;
            color = Plugin.Config.ChatColours.TryGetValue(type, out var col)
                ? ColourUtil.RgbaToVector4(col)
                : ColourUtil.RgbaToVector4(type.DefaultColor());
        }

        using var pushedColor = ImRaii.PushColor(ImGuiCol.Text, color);

        var disposableFont = Plugin.Config.FontsEnabled && Plugin.FontManager.ItalicFont != null
            ? Plugin.FontManager.ItalicFont
            : Plugin.FontManager.AxisItalic;
        if (text.Italic)
            disposableFont.Push();

        // Check for contains here as sometimes there are multiple
        // TextChunks with the same PlayerPayload but only one has the name.
        // E.g. party chat with cross world players adds extra chunks.
        //
        // Note: This has been null before, I'm guessing due to some issues with
        // other plugins. New TextChunks will now enforce empty string in ctor,
        // but old ones may still be null.
        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        var content = text.Content ?? "";
        if (PlayerUtil.ScreenshotMode)
        {
            if (chunk.Link is PlayerPayload playerPayload)
                content = PlayerUtil.HidePlayerInString(content, playerPayload.PlayerName, playerPayload.World.RowId);
            else if (Plugin.PlayerState.IsLoaded)
                content = PlayerUtil.HidePlayerInString(content, Plugin.PlayerState.CharacterName, Plugin.PlayerState.HomeWorld.RowId);
        }

        if (wrap)
        {
            ImGuiUtil.WrapText(content, chunk, handler, Plugin.DefaultText, lineWidth);
        }
        else
        {
            ImGui.TextUnformatted(content);
            ImGuiUtil.PostPayload(chunk, handler);
        }

        if (text.Italic)
            disposableFont.Pop();
    }
}