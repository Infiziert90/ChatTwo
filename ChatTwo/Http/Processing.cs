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

    internal (MessageTemplate[] Name, bool Locked) ReadChannelName(Chunk[] channelName)
    {
        var locked = HostContext.Core.Plugin.CurrentTab is not { Channel: null };
        return (channelName.Select(ProcessChunk).ToArray(), locked);
    }

    internal async Task<MessageResponse[]> ReadMessageList()
    {
        var tabMessages = await HostContext.Core.Plugin.CurrentTab.Messages.GetCopy();
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

            var userContent = text.Content ?? string.Empty;
            if (HostContext.Core.Plugin.ChatLogWindow.ScreenshotMode)
            {
                if (chunk.Link is PlayerPayload playerPayload)
                    userContent = HostContext.Core.Plugin.ChatLogWindow.HidePlayerInString(userContent, playerPayload.PlayerName, playerPayload.World.RowId);
                else if (Plugin.ClientState.LocalPlayer is { } player)
                    userContent = HostContext.Core.Plugin.ChatLogWindow.HidePlayerInString(userContent, player.Name.TextValue, player.HomeWorld.RowId);
            }

            var isNotUrl = text.Link is not UriPayload;
            return new MessageTemplate { Payload = isNotUrl ? "text" : "url", Color = color.Value, Content = userContent };
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
        var channel = ReadChannelName(HostContext.Core.Plugin.ChatLogWindow.PreviousChannel);
        return new SwitchChannel(channel);
    }

    public ChannelList GetValidChannels()
    {
        var channels = HostContext.Core.Plugin.ChatLogWindow.GetValidChannels();
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
