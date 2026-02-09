using WatsonWebserver.Core;
using WatsonWebserver.Lite;

namespace ChatTwo.Http;

public class HostContext
{
    public readonly ServerCore Core;

    public bool IsActive;
    public bool IsStopping;

    // Initialized at webserver start
    public WebserverLite Host = null!;
    public Processing Processing = null!;
    public RouteController RouteController = null!;

    public readonly List<SSEConnection> EventConnections = [];

    public readonly CancellationTokenSource TokenSource = new();
    public readonly string StaticDir = Path.Combine(Plugin.Interface.AssemblyLocation.DirectoryName!, "Frontend/");

    public HostContext(ServerCore core)
    {
        Core = core;
    }

    public bool Start()
    {
        try
        {
            Host = new WebserverLite(new WebserverSettings("*", Plugin.Config.WebinterfacePort), DefaultRoute);

            Processing = new Processing(this);
            RouteController = new RouteController(this);

            Host.Routes.PreAuthentication.Content.BaseDirectory = StaticDir;
            Host.Routes.AuthenticateRequest = CheckAuthenticationCookie;
            Host.Events.ExceptionEncountered += ExceptionEncountered;

            // Settings
            #if DEBUG
            Host.Settings.Debug.Requests = true;
            Host.Settings.Debug.Routing = true;
            Host.Settings.Debug.Responses = true;
            Host.Settings.Debug.AccessControl = true;
            #endif
            Host.Events.Logger = logMessage => Plugin.Log.Debug(logMessage);

            IsActive = true;
            return true;
        }
        catch (Exception ex)
        {
            IsActive = false;
            Plugin.Log.Error(ex, "Initialization of the webserver failed.");
            return false;
        }
    }

    public void Run()
    {
        try
        {
            Host.Start(TokenSource.Token);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Webserver failed to boot up.");
        }
    }

    public async ValueTask<bool> Stop()
    {
        // Is already stopped
        if (!IsActive)
            return true;

        try
        {
            IsActive = false;
            IsStopping = true;
            Host.Stop();

            // Save our session tokens
            Core.Plugin.SaveConfig();

            // We get a copy, so that the original can be cleaned up successfully
            foreach (var eventServer in EventConnections.ToArray())
                await eventServer.DisposeAsync();

            EventConnections.Clear();
            Host.Dispose();
            RouteController.Dispose();
            IsStopping = false;

            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Webserver failed to stop and dispose all resources.");
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Stop();
    }

    #region GeneralHandlers
    private static void ExceptionEncountered(object? _, ExceptionEventArgs args)
    {
        Plugin.Log.Error(args.Exception, "Webserver threw an exception.");
    }

    private async Task<bool> DefaultRoute(HttpContextBase ctx)
    {
        return await ctx.Response.Send("Nothing to see here.");
    }

    private async Task CheckAuthenticationCookie(HttpContextBase ctx)
    {
        if (Plugin.Config.AuthStore.Count == 0)
        {
            await RouteController.Redirect(ctx, "/", ("message", "Invalid session token."));
            return;
        }

        var cookies = WebserverUtil.GetCookieData(ctx.Request.Headers.Get("Cookie") ?? "");
        if (!cookies.TryGetValue("ChatTwo-token", out var token) || !Plugin.Config.AuthStore.Contains(token))
            await RouteController.Redirect(ctx, "/", ("message", "Invalid session token."));

        // Do nothing to let auth pass
    }
    #endregion
}