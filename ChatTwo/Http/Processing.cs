using System.Globalization;
using ChatTwo.Code;
using ChatTwo.Http.MessageProtocol;
using ChatTwo.Util;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Ganss.Xss;

namespace ChatTwo.Http;

public class Processing
{
    private readonly Plugin Plugin;
    private readonly HtmlSanitizer Sanitizer = new();

    public Processing(Plugin plugin)
    {
        Plugin = plugin;
    }

    public string ReadChannelName()
    {
        var messages = new List<string>();
        foreach (var chunk in Plugin.ChatLogWindow.ReadChannelName(Plugin.ChatLogWindow.CurrentTab))
            messages.Add(ProcessChunk(chunk, noColor: true));

        return string.Join("", messages);
    }

    internal async Task<List<MessageResponse>> ReadMessageList()
    {
        var tabMessages = await Plugin.ChatLogWindow.CurrentTab!.Messages.GetCopy();
        return tabMessages.Select(ReadMessageContent).ToList();
    }

    internal MessageResponse ReadMessageContent(Message message)
    {
        var response = new MessageResponse
        {
            Timestamp = message.Date.ToLocalTime().ToString("t", !Plugin.Config.Use24HourClock ? null : CultureInfo.CreateSpecificCulture("es-ES"))
        };

        var content = "";
        if (message.Sender.Count > 0)
            content = message.Sender.Aggregate(content, (current, chunk) => current + ProcessChunk(chunk));

        content = message.Content.Aggregate(content, (current, chunk) => current + ProcessChunk(chunk));
        response.Message = content;

        return response;
    }

    private string ProcessChunk(Chunk chunk, bool noColor = false)
    {
        if (chunk is IconChunk { } icon)
        {
            return IconUtil.GfdFileView.TryGetEntry((uint) icon.Icon, out _)
                ? $"<span class=\"gfd-icon gfd-icon-hq-{(uint)icon.Icon}\" style=\"zoom:calc(16 * 4 / 3 / 40 * 1.4)\"></span>"
                : "";
        }

        if (chunk is TextChunk { } text)
        {
            if (chunk.Link is EmotePayload emotePayload && Plugin.Config.ShowEmotes)
            {
                var image = EmoteCache.GetEmote(emotePayload.Code);
                if (image is { Failed: false })
                    return $"<span style\"height: 1em\"><img src=\"/emote/{emotePayload.Code}\"></span>";
            }

            var colour = text.Foreground;
            if (colour == null && text.FallbackColour != null)
            {
                var type = text.FallbackColour.Value;
                colour = Plugin.Config.ChatColours.TryGetValue(type, out var col) ? col : type.DefaultColor();
            }

            var color = ColourUtil.RgbaToComponents(colour ?? 0);

            var userContent = text.Content ?? "";
            if (Plugin.ChatLogWindow.ScreenshotMode)
            {
                if (chunk.Link is PlayerPayload playerPayload)
                    userContent = Plugin.ChatLogWindow.HidePlayerInString(userContent, playerPayload.PlayerName, playerPayload.World.RowId);
                else if (Plugin.ClientState.LocalPlayer is { } player)
                    userContent = Plugin.ChatLogWindow.HidePlayerInString(userContent, player.Name.TextValue, player.HomeWorld.Id);
            }

            userContent = Sanitizer.Sanitize(userContent);
            return noColor
                ? userContent
                : $"<span style=\"color:rgba({color.r}, {color.g}, {color.b}, {color.a})\">{userContent}</span>";
        }

        return string.Empty;
    }
}