using System.Runtime.InteropServices;
using Dalamud.Hooking;
using Dalamud.Memory;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace ChatTwo.GameFunctions;

internal unsafe class GameFunctions : IDisposable {
    private static class Signatures {
        internal const string IsMentorA1 = "48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 84 C0 74 71 0F B6 86";
        internal const string ResolveTextCommandPlaceholder = "E8 ?? ?? ?? ?? 49 8D 4F 18 4C 8B E0";

        internal const string CurrentChatEntryOffset = "8B 77 ?? 8D 46 01 89 47 14 81 FE ?? ?? ?? ?? 72 03 FF 47";
    }

    #region Functions

    [Signature("E8 ?? ?? ?? ?? 8B FD 8B CD", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<IntPtr, uint, IntPtr> _getInfoProxyByIndex = null!;

    [Signature("E8 ?? ?? ?? ?? 84 C0 74 0D B0 02", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<IntPtr, byte> _isMentor = null!;

    [Signature("48 89 5C 24 ?? 57 48 83 EC 20 48 8B FA 48 8B D9 E8 ?? ?? ?? ?? 48 8B 8B ?? ?? ?? ?? 48 85 C9", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<AgentInterface*, ulong, byte> _openPartyFinder = null!;

    [Signature("E8 ?? ?? ?? ?? EB 20 48 8B 46 28", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<AgentInterface*, uint, void> _openAchievement = null!;

    [Signature("E8 ?? ?? ?? ?? 41 8D 4F 08 84 C0", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<byte> _inInstance = null!;

    #endregion

    #region Hooks

    private delegate IntPtr ResolveTextCommandPlaceholderDelegate(IntPtr a1, byte* placeholderText, byte a3, byte a4);

    [Signature(Signatures.ResolveTextCommandPlaceholder, DetourName = nameof(ResolveTextCommandPlaceholderDetour))]
    private Hook<ResolveTextCommandPlaceholderDelegate>? ResolveTextCommandPlaceholderHook { get; init; }

    #endregion

    #pragma warning disable 0649

    [Signature(Signatures.CurrentChatEntryOffset, Offset = 2)]
    private readonly byte? _currentChatEntryOffset;

    [Signature(Signatures.IsMentorA1, ScanType = ScanType.StaticAddress)]
    private readonly IntPtr? _isMentorA1;

    #pragma warning restore 0649

    [Signature("FF 90 ?? ?? ?? ?? 48 8B C8 BA ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B F0 48 85 C0 0F 84 ?? ?? ?? ?? 48 8B 10 33 ED", Offset = 2)]
    private readonly int? _infoModuleVfunc;

    private Plugin Plugin { get; }
    internal Party Party { get; }
    internal Chat Chat { get; }
    internal Context Context { get; }

    internal GameFunctions(Plugin plugin) {
        this.Plugin = plugin;
        this.Party = new Party(this.Plugin);
        this.Chat = new Chat(this.Plugin);
        this.Context = new Context(this.Plugin);

        Plugin.GameInteropProvider.InitializeFromAttributes(this);

        this.ResolveTextCommandPlaceholderHook?.Enable();
    }

    public void Dispose() {
        this.Chat.Dispose();

        this.ResolveTextCommandPlaceholderHook?.Dispose();

        Marshal.FreeHGlobal(this._placeholderNamePtr);
    }

    private IntPtr GetInfoModule() {
        if (this._infoModuleVfunc is not { } vfunc) {
            return IntPtr.Zero;
        }

        var uiModule = Framework.Instance()->GetUiModule();
        var getInfoModule = (delegate* unmanaged<UIModule*, IntPtr>) uiModule->vfunc[vfunc / 8];
        return getInfoModule(uiModule);
    }

    internal IntPtr GetInfoProxyByIndex(uint idx) {
        var infoModule = this.GetInfoModule();
        return infoModule == IntPtr.Zero ? IntPtr.Zero : this._getInfoProxyByIndex(infoModule, idx);
    }

    internal uint? GetCurrentChatLogEntryIndex() {
        if (this._currentChatEntryOffset == null) {
            return null;
        }

        var log = (IntPtr) Framework.Instance()->GetUiModule()->GetRaptureLogModule();
        return *(uint*) (log + this._currentChatEntryOffset.Value);
    }

    internal void SendFriendRequest(string name, ushort world) {
        this.ListCommand(name, world, "friendlist");
    }

    internal void AddToBlacklist(string name, ushort world) {
        this.ListCommand(name, world, "blist");
    }

    private void ListCommand(string name, ushort world, string commandName) {
        var row = Plugin.DataManager.GetExcelSheet<World>()!.GetRow(world);
        if (row == null) {
            return;
        }

        var worldName = row.Name.RawString;
        this._replacementName = $"{name}@{worldName}";
        this.Plugin.Common.Functions.Chat.SendMessage($"/{commandName} add {this._placeholder}");
    }

    internal static void SetAddonInteractable(string name, bool interactable) {
        var unitManager = AtkStage.GetSingleton()->RaptureAtkUnitManager;

        var addon = (IntPtr) unitManager->GetAddonByName(name);
        if (addon == IntPtr.Zero) {
            return;
        }

        var flags = (uint*) (addon + 0x180);
        if (interactable) {
            *flags &= ~(1u << 22);
        } else {
            *flags |= 1 << 22;
        }
    }

    internal static void SetChatInteractable(bool interactable) {
        for (var i = 0; i < 4; i++) {
            SetAddonInteractable($"ChatLogPanel_{i}", interactable);
        }

        SetAddonInteractable("ChatLog", interactable);
    }

    internal static bool IsAddonInteractable(string name) {
        var unitManager = AtkStage.GetSingleton()->RaptureAtkUnitManager;

        var addon = (IntPtr) unitManager->GetAddonByName(name);
        if (addon == IntPtr.Zero) {
            return false;
        }

        var flags = (uint*) (addon + 0x180);
        return (*flags & (1 << 22)) == 0;
    }

    internal static void OpenItemTooltip(uint id) {
        var atkStage = AtkStage.GetSingleton();
        var agent = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.ItemDetail);
        var addon = atkStage->RaptureAtkUnitManager->GetAddonByName("ItemDetail");
        if (agent == null || addon == null) {
            // atkStage ain't gonna be null or we have bigger problems
            return;
        }

        var agentPtr = (IntPtr) agent;

        // addresses mentioned here are 6.11
        // see the call near the end of AgentItemDetail.Update
        // offsets valid as of 6.11

        // A54B19: sets some shit
        *(uint*) (agentPtr + 0x20) = 22;
        // A55218: switch goes down to default, which is what we want
        *(byte*) (agentPtr + 0x118) = 1;
        // A54BE0: item id when hovering over item in chat
        *(uint*) (agentPtr + 0x11C) = id;
        // A54BCC: always 0 when hovering over item in chat
        *(uint*) (agentPtr + 0x120) = 0;
        // A558A5: skips a check to do with inventory
        *(byte*) (agentPtr + 0x128) &= 0xEF;
        // A54B3F: when set to 1, lets everything continue (one frame)
        *(byte*) (agentPtr + 0x14A) = 1;
        // A54B59: skips early return
        *(byte*) (agentPtr + 0x14E) = 0;

        // this just probably needs to be set
        agent->AddonId = addon->ID;

        // vcall from E8 ?? ?? ?? ?? 0F B7 C0 48 83 C4 60 (FF 50 28 48 8B D3 48 8B CF)
        var vf5 = (delegate* unmanaged<AtkUnitBase*, byte, uint, void>*) ((IntPtr) addon->VTable + 40);
        // EA8BED: lets vf5 actually run
        *(byte*) ((IntPtr) atkStage + 0x2B4) |= 2;
        (*vf5)(addon, 0, 15);
    }

    internal static void CloseItemTooltip() {
        // hide addon first to prevent the "addon close" sound
        var addon = AtkStage.GetSingleton()->RaptureAtkUnitManager->GetAddonByName("ItemDetail");
        if (addon != null) {
            addon->Hide(true, false, 0);
        }

        var agent = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.ItemDetail);
        if (agent != null) {
            agent->Hide();
        }
    }

    internal static void OpenPartyFinder() {
        // this whole method: 6.05: 84433A (FF 97 ?? ?? ?? ?? 41 B4 01)
        var lfg = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.LookingForGroup);
        if (lfg->IsAgentActive()) {
            var addonId = lfg->GetAddonID();
            var atkModule = Framework.Instance()->GetUiModule()->GetRaptureAtkModule();
            var atkModuleVtbl = (void**) atkModule->AtkModule.vtbl;
            var vf27 = (delegate* unmanaged<RaptureAtkModule*, ulong, ulong, byte>) atkModuleVtbl[27];
            vf27(atkModule, addonId, 1);
        } else {
            // 6.05: 8443DD
            if (*(uint*) ((IntPtr) lfg + 0x2C20) > 0) {
                lfg->Hide();
            } else {
                lfg->Show();
            }
        }
    }

    internal bool IsMentor() {
        if (this._isMentor == null || this._isMentorA1 == null || this._isMentorA1.Value == IntPtr.Zero) {
            return false;
        }

        return this._isMentor(this._isMentorA1.Value) > 0;
    }

    internal void OpenPartyFinder(uint id) {
        if (this._openPartyFinder == null) {
            return;
        }

        var agent = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.LookingForGroup);
        if (agent != null) {
            this._openPartyFinder(agent, id);
        }
    }

    internal void OpenAchievement(uint id) {
        if (this._openAchievement == null) {
            return;
        }

        var agent = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.Achievement);
        if (agent != null) {
            this._openAchievement(agent, id);
        }
    }

    internal bool IsInInstance() {
        if (this._inInstance == null) {
            return false;
        }

        return this._inInstance() != 0;
    }

    internal void ClickNoviceNetworkButton() {
        var agent = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.ChatLog);
        // case 3
        var value = new AtkValue {
            Type = ValueType.Int,
            Int = 3,
        };
        var result = 0;
        var vf0 = *(delegate* unmanaged<AgentInterface*, int*, AtkValue*, ulong, ulong, int*>*) agent->VTable;
        vf0(agent, &result, &value, 0, 0);
    }

    private readonly IntPtr _placeholderNamePtr = Marshal.AllocHGlobal(128);
    private readonly string _placeholder = $"<{Guid.NewGuid():N}>";
    private string? _replacementName;

    private IntPtr ResolveTextCommandPlaceholderDetour(IntPtr a1, byte* placeholderText, byte a3, byte a4) {
        if (this._replacementName == null) {
            goto Original;
        }

        var placeholder = MemoryHelper.ReadStringNullTerminated((IntPtr) placeholderText);
        if (placeholder != this._placeholder) {
            goto Original;
        }

        MemoryHelper.WriteString(this._placeholderNamePtr, this._replacementName);
        this._replacementName = null;

        return this._placeholderNamePtr;

        Original:
        return this.ResolveTextCommandPlaceholderHook!.Original(a1, placeholderText, a3, a4);
    }
}
