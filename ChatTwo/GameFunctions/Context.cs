using ChatTwo.Util;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace ChatTwo.GameFunctions;

internal sealed unsafe class Context
{
    [Signature("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8B CB E8 ?? ?? ?? ?? 45 33 C9", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<IntPtr, ulong, ushort, byte*, byte> InviteToNoviceNetworkNative = null!;

    [Signature("E8 ?? ?? ?? ?? EB 35 BA", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<uint, uint, ulong, uint, byte, byte> TryOnNative = null!;

    [Signature("E8 ?? ?? ?? ?? EB 7B 49 8B 06", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<AgentInterface*, uint, void> LinkItemNative = null!;

    [Signature("E8 ?? ?? ?? ?? EB 3F 83 F8 FE", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<AgentInterface*, ushort, uint, byte, void> ItemComparisonNative = null!;

    [Signature("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 83 F8 0F", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<IntPtr, uint, void> SearchForRecipesUsingItemNative = null!;

    [Signature("E8 ?? ?? ?? ?? EB 45 45 33 C9", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<void*, uint, byte, void> SearchForItemNative = null!;

    #region Offsets
    [Signature(
        "FF 90 ?? ?? ?? ?? 8B 93 ?? ?? ?? ?? 48 8B C8 E8 ?? ?? ?? ?? 41 0F B6 D4 48 8B CB E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 81 FF ?? ?? ?? ?? 0F 85",
        Offset = 2
    )]
    private readonly int? SearchForRecipesUsingItemVfunc;
    #endregion

    private Plugin Plugin { get; }

    internal Context(Plugin plugin)
    {
        Plugin = plugin;
        Plugin.GameInteropProvider.InitializeFromAttributes(this);
    }

    internal void InviteToNoviceNetwork(string name, ushort world)
    {
        if (InviteToNoviceNetworkNative == null)
            return;

        // 6.3: 221EFD
        var a1 = Plugin.Functions.GetInfoProxyByIndex(0x14);

        // can specify content id if we have it, but there's no need
        fixed (byte* namePtr = name.ToTerminatedBytes()) {
            InviteToNoviceNetworkNative(a1, 0, world, namePtr);
        }
    }

    // These context menu things come from AgentChatLog.vf0 at the bottom
    // 0x10000: item comparison
    // 0x10001: try on
    // 0x10002: search for item
    // 0x10003: link
    // 0x10005: copy item name
    // 0x10006: search recipes using this material

    internal void TryOn(uint itemId, byte stainId)
    {
        if (TryOnNative == null)
            return;

        TryOnNative(0xFF, itemId, stainId, 0, 0);
    }

    internal void LinkItem(uint itemId)
    {
        if (LinkItemNative == null)
            return;

        var agent = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.ChatLog);
        LinkItemNative(agent, itemId);
    }

    internal void OpenItemComparison(uint itemId)
    {
        if (ItemComparisonNative == null)
            return;

        var agent = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.ItemCompare);
        ItemComparisonNative(agent, 0x4D, itemId, 0);
    }

    internal void SearchForRecipesUsingItem(uint itemId)
    {
        if (SearchForRecipesUsingItemNative == null || SearchForRecipesUsingItemVfunc is not { } offset)
            return;

        var uiModule = Framework.Instance()->GetUiModule();
        var vf = (delegate* unmanaged<UIModule*, IntPtr>) uiModule->vfunc[offset / 8];
        var a1 = vf(uiModule);
        SearchForRecipesUsingItemNative(a1, itemId);
    }

    internal void SearchForItem(uint itemId)
    {
        if (SearchForItemNative == null)
            return;

        var itemFinder = Framework.Instance()->GetUiModule()->GetItemFinderModule();
        SearchForItemNative(itemFinder, itemId, 1);
    }
}
