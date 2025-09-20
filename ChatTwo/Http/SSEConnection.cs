using ChatTwo.Http.MessageProtocol;
using WatsonWebserver.Core;

namespace ChatTwo.Http;

public class SSEConnection
{
    private bool Stopping;
    private readonly CancellationToken Token;

    public bool Done;
    public readonly Queue<BaseEvent> OutboundQueue = new();

    public SSEConnection(CancellationToken token)
    {
        Token = token;
    }

    public async Task HandleEventLoop(HttpContextBase ctx)
    {
        try
        {
            ctx.Response.Headers.Add("Content-Type", "text/event-stream");
            ctx.Response.Headers.Add("Cache-Control", "no-cache");
            ctx.Response.Headers.Add("Connection", "keep-alive");

            ctx.Response.ChunkedTransfer = true;
            while (!Token.IsCancellationRequested && !Stopping)
            {
                await Task.Delay(10, Token);
                if (Token.IsCancellationRequested)
                    return;

                if (!OutboundQueue.TryDequeue(out var outgoingEvent))
                    continue;

                if (!await ctx.Response.SendChunk(outgoingEvent.Build(), false, Token))
                {
                    Plugin.Log.Information("SSE connection was unable to send new data");
                    Plugin.Log.Information($"Client disconnected: {ctx.Guid}");
                    return;
                }
            }
        }
        catch (TaskCanceledException)
        {
            // Ignore
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "SSE handler failed.");
        }
        finally
        {
            // "No Content" (204) didn't work for Firefox, so manually closing the connection on client side
            await ctx.Response.SendChunk(new CloseEvent().Build(), true, Token);

            // Manually confirm that we have finished our connection, even if the final response failed
            // This can happen if the client disconnects before the server does
            ctx.Response.ResponseSent = true;

            Done = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        Stopping = true;

        var timeout = 1000; // 1000ms
        while (timeout > 0)
        {
            if (Done)
                break;

            timeout -= 100;
            await Task.Delay(100);
            Plugin.Log.Debug("Sleeping because EventServer still alive");
        }
    }
}