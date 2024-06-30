using System.Globalization;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using Dalamud.Memory;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace ChatTwo.GameFunctions;

internal unsafe class GameFunctions : IDisposable
{
    #region Hooks
    private delegate nint ResolveTextCommandPlaceholderDelegate(nint a1, byte* placeholderText, byte a3, byte a4);

    [Signature("E8 ?? ?? ?? ?? 49 8D 4F 18 4C 8B E0", DetourName = nameof(ResolveTextCommandPlaceholderDetour))]
    private Hook<ResolveTextCommandPlaceholderDelegate>? ResolveTextCommandPlaceholderHook { get; init; }
    #endregion

    private Plugin Plugin { get; }
    internal Chat Chat { get; }

    internal GameFunctions(Plugin plugin)
    {
        Plugin = plugin;
        Chat = new Chat(Plugin);

        Plugin.GameInteropProvider.InitializeFromAttributes(this);

        ResolveTextCommandPlaceholderHook?.Enable();
    }

    public void Dispose()
    {
        Chat.Dispose();

        ResolveTextCommandPlaceholderHook?.Dispose();

        Marshal.FreeHGlobal(PlaceholderNamePtr);
    }

    internal nint GetInfoProxyByIndex(InfoProxyId proxyId)
    {
        var infoModule = InfoModule.Instance();
        if (infoModule == null)
            return nint.Zero;

        return (nint) infoModule->GetInfoProxyById(proxyId);
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
        Plugin.Common.SendMessage($"/{commandName} add {Placeholder}");
    }

    internal static void SetAddonInteractable(string name, bool interactable)
    {
        var unitManager = AtkStage.Instance()->RaptureAtkUnitManager;

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
        var unitManager = AtkStage.Instance()->RaptureAtkUnitManager;

        var addon = (nint) unitManager->GetAddonByName(name);
        if (addon == nint.Zero)
            return false;

        var flags = (uint*) (addon + 0x180);
        return (*flags & (1 << 22)) == 0;
    }

    internal static void OpenItemTooltip(uint id, ItemPayload.ItemKind itemKind)
    {
        var atkStage = AtkStage.Instance();
        var agent = AgentItemDetail.Instance();
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
        agent->AddonId = addon->Id;

        // vcall from E8 ?? ?? ?? ?? 0F B7 C0 48 83 C4 60 (FF 50 28 48 8B D3 48 8B CF)
        var vf5 = (delegate* unmanaged<AtkUnitBase*, byte, uint, void>*) ((nint) addon->VirtualTable + 40);
        // EA8BED: lets vf5 actually run
        *(byte*) ((nint) atkStage + 0x2B4) |= 2;
        (*vf5)(addon, 0, 15);
    }

    internal static void CloseItemTooltip()
    {
        // hide addon first to prevent the "addon close" sound
        var addon = AtkStage.Instance()->RaptureAtkUnitManager->GetAddonByName("ItemDetail");
        if (addon != null)
            addon->Hide(true, false, 0);

        var agent = AgentItemDetail.Instance();
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
        var lfg = AgentLookingForGroup.Instance();
        if (lfg->IsAgentActive())
        {
            var addonId = lfg->GetAddonId();
            var atkModule = RaptureAtkModule.Instance();
            var atkModuleVtbl = (void**) atkModule->AtkModule.VirtualTable;
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

    internal static bool IsMentor()
    {
        return PlayerState.Instance()->IsMentor();
    }

    internal static InfoProxyCommonList.CharacterData[] GetFriends()
    {
        ChatTwo.Plugin.Log.Information($"Address {(nint)InfoProxyFriendList.Instance():X}");
        ChatTwo.Plugin.Log.Information($"Address CharaData {(nint)InfoProxyFriendList.Instance()->CharData:X}");
        var list = InfoProxyFriendList.Instance()->CharDataSpan.ToArray();
        foreach (var data in list)
        {
            ChatTwo.Plugin.Log.Information($"Data was: {data.NameString} {data.HomeWorld} {data.ContentId}");
        }
        return list;
    }

    internal static void OpenQuestLog(Quest quest)
    {
        var splits = quest.Id.RawString.Split("_");
        if (splits.Length != 2)
        {
            Plugin.ChatGui.Print("QuestId is wrongly formatted");
            return;
        }

        if (!uint.TryParse(splits[1], NumberStyles.Any, CultureInfo.InvariantCulture,  out var questId))
        {
            Plugin.ChatGui.Print("Unable to parse quest id");
            return;
        }

        AgentQuestJournal.Instance()->OpenForQuest(questId, 1);
    }

    internal static void OpenPartyFinder(uint id)
    {
        AgentLookingForGroup.Instance()->OpenListing(id);
    }

    internal static void OpenAchievement(uint id)
    {
        AgentAchievement.Instance()->OpenById(id);
    }

    internal static bool IsInInstance()
    {
        return Plugin.Condition[ConditionFlag.BoundByDuty56];
    }

    internal static bool TryOpenAdventurerPlate(ulong playerId)
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

    internal static void ClickNoviceNetworkButton()
    {
        var agent = AgentChatLog.Instance();
        // case 3
        var value = new AtkValue { Type = ValueType.Int, Int = 3, };
        var result = 0;
        var vf0 = *(delegate* unmanaged<AgentChatLog*, int*, AtkValue*, ulong, ulong, int*>*) agent->VirtualTable;
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
