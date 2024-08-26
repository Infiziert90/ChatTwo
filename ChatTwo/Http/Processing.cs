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

    public string ReadChannelName(Chunk[] channelName)
    {
        return string.Join("", channelName.Select(chunk => ProcessChunk(chunk, noColor: true)));
    }

    internal async Task<MessageResponse[]> ReadMessageList()
    {
        var tabMessages = await Plugin.ChatLogWindow.CurrentTab!.Messages.GetCopy();
        return tabMessages.Select(ReadMessageContent).ToArray();
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
                ? $"<span class=\"gfd-icon gfd-icon-hq-{(uint)icon.Icon}\"></span>"
                : "";
        }

        if (chunk is TextChunk { } text)
        {
            if (chunk.Link is EmotePayload emotePayload && Plugin.Config.ShowEmotes)
            {
                var image = EmoteCache.GetEmote(emotePayload.Code);

                // The emote name should be safe, it is checked against a list from BTTV.
                // Still sanitizing it for the extra safety.
                if (image is { Failed: false })
                    return $"<span class=\"emote-icon\"><img src=\"/emote/{Sanitizer.Sanitize(emotePayload.Code)}\"></span>";
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
