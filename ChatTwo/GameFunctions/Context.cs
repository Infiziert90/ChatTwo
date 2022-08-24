using ChatTwo.Util;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace ChatTwo.GameFunctions;

internal sealed unsafe class Context {
    [Signature("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8B CB E8 ?? ?? ?? ?? 45 33 C9", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<IntPtr, ulong, ushort, byte*, byte> _inviteToNoviceNetwork = null!;

    [Signature("E8 ?? ?? ?? ?? EB 35 BA", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<uint, uint, ulong, uint, byte, byte> _tryOn = null!;

    [Signature("E8 ?? ?? ?? ?? EB 7B 49 8B 06", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<AgentInterface*, uint, void> _linkItem = null!;

    [Signature("E8 ?? ?? ?? ?? EB 3F 83 F8 FE", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<AgentInterface*, ushort, uint, byte, void> _itemComparison = null!;

    [Signature("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 41 B4 01", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<IntPtr, uint, void> _searchForRecipesUsingItem = null!;

    [Signature("E8 ?? ?? ?? ?? EB 45 45 33 C9", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<void*, uint, byte, void> _searchForItem = null!;

    #region Offsets

    [Signature(
        "FF 90 ?? ?? ?? ?? 8B 93 ?? ?? ?? ?? 48 8B C8 E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 41 B4 01",
        Offset = 2
    )]
    private readonly int? _searchForRecipesUsingItemVfunc;

    #endregion

    private Plugin Plugin { get; }

    internal Context(Plugin plugin) {
        this.Plugin = plugin;
        SignatureHelper.Initialise(this);
    }

    internal void InviteToNoviceNetwork(string name, ushort world) {
        if (this._inviteToNoviceNetwork == null) {
            return;
        }

        // 6.05: 20E4CB
        var a1 = this.Plugin.Functions.GetInfoProxyByIndex(0x11);

        fixed (byte* namePtr = name.ToTerminatedBytes()) {
            // can specify content id if we have it, but there's no need
            this._inviteToNoviceNetwork(a1, 0, world, namePtr);
        }
    }

    // These context menu things come from AgentChatLog.vf0 at the bottom
    // 0x10000: item comparison
    // 0x10001: try on
    // 0x10002: search for item
    // 0x10003: link
    // 0x10005: copy item name
    // 0x10006: search recipes using this material

    internal void TryOn(uint itemId, byte stainId) {
        if (this._tryOn == null) {
            return;
        }

        this._tryOn(0xFF, itemId, stainId, 0, 0);
    }

    internal void LinkItem(uint itemId) {
        if (this._linkItem == null) {
            return;
        }

        var agent = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.ChatLog);
        this._linkItem(agent, itemId);
    }

    internal void OpenItemComparison(uint itemId) {
        if (this._itemComparison == null) {
            return;
        }

        var agent = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.ItemCompare);
        this._itemComparison(agent, 0x4D, itemId, 0);
    }

    internal void SearchForRecipesUsingItem(uint itemId) {
        if (this._searchForRecipesUsingItem == null || this._searchForRecipesUsingItemVfunc is not { } offset) {
            return;
        }

        var uiModule = Framework.Instance()->GetUiModule();
        var vf = (delegate* unmanaged<UIModule*, IntPtr>) uiModule->vfunc[offset / 8];
        var a1 = vf(uiModule);
        this._searchForRecipesUsingItem(a1, itemId);
    }

    internal void SearchForItem(uint itemId) {
        if (this._searchForItem == null) {
            return;
        }

        var itemFinder = Framework.Instance()->GetUiModule()->GetItemFinderModule();
        this._searchForItem(itemFinder, itemId, 1);
    }
}
