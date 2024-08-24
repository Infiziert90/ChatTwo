using ChatTwo.Http.MessageProtocol;
using EmbedIO.WebSockets;
using Newtonsoft.Json;

namespace ChatTwo.Http;

public class WebSocketServer : WebSocketModule {
    private readonly SemaphoreSlim SendLock = new(1, 1);

    public event EventHandler? OnClientConnected;

    public WebSocketServer(string urlPath) : base(urlPath, true) {

    }

    protected override async Task OnMessageReceivedAsync(IWebSocketContext context, byte[] buffer, IWebSocketReceiveResult result)
    {
        // Unused method
    }

    protected override Task OnClientConnectedAsync(IWebSocketContext context)
    {
        Plugin.Log.Information($"Client connected: {context.Id}");
        OnClientConnected?.Invoke(this, EventArgs.Empty);
        return base.OnClientConnectedAsync(context);
    }

    protected override Task OnClientDisconnectedAsync(IWebSocketContext context)
    {
        Plugin.Log.Information($"Client disconnected: {context.Id}");
        return base.OnClientConnectedAsync(context);
    }

    protected override void Dispose(bool disposing) {
        base.Dispose(disposing);

        SendLock.Dispose();
    }

    public void BroadcastMessage(BaseOutboundMessage message) {
        Task.Run(async () => {
            using (await SendLock.UseWaitAsync()) {
                var serializedData = JsonConvert.SerializeObject(message);
                await BroadcastAsync(serializedData);
            }
        });
    }
}