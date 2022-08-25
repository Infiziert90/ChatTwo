using ChatTwo.Util;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace ChatTwo.GameFunctions;

internal sealed unsafe class Party {
    [Signature("E8 ?? ?? ?? ?? 33 C0 EB 51", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<IntPtr, ulong, byte*, ushort, byte> _inviteToParty = null!;

    [Signature("48 83 EC 38 41 B1 09", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<IntPtr, ulong, ushort, byte> _inviteToPartyContentId = null!;

    [Signature("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8B 83 ?? ?? ?? ?? 48 85 C0 74 62", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<IntPtr, ulong, byte> _inviteToPartyInInstance = null!;

    [Signature("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 49 8B 56 20", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<AgentInterface*, byte*, ushort, ulong, void> _promote = null!;

    [Signature("E8 ?? ?? ?? ?? EB 66 49 8B 4E 20", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<AgentInterface*, byte*, ushort, ulong, void> _kick = null!;

    private Plugin Plugin { get; }

    internal Party(Plugin plugin) {
        this.Plugin = plugin;
        SignatureHelper.Initialise(this);
    }

    internal void InviteSameWorld(string name, ushort world, ulong contentId) {
        if (this._inviteToParty == null) {
            return;
        }

        // 6.11: 214A55
        var a1 = this.Plugin.Functions.GetInfoProxyByIndex(2);
        fixed (byte* namePtr = name.ToTerminatedBytes()) {
            // this only works if target is on the same world
            this._inviteToParty(a1, contentId, namePtr, world);
        }
    }

    internal void InviteOtherWorld(ulong contentId) {
        if (this._inviteToPartyContentId == null) {
            return;
        }

        // 6.11: 214A55
        var a1 = this.Plugin.Functions.GetInfoProxyByIndex(2);
        if (contentId != 0) {
            // third param is world, but it requires a specific world
            // if they're not on that world, it will fail
            // pass 0 and it will work on any world EXCEPT for the world the
            // current player is on
            this._inviteToPartyContentId(a1, contentId, 0);
        }
    }

    internal void InviteInInstance(ulong contentId) {
        if (this._inviteToPartyInInstance == null) {
            return;
        }

        // 6.11: 214A55
        var a1 = this.Plugin.Functions.GetInfoProxyByIndex(2);
        if (contentId != 0) {
            // third param is world, but it requires a specific world
            // if they're not on that world, it will fail
            // pass 0 and it will work on any world EXCEPT for the world the
            // current player is on
            this._inviteToPartyInInstance(a1, contentId);
        }
    }

    internal void Kick(string name, ulong contentId) {
        if (this._kick == null) {
            return;
        }

        var agent = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.SocialPartyMember);
        if (agent == null) {
            return;
        }

        fixed (byte* namePtr = name.ToTerminatedBytes()) {
            this._kick(agent, namePtr, 0, contentId);
        }
    }

    internal void Promote(string name, ulong contentId) {
        if (this._promote == null) {
            return;
        }

        var agent = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.SocialPartyMember);
        if (agent == null) {
            return;
        }

        fixed (byte* namePtr = name.ToTerminatedBytes()) {
            this._promote(agent, namePtr, 0, contentId);
        }
    }
}
