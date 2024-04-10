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

    [Signature("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 83 F8 0F", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<IntPtr, uint, void> _searchForRecipesUsingItem = null!;

    [Signature("E8 ?? ?? ?? ?? EB 45 45 33 C9", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<void*, uint, byte, void> _searchForItem = null!;

    #region Offsets

    [Signature(
        "FF 90 ?? ?? ?? ?? 8B 93 ?? ?? ?? ?? 48 8B C8 E8 ?? ?? ?? ?? 41 0F B6 D4 48 8B CB E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 81 FF ?? ?? ?? ?? 0F 85",
        Offset = 2
    )]
    private readonly int? _searchForRecipesUsingItemVfunc;

    #endregion

    private Plugin Plugin { get; }

    internal Context(Plugin plugin) {
        Plugin = plugin;
        Plugin.GameInteropProvider.InitializeFromAttributes(this);
    }

    internal void InviteToNoviceNetwork(string name, ushort world) {
        if (_inviteToNoviceNetwork == null) {
            return;
        }

        // 6.3: 221EFD
        var a1 = Plugin.Functions.GetInfoProxyByIndex(0x14);

        fixed (byte* namePtr = name.ToTerminatedBytes()) {
            // can specify content id if we have it, but there's no need
            _inviteToNoviceNetwork(a1, 0, world, namePtr);
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
        if (_tryOn == null) {
            return;
        }

        _tryOn(0xFF, itemId, stainId, 0, 0);
    }

    internal void LinkItem(uint itemId) {
        if (_linkItem == null) {
            return;
        }

        var agent = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.ChatLog);
        _linkItem(agent, itemId);
    }

    internal void OpenItemComparison(uint itemId) {
        if (_itemComparison == null) {
            return;
        }

        var agent = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.ItemCompare);
        _itemComparison(agent, 0x4D, itemId, 0);
    }

    internal void SearchForRecipesUsingItem(uint itemId) {
        if (_searchForRecipesUsingItem == null || _searchForRecipesUsingItemVfunc is not { } offset) {
            return;
        }

        var uiModule = Framework.Instance()->GetUiModule();
        var vf = (delegate* unmanaged<UIModule*, IntPtr>) uiModule->vfunc[offset / 8];
        var a1 = vf(uiModule);
        _searchForRecipesUsingItem(a1, itemId);
    }

    internal void SearchForItem(uint itemId) {
        if (_searchForItem == null) {
            return;
        }

        var itemFinder = Framework.Instance()->GetUiModule()->GetItemFinderModule();
        _searchForItem(itemFinder, itemId, 1);
    }
}
