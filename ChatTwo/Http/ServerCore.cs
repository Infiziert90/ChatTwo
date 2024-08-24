using ChatTwo.Code;
using ChatTwo.Http.MessageProtocol;
using WatsonWebserver.Core;
using WatsonWebserver.Lite;
using ExceptionEventArgs = WatsonWebserver.Core.ExceptionEventArgs;

namespace ChatTwo.Http;

public class ServerCore : IAsyncDisposable
{
    private readonly Plugin Plugin;
    internal readonly Processing Processing;
    internal readonly RouteController RouteController;

    internal readonly WebserverLite HostContext;

    internal readonly CancellationTokenSource TokenSource = new();
    internal readonly string StaticDir = Path.Combine(Plugin.Interface.AssemblyLocation.DirectoryName!, "Http");

    internal readonly List<SSEConnection> EventConnections = [];

    public ServerCore(Plugin plugin)
    {
        Plugin = plugin;
        HostContext = new WebserverLite(new WebserverSettings("*", 9000), DefaultRoute);

        Processing = new Processing(plugin);
        RouteController = new RouteController(plugin, this);

        HostContext.Routes.PreAuthentication.Content.BaseDirectory = StaticDir;
        HostContext.Routes.AuthenticateRequest = CheckAuthenticationCookie;
        HostContext.Events.ExceptionEncountered += ExceptionEncountered;

        // Settings
        HostContext.Settings.Debug.Requests = true;
        HostContext.Settings.Debug.Routing = true;
        HostContext.Settings.Debug.Responses = true;
        HostContext.Settings.Debug.AccessControl = true;
        HostContext.Events.Logger = logMessage => Plugin.Log.Information(logMessage);
    }

    #region SSEFunctions
    internal void SendNewMessage(Message message)
    {
        try
        {
            Plugin.Framework.RunOnTick(() =>
            {
                var bundledResponse = new NewMessageEvent(new Messages([Processing.ReadMessageContent(message)]));
                foreach (var eventServer in EventConnections)
                    eventServer.OutboundQueue.Enqueue(bundledResponse);
            });
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Sending message over SSE failed.");
        }
    }

    internal void SendChannelSwitch(Chunk[] channelName)
    {
        try
        {
            Plugin.Framework.RunOnTick(() =>
            {
                var bundledResponse = new SwitchChannelEvent(new SwitchChannel(Processing.ReadChannelName(channelName)));
                foreach (var eventServer in EventConnections)
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
        try
        {
            Plugin.Framework.RunOnTick(() =>
            {
                var channels = Plugin.ChatLogWindow.GetAvailableChannels();
                var bundledResponse = new ChannelListEvent(new ChannelList(channels.ToDictionary(pair => pair.Key, pair => (uint)pair.Value)));
                foreach (var eventServer in EventConnections)
                    eventServer.OutboundQueue.Enqueue(bundledResponse);
            });
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Sending channel switch over SSE failed.");
        }
    }
    #endregion

    #region GeneralHandlers
    private static void ExceptionEncountered(object? _, ExceptionEventArgs args)
    {
        Plugin.Log.Error(args.Exception, "Webserver threw an exception.");
    }

    private async Task DefaultRoute(HttpContextBase ctx)
    {
        await ctx.Response.Send("Nothing to see here.");
    }
    #endregion

    private async Task CheckAuthenticationCookie(HttpContextBase ctx)
    {
        var cookie = ctx.Request.Headers.Get("Cookie") ?? "";
        if (!cookie.StartsWith("auth=") || cookie[5..] != Plugin.Config.WebinterfacePassword)
        {
            ctx.Response.StatusCode = 401;
            await ctx.Response.Send("Your session auth code was invalid");
        }

        // Do nothing to let auth pass
    }

    public bool GetStats()
    {
        return HostContext.IsListening;
    }

    public void Start()
    {
        try
        {
            HostContext.Start(TokenSource.Token);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Startup failed with an error.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await TokenSource.CancelAsync();
        HostContext.Stop();

        // We get a copy, so that the original can be cleaned up succesfully
        foreach (var eventServer in EventConnections.ToArray())
            await eventServer.DisposeAsync();

        HostContext.Dispose();
        RouteController.Dispose();
    }
}