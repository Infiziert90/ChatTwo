using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using Dalamud.Memory;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace ChatTwo.GameFunctions;

internal unsafe class GameFunctions : IDisposable
{
    #region Functions
    // TODO: Can be replaced with CS version soon
    [Signature("48 89 5C 24 ?? 57 48 83 EC 20 48 8B FA 48 8B D9 E8 ?? ?? ?? ?? 48 8B 8B ?? ?? ?? ?? 48 85 C9", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<AgentInterface*, ulong, byte> OpenPartyFinderNative = null!;

    // TODO: Can be replaced with CS version soon
    [Signature("E8 ?? ?? ?? ?? EB 20 48 8B 46 28", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<AgentInterface*, uint, void> OpenAchievementNative = null!;
    #endregion

    #region Hooks
    private delegate nint ResolveTextCommandPlaceholderDelegate(nint a1, byte* placeholderText, byte a3, byte a4);

    [Signature("E8 ?? ?? ?? ?? 49 8D 4F 18 4C 8B E0", DetourName = nameof(ResolveTextCommandPlaceholderDetour))]
    private Hook<ResolveTextCommandPlaceholderDelegate>? ResolveTextCommandPlaceholderHook { get; init; }
    #endregion

    private Plugin Plugin { get; }
    internal Party Party { get; }
    internal Chat Chat { get; }
    internal Context Context { get; }

    internal GameFunctions(Plugin plugin)
    {
        Plugin = plugin;
        Party = new Party(Plugin);
        Chat = new Chat(Plugin);
        Context = new Context(Plugin);

        Plugin.GameInteropProvider.InitializeFromAttributes(this);

        ResolveTextCommandPlaceholderHook?.Enable();
    }

    public void Dispose()
    {
        Chat.Dispose();

        ResolveTextCommandPlaceholderHook?.Dispose();

        Marshal.FreeHGlobal(PlaceholderNamePtr);
    }

    internal nint GetInfoProxyByIndex(uint idx)
    {
        var infoModule = InfoModule.Instance();
        if (infoModule == null)
            return nint.Zero;

        return (nint) infoModule->GetInfoProxyById(idx);
    }

    internal int GetCurrentChatLogEntryIndex()
    {
        return Framework.Instance()->GetUiModule()->GetRaptureLogModule()->LogModule.LogMessageCount;
    }

    internal void SendFriendRequest(string name, ushort world)
    {
        ListCommand(name, world, "friendlist");
    }

    internal void AddToBlacklist(string name, ushort world)
    {
        ListCommand(name, world, "blist");
    }

    private void ListCommand(string name, ushort world, string commandName)
    {
        var row = Plugin.DataManager.GetExcelSheet<World>()!.GetRow(world);
        if (row == null)
            return;

        var worldName = row.Name.RawString;
        ReplacementName = $"{name}@{worldName}";
        Plugin.Common.Functions.Chat.SendMessage($"/{commandName} add {Placeholder}");
    }

    internal static void SetAddonInteractable(string name, bool interactable)
    {
        var unitManager = AtkStage.GetSingleton()->RaptureAtkUnitManager;

        var addon = (nint) unitManager->GetAddonByName(name);
        if (addon == nint.Zero)
            return;

        var flags = (uint*) (addon + 0x180);
        if (interactable)
            *flags &= ~(1u << 22);
        else
            *flags |= 1 << 22;
    }

    internal static void SetChatInteractable(bool interactable)
    {
        for (var i = 0; i < 4; i++)
            SetAddonInteractable($"ChatLogPanel_{i}", interactable);

        SetAddonInteractable("ChatLog", interactable);
    }

    internal static bool IsAddonInteractable(string name)
    {
        var unitManager = AtkStage.GetSingleton()->RaptureAtkUnitManager;

        var addon = (nint) unitManager->GetAddonByName(name);
        if (addon == nint.Zero)
            return false;

        var flags = (uint*) (addon + 0x180);
        return (*flags & (1 << 22)) == 0;
    }

    internal static void OpenItemTooltip(uint id, ItemPayload.ItemKind itemKind)
    {
        var atkStage = AtkStage.GetSingleton();
        var agent = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.ItemDetail);
        var addon = atkStage->RaptureAtkUnitManager->GetAddonByName("ItemDetail");

        // atkStage ain't gonna be null or we have bigger problems
        if (agent == null || addon == null)
            return;

        var agentPtr = (nint) agent;
        // addresses mentioned here are 6.11
        // see the call near the end of AgentItemDetail.Update
        // offsets valid as of 6.11

        // A54B19: sets some shit
        *(uint*) (agentPtr + 0x20) = 22;
        // A55218: switch goes down to default, which is what we want
        *(byte*) (agentPtr + 0x118) = itemKind == ItemPayload.ItemKind.EventItem ? (byte)8 : (byte)1;
        // A54BE0: item id when hovering over item in chat
        *(uint*) (agentPtr + 0x11C) = id;
        // A54BCC: always 0 when hovering over item in chat
        *(uint*) (agentPtr + 0x120) = 0;
        // A558A5: skips a check to do with inventory
        *(byte*) (agentPtr + 0x128) &= 0xEF;
        // Is also set to the ID of the item when in chat
        *(uint*) (agentPtr + 0x138) = id;
        // A54B3F: when set to 1, lets everything continue (one frame)
        *(byte*) (agentPtr + 0x14A) = 1;
        // A54B59: skips early return
        *(byte*) (agentPtr + 0x14E) = 0;

        // this just probably needs to be set
        agent->AddonId = addon->ID;

        // vcall from E8 ?? ?? ?? ?? 0F B7 C0 48 83 C4 60 (FF 50 28 48 8B D3 48 8B CF)
        var vf5 = (delegate* unmanaged<AtkUnitBase*, byte, uint, void>*) ((nint) addon->VTable + 40);
        // EA8BED: lets vf5 actually run
        *(byte*) ((nint) atkStage + 0x2B4) |= 2;
        (*vf5)(addon, 0, 15);
    }

    internal static void CloseItemTooltip()
    {
        // hide addon first to prevent the "addon close" sound
        var addon = AtkStage.GetSingleton()->RaptureAtkUnitManager->GetAddonByName("ItemDetail");
        if (addon != null)
            addon->Hide(true, false, 0);

        var agent = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.ItemDetail);
        if (agent != null)
        {
            var eventData = stackalloc AtkValue[1];
            var atkValues = stackalloc AtkValue[1];
            atkValues->Type = ValueType.Int;
            atkValues->Int = -1;
            agent->ReceiveEvent(eventData, atkValues, 1, 1);
        }
    }

    internal static void OpenPartyFinder()
    {
        // this whole method: 6.05: 84433A (FF 97 ?? ?? ?? ?? 41 B4 01)
        var lfg = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.LookingForGroup);
        if (lfg->IsAgentActive())
        {
            var addonId = lfg->GetAddonID();
            var atkModule = Framework.Instance()->GetUiModule()->GetRaptureAtkModule();
            var atkModuleVtbl = (void**) atkModule->AtkModule.vtbl;
            var vf27 = (delegate* unmanaged<RaptureAtkModule*, ulong, ulong, byte>) atkModuleVtbl[27];
            vf27(atkModule, addonId, 1);
        }
        else
        {
            // 6.05: 8443DD
            if (*(uint*) ((nint) lfg + 0x2C20) > 0)
                lfg->Hide();
            else
                lfg->Show();
        }
    }

    internal bool IsMentor()
    {
        return PlayerState.Instance()->IsMentor();
    }

    internal void OpenPartyFinder(uint id)
    {
        if (OpenPartyFinderNative == null)
            return;

        var agent = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.LookingForGroup);
        if (agent != null)
            OpenPartyFinderNative(agent, id);
    }

    internal void OpenAchievement(uint id)
    {
        if (OpenAchievementNative == null)
            return;

        var agent = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.Achievement);
        if (agent != null)
            OpenAchievementNative(agent, id);
    }

    internal bool IsInInstance()
    {
        return Plugin.Condition[ConditionFlag.BoundByDuty56];
    }

    internal bool TryOpenAdventurerPlate(ulong playerId)
    {
        try
        {
            AgentCharaCard.Instance()->OpenCharaCard(playerId);
            return true;
        }
        catch (Exception e)
        {
            Plugin.Log.Warning(e, "Unable to open adventurer plate");
            return false;
        }
    }

    internal void ClickNoviceNetworkButton()
    {
        var agent = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.ChatLog);
        // case 3
        var value = new AtkValue { Type = ValueType.Int, Int = 3, };
        var result = 0;
        var vf0 = *(delegate* unmanaged<AgentInterface*, int*, AtkValue*, ulong, ulong, int*>*) agent->VTable;
        vf0(agent, &result, &value, 0, 0);
    }

    private readonly nint PlaceholderNamePtr = Marshal.AllocHGlobal(128);
    private readonly string Placeholder = $"<{Guid.NewGuid():N}>";
    private string? ReplacementName;

    private nint ResolveTextCommandPlaceholderDetour(nint a1, byte* placeholderText, byte a3, byte a4)
    {
        var placeholder = MemoryHelper.ReadStringNullTerminated((nint) placeholderText);
        if (ReplacementName == null || placeholder != Placeholder)
            return ResolveTextCommandPlaceholderHook!.Original(a1, placeholderText, a3, a4);

        MemoryHelper.WriteString(PlaceholderNamePtr, ReplacementName);
        ReplacementName = null;

        return PlaceholderNamePtr;
    }
}
