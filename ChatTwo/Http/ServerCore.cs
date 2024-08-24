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

    internal readonly List<EventServer> EventConnections = [];

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
                var bundledMessage = new NewMessage([Processing.ReadMessageContent(message)]);
                foreach (var eventServer in EventConnections)
                    eventServer.OutboundStack.Push(bundledMessage);
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
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Startup failed with an error.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await TokenSource.CancelAsync();

        foreach (var eventServer in EventConnections)
            await eventServer.DisposeAsync();

        HostContext.Stop();
        HostContext.Dispose();

        RouteController.Dispose();
    }
}