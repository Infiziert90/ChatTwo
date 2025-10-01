using ChatTwo.Http.MessageProtocol;
using ChatTwo.Util;
using Dalamud.Plugin.Services;

namespace ChatTwo.Http;

public class ServerCore : IAsyncDisposable
{
    public readonly Plugin Plugin;
    private readonly HostContext HostContext;

    public ServerCore(Plugin plugin)
    {
        Plugin = plugin;
        HostContext = new HostContext(this);

        Plugin.Framework.Update += FrameworkUpdate;
    }

    public async ValueTask DisposeAsync()
    {
        Plugin.Framework.Update -= FrameworkUpdate;
        await HostContext.DisposeAsync();
    }

    private void FrameworkUpdate(IFramework _)
    {
        foreach (var (tab, idx) in Plugin.Config.Tabs.WithIndex())
        {
            if (tab.Unread == tab.LastSendUnread)
                continue;

            tab.LastSendUnread = tab.Unread;
            foreach (var eventServer in HostContext.EventConnections)
                eventServer.OutboundQueue.Enqueue(new ChatTabUnreadStateEvent(new ChatTabUnreadState(idx, tab.Unread)));
        }
    }

    #region SSE Helper
    internal async Task PrepareNewClient(SSEConnection sse)
    {
        // This takes long, so keep it outside the next frame
        var messages = await HostContext.Processing.GetAllMessages();

        // Using the bulk message event to clear everything on the client side that may still exist
        await Plugin.Framework.RunOnTick(() =>
        {
            sse.OutboundQueue.Enqueue(new BulkMessagesEvent(messages));

            sse.OutboundQueue.Enqueue(new SwitchChannelEvent(HostContext.Processing.GetCurrentChannel()));
            sse.OutboundQueue.Enqueue(new ChannelListEvent(HostContext.Processing.GetValidChannels()));

            sse.OutboundQueue.Enqueue(new ChatTabSwitchedEvent(HostContext.Processing.GetCurrentTab()));
            sse.OutboundQueue.Enqueue(new ChatTabListEvent(HostContext.Processing.GetAllTabs()));
        });
    }

    internal void SendNewMessage(Message message)
    {
        if (!HostContext.IsActive)
            return;

        try
        {
            Plugin.Framework.RunOnTick(() =>
            {
                var bundledResponse = new NewMessageEvent(HostContext.Processing.ReadMessageContent(message));
                foreach (var eventServer in HostContext.EventConnections)
                    eventServer.OutboundQueue.Enqueue(bundledResponse);
            });
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Sending message over SSE failed.");
        }
    }

    internal void SendBulkMessageList()
    {
        if (!HostContext.IsActive)
            return;

        try
        {
            Plugin.Framework.RunOnTick(() =>
            {
                foreach (var eventServer in HostContext.EventConnections)
                    eventServer.OutboundQueue.Enqueue(new BulkMessagesEvent(new Messages(HostContext.Processing.ReadMessageList().Result)));
            });
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Sending channel switch over SSE failed.");
        }
    }

    internal void SendChannelSwitch(Chunk[] channelName)
    {
        if (!HostContext.IsActive)
            return;

        try
        {
            Plugin.Framework.RunOnTick(() =>
            {
                var bundledResponse = new SwitchChannelEvent(new SwitchChannel(HostContext.Processing.ReadChannelName(channelName)));
                foreach (var eventServer in HostContext.EventConnections)
                    eventServer.OutboundQueue.Enqueue(bundledResponse);
            });
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Sending channel switch over SSE failed.");
        }
    }

    internal void SendChannelList()
    {
        if (!HostContext.IsActive)
            return;

        try
        {
            Plugin.Framework.RunOnTick(() =>
            {
                var bundledResponse = new ChannelListEvent(HostContext.Processing.GetValidChannels());
                foreach (var eventServer in HostContext.EventConnections)
                    eventServer.OutboundQueue.Enqueue(bundledResponse);
            });
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Sending channel switch over SSE failed.");
        }
    }

    internal void SendNewLogin()
    {
        if (!HostContext.IsActive)
            return;

        try
        {
            Plugin.Framework.RunOnTick(async () =>
            {
                foreach (var eventServer in HostContext.EventConnections)
                    await HostContext.Core.PrepareNewClient(eventServer);
            });
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Preparing all clients after login failed.");
        }
    }
    #endregion

    public void InvalidateSessions()
    {
        if (!HostContext.IsActive)
            return;

        Plugin.Config.AuthStore.Clear();
        Plugin.SaveConfig();
    }

    public bool IsActive()
    {
        return HostContext is { IsActive: true, Host.IsListening: true };
    }

    public bool IsStopping()
    {
        return HostContext is { IsActive: false, IsStopping: true };
    }


    public bool Start()
    {
        return HostContext.Start();
    }

    public void Run()
    {
        HostContext.Run();
    }

    public async ValueTask<bool> Stop()
    {
        return await HostContext.Stop();
    }
}