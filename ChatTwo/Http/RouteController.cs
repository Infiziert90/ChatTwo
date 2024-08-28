using System.Collections.Concurrent;
using System.Web;
using ChatTwo.Code;
using ChatTwo.Http.MessageProtocol;
using ChatTwo.Util;
using Lumina.Data.Files;
using Newtonsoft.Json;
using WatsonWebserver.Core;
using ErrorEventArgs = Newtonsoft.Json.Serialization.ErrorEventArgs;
using HttpMethod = WatsonWebserver.Core.HttpMethod;

namespace ChatTwo.Http;

public class RouteController
{
    private readonly Plugin Plugin;
    private readonly HostContext Core;

    private readonly string AuthTemplate;
    private readonly string ChatBoxTemplate;

    private readonly ConcurrentDictionary<string, long> RateLimit = [];

    private readonly JsonSerializerSettings JsonSettings = new()
    {
        Error = delegate(object? _, ErrorEventArgs args) { args.ErrorContext.Handled = true; }
    };

    public RouteController(Plugin plugin, HostContext core)
    {
        Plugin = plugin;
        Core = core;

        AuthTemplate = File.ReadAllText(Path.Combine(Core.StaticDir, "templates", "auth.html"));
        ChatBoxTemplate = File.ReadAllText(Path.Combine(Core.StaticDir, "templates", "chat.html"));

        // Pre Auth
        Core.Host.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/", AuthRoute, ExceptionRoute);
        Core.Host.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/auth", GetAuthenticateClient, ExceptionRoute);
        Core.Host.Routes.PreAuthentication.Static.Add(HttpMethod.POST, "/auth", AuthenticateClient, ExceptionRoute);
        Core.Host.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/files/gfdata.gfd", GetGfdData, ExceptionRoute);
        Core.Host.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/files/fonticon_ps5.tex", GetTexData, ExceptionRoute);
        Core.Host.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/files/FFXIV_Lodestone_SSF.ttf", GetLodestoneFont, ExceptionRoute);
        Core.Host.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/favicon.ico", GetFavicon, ExceptionRoute);
        Core.Host.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, "/emote/{name}", GetEmote, ExceptionRoute);
        Core.Host.Routes.PreAuthentication.Content.Add("/static", true, ExceptionRoute);

        // Post Auth
        Core.Host.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/chat", ChatBoxRoute, ExceptionRoute);
        Core.Host.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/send", ReceiveMessage, ExceptionRoute);
        Core.Host.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/channel", ReceiveChannelSwitch, ExceptionRoute);

        // Server-Sent Events Route
        Core.Host.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/sse", NewSSEConnection, ExceptionRoute);
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

    private async Task GetFavicon(HttpContextBase ctx)
    {
        ctx.Response.StatusCode = 404;
        await ctx.Response.Send();
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
        if (emote is null)
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.Send("Emote not valid.");
            return;
        }

        // Wait for the emote to be loaded a maximum of 5 times
        var timeout = 5;
        while (!emote.IsLoaded && timeout > 0)
        {
            timeout--;
            await Task.Delay(25);
        }

        ctx.Response.Headers.Add("Cache-Control", "max-age=86400");
        await ctx.Response.Send(emote.RawData);
    }
    #endregion

    #region PreAuthRoutes
    private async Task<bool> AuthenticateClient(HttpContextBase ctx)
    {
        Plugin.Log.Information($"Auth requested");

        var currentTick = Environment.TickCount64;
        if (RateLimit.TryGetValue(ctx.Request.Source.IpAddress, out var timestamp) && timestamp > currentTick)
            return await Redirect(ctx, "/", ("message", "Rate limit active."));

        // The next request will be rate limited for 10s
        RateLimit[ctx.Request.Source.IpAddress] = currentTick + 10_000;

        var authcode = HttpUtility.ParseQueryString(ctx.Request.DataAsString ?? "").Get("authcode");
        if (authcode == null || authcode != Plugin.Config.WebinterfacePassword)
            return await Redirect(ctx, "/", ("message", "Authentication failed."));

        var token = WebinterfaceUtil.GenerateSimpleToken();
        Plugin.Config.SessionTokens.TryAdd(token, true);

        ctx.Response.Headers.Add("Set-Cookie", $"ChatTwo-token={token}");
        return await Redirect(ctx, "/chat");
    }

    private async Task<bool> GetAuthenticateClient(HttpContextBase ctx)
    {
        Plugin.Log.Information($"Get was used for auth requested");
        Plugin.Log.Information($"{ctx.Request.Url.Full}");
        Plugin.Log.Information($"{ctx.RouteType}");

        return await Redirect(ctx, "/", ("message", "Rate limit active."));
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
        if (ctx.Request.ContentType != "application/json")
        {
            ctx.Response.StatusCode = 415;
            await ctx.Response.Send(JsonConvert.SerializeObject(new ErrorResponse("Request contains wrong media type.")));
            return;
        }

        var content = JsonConvert.DeserializeObject<IncomingMessage>(ctx.Request.DataAsString, JsonSettings);
        if (content.Message.Length is < 2 or > 500)
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.Send(JsonConvert.SerializeObject(new ErrorResponse("Invalid message received.")));
            return;
        }

        await Plugin.Framework.RunOnFrameworkThread(() =>
        {
            Plugin.ChatLogWindow.Chat = content.Message;
            Plugin.ChatLogWindow.SendChatBox(Plugin.ChatLogWindow.CurrentTab);
        });

        ctx.Response.StatusCode = 201;
        await ctx.Response.Send(JsonConvert.SerializeObject(new OkResponse("Message was send to the channel.")));
    }

    private async Task ReceiveChannelSwitch(HttpContextBase ctx)
    {
        if (ctx.Request.ContentType != "application/json")
        {
            ctx.Response.StatusCode = 415;
            await ctx.Response.Send(JsonConvert.SerializeObject(new ErrorResponse("Request contains wrong media type.")));
            return;
        }

        var channel = JsonConvert.DeserializeObject<IncomingChannel>(ctx.Request.DataAsString, JsonSettings);
        if (!Enum.IsDefined(typeof(InputChannel), channel.Channel))
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.Send(JsonConvert.SerializeObject(new ErrorResponse("Invalid channel received.")));
            return;
        }

        await Plugin.Framework.RunOnFrameworkThread(() =>
        {
            Plugin.ChatLogWindow.SetChannel((InputChannel)channel.Channel);
        });

        ctx.Response.StatusCode = 201;
        await ctx.Response.Send(JsonConvert.SerializeObject(new OkResponse("Channel switch got initiated.")));
    }

    private async Task NewSSEConnection(HttpContextBase ctx)
    {
        try
        {
            Plugin.Log.Information($"Client connected: {ctx.Guid}");

            var sse = new SSEConnection(Core.TokenSource.Token);
            await Core.Processing.PrepareNewClient(sse);
            Core.EventConnections.Add(sse);

            await sse.HandleEventLoop(ctx);

            // It should always be done after return
            if (sse.Done)
                Core.EventConnections.Remove(sse);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to finish the server event function");
        }
    }
    #endregion

    #region RedirectHelper
    public static async Task<bool> Redirect(HttpContextBase ctx, string location, params (string, string)[] parameter)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        foreach (var (key, value) in parameter)
            query.Add(key, value);

        ctx.Response.Headers.Add("Location", $"{location}?{query}");
        ctx.Response.StatusCode = 302;
        return await ctx.Response.Send();
    }
    #endregion
}