using ChatTwo.Http.MessageProtocol;
using EmbedIO;
using WatsonWebserver.Core;
using WatsonWebserver.Lite;
using ExceptionEventArgs = WatsonWebserver.Core.ExceptionEventArgs;

namespace ChatTwo.Http;

public class ServerCore : IDisposable
{
    private readonly Plugin Plugin;
    private readonly Processing Processing;
    private readonly RouteController RouteController;

    internal readonly WebserverLite HostContext;
    private readonly WebSocketServer Websocket;
    private readonly WebServer Host;

    internal readonly CancellationTokenSource TokenSource = new();
    internal readonly string StaticDir = Path.Combine(Plugin.Interface.AssemblyLocation.DirectoryName!, "Http");

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


        // Websocket
        Host = new WebServer(o => o
            .WithUrlPrefixes($"http://*:9001")
            .WithMode(HttpListenerMode.EmbedIO)
        );

        Websocket = new WebSocketServer("/ws");
        Host.WithModule(Websocket);

        Websocket.OnClientConnected += ClientConnected;
    }

    #region WebsocketFunctions
    private void ClientConnected(object? sender, EventArgs args)
    {
        Task.Run(async () =>
        {
            var messages = await WebserverUtil.FrameworkWrapper(Processing.ReadMessageList);
            Websocket.BroadcastMessage(new WebSocketNewMessage(messages.ToArray()));
        });
    }

    internal void SendNewMessage(Message message)
    {
        try
        {
            Plugin.Framework.RunOnTick(() =>
            {
                Websocket.BroadcastMessage(new WebSocketNewMessage([Processing.ReadMessageContent(message)]));
            });
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Send message to websockets failed.");
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
            Host.Start(TokenSource.Token);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Startup failed with an error.");
        }
    }

    public void Dispose()
    {
        TokenSource.Cancel();

        HostContext.Stop();
        HostContext.Dispose();

        Websocket.Dispose();
        Host.Dispose();

        RouteController.Dispose();
    }
}