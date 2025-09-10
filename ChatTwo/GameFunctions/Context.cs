using ChatTwo.Util;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace ChatTwo.GameFunctions;

internal sealed unsafe class Context
{
    internal static void InviteToNoviceNetwork(string name, ushort world)
    {
        // can specify content id if we have it, but there's no need
        InfoProxyNoviceNetwork.Instance()->InviteToNoviceNetwork(0, 0, world, name.ToTerminatedBytes());
    }

    internal static void TryOn(uint itemId, byte stainId)
    {
        AgentTryon.TryOn(0xFF, itemId, stainId, 0, 0);
    }

    internal static void LinkItem(uint itemId)
    {
        AgentChatLog.Instance()->LinkItem(itemId);
    }

    internal static void LinkStatus(uint statusId)
    {
        AgentChatLog.Instance()->ContextStatusId = statusId;
    }

    internal static void OpenItemComparison(uint itemId)
    {
        AgentItemComp.Instance()->CompareItem(0x4D, itemId, 0, 0);
    }

    internal static void SearchForRecipesUsingItem(uint itemId)
    {
        AgentRecipeProductList.Instance()->SearchForRecipesUsingItem(itemId);
    }

    internal static void SearchForItem(uint itemId)
    {
        ItemFinderModule.Instance()->SearchForItem(itemId, true);
    }
}
