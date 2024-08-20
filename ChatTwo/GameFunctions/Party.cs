using ChatTwo.Resources;
using ChatTwo.Util;
using Dalamud.Interface.ImGuiNotification;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace ChatTwo.GameFunctions;

internal static unsafe class Party
{
    internal static void InviteSameWorld(string name, ushort world, ulong contentId)
    {
        // this only works if target is on the same world
        fixed (byte* namePtr = name.ToTerminatedBytes()) {
            InfoProxyPartyInvite.Instance()->InviteToParty(contentId, namePtr, world);
        }
    }

    internal static void InviteOtherWorld(ulong contentId)
    {
        // third param is world, but it requires a specific world
        // if they're not on that world, it will fail
        // pass 0 and it will work on any world EXCEPT for the world the
        // current player is on
        if (contentId == 0)
        {
            WrapperUtil.AddNotification(Language.PartyInvite_NoId, NotificationType.Warning);
            return;
        }

        InfoProxyPartyInvite.Instance()->InviteToPartyContentId(contentId, 0);
    }

    internal static void InviteInInstance(ulong contentId)
    {
        if (contentId == 0)
        {
            WrapperUtil.AddNotification(Language.PartyInvite_NoId, NotificationType.Warning);
            return;
        }

        InfoProxyPartyInvite.Instance()->InviteToPartyInInstance(contentId);
    }

    internal static void Kick(string name, ulong contentId)
    {
        fixed (byte* namePtr = name.ToTerminatedBytes()) {
            AgentPartyMember.Instance()->Kick(namePtr, 0, contentId);
        }
    }

    internal static void Promote(string name, ulong contentId)
    {
        fixed (byte* namePtr = name.ToTerminatedBytes()) {
            AgentPartyMember.Instance()->Promote(namePtr, 0, contentId);
        }
    }
}
