using Dalamud.Plugin.Ipc;

namespace ChatTwo.Ipc;

using MessageStyle = (uint BackgroundRgba, float Alpha);

public sealed class StyleIpc : IDisposable
{
    private const int Version = 1;

    private ICallGateProvider<int> VersionGate { get; }
    private ICallGateProvider<string, object?> SetProviderGate { get; }

    // Written from the registering plugin's thread, read from the message
    // processing thread; reference reads/writes are atomic.
    private ICallGateSubscriber<string, string, ulong, ushort, string, string, MessageStyle>? Provider;

    public bool HasProvider => Provider != null;

    public StyleIpc()
    {
        VersionGate = Plugin.Interface.GetIpcProvider<int>("ChatTwo.StyleVersion");
        VersionGate.RegisterFunc(() => Version);

        SetProviderGate = Plugin.Interface.GetIpcProvider<string, object?>("ChatTwo.SetMessageStyleProvider");
        SetProviderGate.RegisterAction(SetProvider);
    }

    private void SetProvider(string gateName)
    {
        Provider = string.IsNullOrEmpty(gateName)
            ? null
            : Plugin.Interface.GetIpcSubscriber<string, string, ulong, ushort, string, string, MessageStyle>(gateName);
    }

    /// <summary>
    /// Asks the registered style provider for a background colour (RGBA, 0 for
    /// none) and alpha (1 = normal, 0 &lt; a &lt; 1 = faded, &lt;= 0 = hidden
    /// from the log) for a message. Called once per message on the message
    /// processing thread; providers must be thread-safe.
    /// </summary>
    public MessageStyle Evaluate(string senderName, string senderWorld, ulong contentId, ushort chatType, string senderRaw, string contentText)
    {
        if (Provider is not { } provider)
            return (0, 1f);

        try
        {
            return provider.InvokeFunc(senderName, senderWorld, contentId, chatType, senderRaw, contentText);
        }
        catch (Exception)
        {
            // A broken or unloaded provider must never break message
            // ingestion; unstyled is the safe fallback.
            return (0, 1f);
        }
    }

    public void Dispose()
    {
        SetProviderGate.UnregisterAction();
        VersionGate.UnregisterFunc();
    }
}
