using ChatTwo.Code;
using ChatTwo.Http.MessageProtocol;

namespace ChatTwo.Http;

public class ServerCore : IAsyncDisposable
{
    private readonly Plugin Plugin;
    private readonly HostContext HostContext;

    public ServerCore(Plugin plugin)
    {
        Plugin = plugin;
        HostContext = new HostContext(plugin);
    }

    #region SSE Helper
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
                var channels = Plugin.ChatLogWindow.GetValidChannels();
                var bundledResponse = new ChannelListEvent(new ChannelList(channels.ToDictionary(pair => pair.Key, pair => (uint)pair.Value)));
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
                    await HostContext.Processing.PrepareNewClient(eventServer);
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

    public async ValueTask DisposeAsync()
    {
        await HostContext.DisposeAsync();
    }
}