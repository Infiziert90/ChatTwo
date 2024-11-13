using System.Globalization;
using ChatTwo.Code;
using ChatTwo.Http.MessageProtocol;
using ChatTwo.Util;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace ChatTwo.Http;

public class Processing
{
    private readonly Plugin Plugin;

    public Processing(Plugin plugin)
    {
        Plugin = plugin;
    }

    internal (MessageTemplate[] ChannelName, bool Locked) ReadChannelName(Chunk[] channelName)
    {
        var locked = Plugin.ChatLogWindow.CurrentTab is not { Channel: null };
        return (channelName.Select(ProcessChunk).ToArray(), locked);
    }

    internal async Task<MessageResponse[]> ReadMessageList()
    {
        var tabMessages = await Plugin.ChatLogWindow.CurrentTab!.Messages.GetCopy();
        return tabMessages.TakeLast(Plugin.Config.WebinterfaceMaxLinesToSend).Select(ReadMessageContent).ToArray();
    }

    internal MessageResponse ReadMessageContent(Message message)
    {
        var response = new MessageResponse
        {
            Timestamp = message.Date.ToLocalTime().ToString("t", !Plugin.Config.Use24HourClock ? null : CultureInfo.CreateSpecificCulture("es-ES"))
        };

        var sender = message.Sender.Select(ProcessChunk);
        var content = message.Content.Select(ProcessChunk);
        response.Templates = sender.Concat(content).ToArray();

        return response;
    }

    internal async Task PrepareNewClient(SSEConnection sse)
    {
        var messages = await WebserverUtil.FrameworkWrapper(ReadMessageList);
        var channels = await Plugin.Framework.RunOnTick(Plugin.ChatLogWindow.GetAvailableChannels);
        var channel = await Plugin.Framework.RunOnTick(() => ReadChannelName(Plugin.ChatLogWindow.PreviousChannel));

        // Using the bulk message event to clear everything on the client side that may still exist
        sse.OutboundQueue.Enqueue(new BulkMessagesEvent(new Messages(messages)));
        sse.OutboundQueue.Enqueue(new SwitchChannelEvent(new SwitchChannel(channel)));
        sse.OutboundQueue.Enqueue(new ChannelListEvent(new ChannelList(channels.ToDictionary(pair => pair.Key, pair => (uint)pair.Value))));
    }

    private MessageTemplate ProcessChunk(Chunk chunk)
    {
        if (chunk is IconChunk { } icon)
        {
            var iconId = (uint)icon.Icon;
            return IconUtil.GfdFileView.TryGetEntry(iconId, out _) ? new MessageTemplate {Payload = "icon", Id = iconId}: MessageTemplate.Empty;
        }

        if (chunk is TextChunk { } text)
        {
            if (chunk.Link is EmotePayload emotePayload && Plugin.Config.ShowEmotes)
            {
                var image = EmoteCache.GetEmote(emotePayload.Code);

                if (image is { Failed: false })
                    return new MessageTemplate { Payload = "emote", Color = 0, Content = emotePayload.Code };
            }

            var color = text.Foreground;
            if (color == null && text.FallbackColour != null)
            {
                var type = text.FallbackColour.Value;
                color = Plugin.Config.ChatColours.TryGetValue(type, out var col) ? col : type.DefaultColor();
            }

            color ??= 0;

            var userContent = text.Content ?? "";
            if (Plugin.ChatLogWindow.ScreenshotMode)
            {
                if (chunk.Link is PlayerPayload playerPayload)
                    userContent = Plugin.ChatLogWindow.HidePlayerInString(userContent, playerPayload.PlayerName, playerPayload.World.RowId);
                else if (Plugin.ClientState.LocalPlayer is { } player)
                    userContent = Plugin.ChatLogWindow.HidePlayerInString(userContent, player.Name.TextValue, player.HomeWorld.RowId);
            }

            var isNotUrl = text.Link is not UriPayload;
            return new MessageTemplate { Payload = isNotUrl ? "text" : "url", Color = color.Value, Content = userContent };
        }

        return MessageTemplate.Empty;
    }
}
