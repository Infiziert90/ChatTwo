using ChatTwo.Util;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Siggingway;

namespace ChatTwo.GameFunctions;

internal sealed unsafe class Party {
    [Signature("E8 ?? ?? ?? ?? 33 C0 EB 51", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<IntPtr, ulong, byte*, ushort, void> _inviteToParty = null!;

    [Signature("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 49 8B 56 20", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<AgentInterface*, byte*, ushort, ulong, void> _promote = null!;

    [Signature("E8 ?? ?? ?? ?? EB 66 49 8B 4E 20", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<AgentInterface*, byte*, ushort, ulong, void> _kick = null!;

    private Plugin Plugin { get; }

    internal Party(Plugin plugin) {
        this.Plugin = plugin;
        Siggingway.Siggingway.Initialise(this.Plugin.SigScanner, this);
    }

    internal void Invite(string name, ushort world) {
        if (this._inviteToParty == null || this.Plugin.Functions.Indexer == null) {
            return;
        }

        var uiModule = Framework.Instance()->GetUiModule();
        // 6.05: 20D722
        var func = (delegate*<UIModule*, IntPtr>) uiModule->vfunc[33];
        var toIndex = func(uiModule);
        var a1 = this.Plugin.Functions.Indexer(toIndex, 1);

        fixed (byte* namePtr = name.ToTerminatedBytes()) {
            // can specify content id if we have it, but there's no need
            this._inviteToParty(a1, 0, namePtr, world);
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
