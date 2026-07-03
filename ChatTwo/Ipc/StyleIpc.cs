using Dalamud.Plugin.Ipc;

namespace ChatTwo.Ipc;

using MessageStyle = (uint BackgroundRgba, float Alpha);

public sealed class StyleIpc : IDisposable
{
    private const int Version = 1;

    // Suppress-flags for SetTabStylePolicies; a tab without an entry has all
    // styling enabled.
    public const int PolicySuppressBackground = 1;
    public const int PolicySuppressFade = 2;
    public const int PolicySuppressHide = 4;

    private ICallGateProvider<int> VersionGate { get; }
    private ICallGateProvider<string, object?> SetProviderGate { get; }
    private ICallGateProvider<Dictionary<Guid, string>> GetTabsGate { get; }
    private ICallGateProvider<Dictionary<Guid, string>, object?> TabsChangedGate { get; }
    private ICallGateProvider<Dictionary<Guid, int>, object?> SetTabPoliciesGate { get; }

    // Written from the registering plugin's thread, read from the message
    // processing thread; reference reads/writes are atomic.
    private ICallGateSubscriber<string, string, ulong, ushort, string, string, MessageStyle>? Provider;

    // Swapped as a whole on writes, so render-thread reads are safe.
    private Dictionary<Guid, int> TabPolicies = new();

    public bool HasProvider => Provider != null;

    public StyleIpc()
    {
        VersionGate = Plugin.Interface.GetIpcProvider<int>("ChatTwo.StyleVersion");
        VersionGate.RegisterFunc(() => Version);

        SetProviderGate = Plugin.Interface.GetIpcProvider<string, object?>("ChatTwo.SetMessageStyleProvider");
        SetProviderGate.RegisterAction(SetProvider);

        GetTabsGate = Plugin.Interface.GetIpcProvider<Dictionary<Guid, string>>("ChatTwo.GetTabs");
        GetTabsGate.RegisterFunc(BuildTabList);

        TabsChangedGate = Plugin.Interface.GetIpcProvider<Dictionary<Guid, string>, object?>("ChatTwo.TabsChanged");

        SetTabPoliciesGate = Plugin.Interface.GetIpcProvider<Dictionary<Guid, int>, object?>("ChatTwo.SetTabStylePolicies");
        // Copy defensively: the render thread reads this dictionary, so it
        // must not share state with (or be a null from) the calling plugin.
        SetTabPoliciesGate.RegisterAction(policies => TabPolicies = policies is null ? [] : new Dictionary<Guid, int>(policies));
    }

    private static Dictionary<Guid, string> BuildTabList()
        => Plugin.Config.Tabs
            .Where(tab => !tab.IsTempTab)
            .ToDictionary(tab => tab.Identifier, tab => tab.Name);

    /// <summary>
    /// Pushes the current tab list to subscribers; called whenever the
    /// configuration is saved.
    /// </summary>
    public void NotifyTabsChanged()
    {
        try
        {
            TabsChangedGate.SendMessage(BuildTabList());
        }
        catch (Exception ex)
        {
            // SendMessage runs subscribers synchronously and does not isolate
            // their exceptions; a broken consumer must not break config saves.
            Plugin.Log.Warning(ex, "Error in a ChatTwo.TabsChanged subscriber");
        }
    }

    /// <summary>
    /// Suppress-flags for a tab; 0 means all styling is enabled.
    /// </summary>
    public int GetTabPolicy(Guid tabIdentifier)
        => TabPolicies.GetValueOrDefault(tabIdentifier, 0);

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
