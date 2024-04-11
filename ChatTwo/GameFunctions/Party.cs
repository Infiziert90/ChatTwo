using ChatTwo.Util;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace ChatTwo.GameFunctions;

internal sealed unsafe class Party
{
    [Signature("E8 ?? ?? ?? ?? 33 C0 EB 51", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<IntPtr, ulong, byte*, ushort, byte> InviteToPartyNative = null!;

    [Signature("48 83 EC 38 41 B1 09", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<IntPtr, ulong, ushort, byte> InviteToPartyContentIdNative = null!;

    [Signature("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8B 83 ?? ?? ?? ?? 48 85 C0 74 62", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<IntPtr, ulong, byte> InviteToPartyInInstanceNative = null!;

    [Signature("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 49 8B 56 20", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<AgentInterface*, byte*, ushort, ulong, void> PromoteNative = null!;

    [Signature("E8 ?? ?? ?? ?? EB 66 49 8B 4E 20", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<AgentInterface*, byte*, ushort, ulong, void> KickNative = null!;

    private Plugin Plugin { get; }

    internal Party(Plugin plugin)
    {
        Plugin = plugin;
        Plugin.GameInteropProvider.InitializeFromAttributes(this);
    }

    internal void InviteSameWorld(string name, ushort world, ulong contentId)
    {
        if (InviteToPartyNative == null)
            return;

        // 6.11: 214A55
        var a1 = Plugin.Functions.GetInfoProxyByIndex(2);

        // this only works if target is on the same world
        fixed (byte* namePtr = name.ToTerminatedBytes()) {
            InviteToPartyNative(a1, contentId, namePtr, world);
        }
    }

    internal void InviteOtherWorld(ulong contentId)
    {
        if (InviteToPartyContentIdNative == null)
            return;

        // 6.11: 214A55
        var a1 = Plugin.Functions.GetInfoProxyByIndex(2);

        // third param is world, but it requires a specific world
        // if they're not on that world, it will fail
        // pass 0 and it will work on any world EXCEPT for the world the
        // current player is on
        if (contentId != 0)
            InviteToPartyContentIdNative(a1, contentId, 0);
    }

    internal void InviteInInstance(ulong contentId)
    {
        if (InviteToPartyInInstanceNative == null)
            return;

        // 6.11: 214A55
        var a1 = Plugin.Functions.GetInfoProxyByIndex(2);

        // third param is world, but it requires a specific world
        // if they're not on that world, it will fail
        // pass 0 and it will work on any world EXCEPT for the world the
        // current player is on
        if (contentId != 0)
            InviteToPartyInInstanceNative(a1, contentId);
    }

    internal void Kick(string name, ulong contentId)
    {
        if (KickNative == null)
            return;

        var agent = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.SocialPartyMember);
        if (agent == null)
            return;

        fixed (byte* namePtr = name.ToTerminatedBytes()) {
            KickNative(agent, namePtr, 0, contentId);
        }
    }

    internal void Promote(string name, ulong contentId)
    {
        if (PromoteNative == null)
            return;

        var agent = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.SocialPartyMember);
        if (agent == null)
            return;

        fixed (byte* namePtr = name.ToTerminatedBytes()) {
            PromoteNative(agent, namePtr, 0, contentId);
        }
    }
}
