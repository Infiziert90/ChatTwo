using System.Globalization;
using ChatTwo.Code;
using ChatTwo.Http.MessageProtocol;
using ChatTwo.Util;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace ChatTwo.Http;

public class Processing
{
    private readonly HostContext HostContext;

    public Processing(HostContext hostContext)
    {
        HostContext = hostContext;
    }

    public (MessageTemplate[] Name, bool Locked) ReadChannelName(Chunk[] channelName)
    {
        var locked = HostContext.Core.Plugin.CurrentTab is not { Channel: null };
        return (channelName.Select(ProcessChunk).ToArray(), locked);
    }

    public async Task<MessageResponse[]> ReadMessageList()
    {
        var tabMessages = await HostContext.Core.Plugin.CurrentTab.Messages.GetCopy();
        return tabMessages.TakeLast(Plugin.Config.WebinterfaceMaxLinesToSend).Select(ReadMessageContent).ToArray();
    }

    public MessageResponse ReadMessageContent(Message message)
    {
        var response = new MessageResponse
        {
            Id = message.Id,
            Timestamp = message.Date.ToLocalTime().ToString("t", !Plugin.Config.Use24HourClock ? null : CultureInfo.CreateSpecificCulture("es-ES"))
        };

        var sender = message.Sender.Select(ProcessChunk);
        var content = message.Content.Select(ProcessChunk);
        response.Templates = sender.Concat(content).ToArray();

        return response;
    }

    private MessageTemplate ProcessChunk(Chunk chunk)
    {
        if (chunk is IconChunk { } icon)
        {
            var iconId = (uint)icon.Icon;
            return IconUtil.GfdFileView.TryGetEntry(iconId, out _) ? new MessageTemplate {PayloadType = WebPayloadType.Icon, IconId = iconId}: MessageTemplate.Empty;
        }

        if (chunk is TextChunk { } text)
        {
            if (chunk.Link is EmotePayload emotePayload && Plugin.Config.ShowEmotes)
            {
                var image = EmoteCache.GetEmote(emotePayload.Code);

                if (image is { Failed: false })
                    return new MessageTemplate { PayloadType = WebPayloadType.CustomEmote, Color = 0, Content = emotePayload.Code };
            }

            if (chunk.Link is TwemojiPayload twemojiPayload && Plugin.Config.ShowEmotes)
            {
                var image = TwemojiProvider.GetTwemoji(twemojiPayload.Unicode);
                if (image is { Failed: false })
                    // We send the shortcode to the web interface, which then loads the emoji from a route on the webserver
                    return new MessageTemplate { PayloadType = WebPayloadType.CustomTwemoji, Color = 0, Content = twemojiPayload.Shortcode};
            }

            var color = text.Foreground;
            if (color == null && text.FallbackColor != null)
            {
                var type = text.FallbackColor.Value;
                color = Plugin.Config.ChatColours.TryGetValue(type, out var col) ? col : type.DefaultColor();
            }

            color ??= 0;

            var userContent = text.Content;
            if (PlayerUtil.ScreenshotMode)
            {
                if (chunk.Link is PlayerPayload playerPayload)
                    userContent = PlayerUtil.HidePlayerInString(userContent, playerPayload.PlayerName, playerPayload.World.RowId);
                else if (Plugin.PlayerState.IsLoaded)
                    userContent = PlayerUtil.HidePlayerInString(userContent, Plugin.PlayerState.CharacterName, Plugin.PlayerState.HomeWorld.RowId);
            }

            var isNotUrl = text.Link is not UriPayload;
            return new MessageTemplate { PayloadType = isNotUrl ? WebPayloadType.RawText : WebPayloadType.CustomUri, Color = color.Value, Content = userContent };
        }

        return MessageTemplate.Empty;
    }

    public async Task<Messages> GetAllMessages()
    {
        var messages = await WebserverUtil.FrameworkWrapper(ReadMessageList);
        return new Messages(messages);
    }

    public SwitchChannel GetCurrentChannel()
    {
        var channel = ReadChannelName(HostContext.Core.Plugin.ChatLog.PreviousChannel);
        return new SwitchChannel(channel);
    }

    public ChannelList GetValidChannels()
    {
        var channels = HostContext.Core.Plugin.ChatLog.GetValidChannels();
        return new ChannelList(channels.ToDictionary(pair => pair.Key, pair => (uint)pair.Value));
    }

    public ChatTab GetCurrentTab()
    {
        var currentTab = HostContext.Core.Plugin.CurrentTab;
        return new ChatTab(currentTab.Name, HostContext.Core.Plugin.LastTab, currentTab.Unread);
    }

    public ChatTabList GetAllTabs()
    {
        var tabs = Plugin.Config.Tabs.Select((tab, idx) => new ChatTab(tab.Name, idx, tab.Unread)).ToArray();
        return new ChatTabList(tabs);
    }
}
