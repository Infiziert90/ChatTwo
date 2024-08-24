using ChatTwo.Http.MessageProtocol;
using Lumina.Data.Files;
using WatsonWebserver.Core;

using HttpMethod = WatsonWebserver.Core.HttpMethod;

namespace ChatTwo.Http;

public class RouteController
{
    private readonly Plugin Plugin;
    private readonly ServerCore Core;

    private readonly string AuthTemplate;
    private readonly string ChatBoxTemplate;

    public RouteController(Plugin plugin, ServerCore core)
    {
        Plugin = plugin;
        Core = core;

        AuthTemplate = File.ReadAllText(Path.Combine(Core.StaticDir, "templates", "auth.html"));
        ChatBoxTemplate = File.ReadAllText(Path.Combine(Core.StaticDir, "templates", "start.html"));

        // Pre Auth
        Core.HostContext.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/", AuthRoute, ExceptionRoute);
        Core.HostContext.Routes.PreAuthentication.Static.Add(HttpMethod.POST, "/auth", AuthenticateClient, ExceptionRoute);
        Core.HostContext.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/files/gfdata.gfd", GetGfdData, ExceptionRoute);
        Core.HostContext.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/files/fonticon_ps5.tex", GetTexData, ExceptionRoute);
        Core.HostContext.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/files/FFXIV_Lodestone_SSF.ttf", GetLodestoneFont, ExceptionRoute);
        Core.HostContext.Routes.PreAuthentication.Content.Add("/static", true, ExceptionRoute);

        // Post Auth
        Core.HostContext.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/chat", ChatBoxRoute, ExceptionRoute);
        Core.HostContext.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/send", ReceiveMessage, ExceptionRoute);
        Core.HostContext.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/emote/{name}", GetEmote, ExceptionRoute);

        Core.HostContext.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, "/sse", StartServerEvent, ExceptionRoute);
    }

    private async Task ExceptionRoute(HttpContextBase ctx, Exception _)
    {
        ctx.Response.StatusCode = 500;
        await ctx.Response.Send("Internal Server Error, please try again");
    }

    private async Task AuthRoute(HttpContextBase ctx)
    {
        await ctx.Response.Send(AuthTemplate);
    }

    public void Dispose()
    {

    }

    #region FileHandlerRoutes
    private async Task GetTexData(HttpContextBase ctx)
    {
        var data = Plugin.DataManager.GetFile<TexFile>("common/font/fonticon_ps5.tex")!.Data;
        await ctx.Response.Send(data);
    }

    private async Task GetGfdData(HttpContextBase ctx)
    {
        var data = Plugin.DataManager.GetFile("common/font/gfdata.gfd")!.Data;
        await ctx.Response.Send(data);
    }

    private async Task GetLodestoneFont(HttpContextBase ctx)
    {
        var data = Plugin.FontManager.GameSymFont;
        await ctx.Response.Send(data);
    }

    private async Task GetEmote(HttpContextBase ctx)
    {
        var name = ctx.Request.Url.Parameters["name"] ?? "";
        if (name == "" || !EmoteCache.Exists(name))
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.Send("Malformed emote name.");
            return;
        }


        var emote = EmoteCache.GetEmote(name);
        if (emote is not { IsLoaded: true })
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.Send("Emote not valid.");
            return;
        }

        ctx.Response.Headers.Add("Cache-Control", "max-age=86400");
        await ctx.Response.Send(emote.RawData);
    }
    #endregion

    #region PreAuthRoutes
    private async Task AuthenticateClient(HttpContextBase ctx)
    {
        var receivedPassword = ctx.Request.DataAsString ?? "";
        if (!receivedPassword.StartsWith("authcode="))
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.Send("Authentication content invalid.");
            return;
        }

        receivedPassword = receivedPassword[9..];
        if (receivedPassword != Plugin.Config.WebinterfacePassword)
        {
            ctx.Response.StatusCode = 401;
            await ctx.Response.Send("Authentication failed.");
            return;
        }

        ctx.Response.Headers.Add("Set-Cookie", $"auth={Plugin.Config.WebinterfacePassword}");
        ctx.Response.Headers.Add("Location", "/chat");
        ctx.Response.StatusCode = 302;
        await ctx.Response.Send();
    }
    #endregion

    #region PostAuthRoutes
    private async Task ChatBoxRoute(HttpContextBase ctx)
    {
        if (Plugin.ChatLogWindow.CurrentTab == null)
        {
            await ctx.Response.Send("No valid chat tab!");
            return;
        }

        await ctx.Response.Send(ChatBoxTemplate);
    }

    private async Task ReceiveMessage(HttpContextBase ctx)
    {
        var content = ctx.Request.DataAsString;
        if (content.Length is > 500 or < 2)
        {
            await ctx.Response.Send("Invalid length for a chat message received.");
            return;
        }

        await Plugin.Framework.RunOnFrameworkThread(() =>
        {
            Plugin.ChatLogWindow.Chat = content;
            Plugin.ChatLogWindow.SendChatBox(Plugin.ChatLogWindow.CurrentTab);
        });

        await ctx.Response.Send("Message was send to the channel.");
    }

    private async Task StartServerEvent(HttpContextBase ctx)
    {
        try
        {
            Plugin.Log.Information($"Client connected: {ctx.Guid}");

            var sse = new EventServer(Core.TokenSource.Token);
            Core.EventConnections.Add(sse);

            // TODO Check if reconnect or new connection
            var messages = await WebserverUtil.FrameworkWrapper(Core.Processing.ReadMessageList);
            sse.OutboundStack.Push(new NewMessage(messages.ToArray()));

            await sse.HandleEventLoop(ctx);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to finish the server event function");
        }
    }
    #endregion
}