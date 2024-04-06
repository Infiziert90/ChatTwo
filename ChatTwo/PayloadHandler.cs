using System.Numerics;
using ChatTwo.Code;
using ChatTwo.Resources;
using ChatTwo.Ui;
using ChatTwo.Util;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Interface.Utility;
using Dalamud.Utility;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Action = System.Action;
using DalamudPartyFinderPayload = Dalamud.Game.Text.SeStringHandling.Payloads.PartyFinderPayload;
using ChatTwoPartyFinderPayload = ChatTwo.Util.PartyFinderPayload;

namespace ChatTwo;

internal sealed class PayloadHandler {
    private const string PopupId = "chat2-context-popup";

    private PluginUi Ui { get; }
    private ChatLog Log { get; }

    private (Chunk, Payload?)? Popup { get; set; }

    private bool _handleTooltips;
    private uint _hoveredItem;
    private uint _hoverCounter;
    private uint _lastHoverCounter;

    internal PayloadHandler(PluginUi ui, ChatLog log) {
        Ui = ui;
        Log = log;
    }

    internal void Draw() {
        DrawPopups();

        if (_handleTooltips && ++_hoverCounter - _lastHoverCounter > 1) {
            GameFunctions.GameFunctions.CloseItemTooltip();
            _hoveredItem = 0;
            _hoverCounter = _lastHoverCounter = 0;
            _handleTooltips = false;
        }
    }

    private void DrawPopups() {
        if (Popup == null) {
            return;
        }

        var (chunk, payload) = Popup.Value;

        if (!ImGui.BeginPopup(PopupId)) {
            Popup = null;
            return;
        }

        ImGui.PushID(PopupId);

        var drawn = false;
        switch (payload) {
            case PlayerPayload player: {
                DrawPlayerPopup(chunk, player);
                drawn = true;
                break;
            }
            case ItemPayload item: {
                DrawItemPopup(item);
                drawn = true;
                break;
            }
        }

        ContextFooter(drawn, chunk);
        Integrations(chunk, payload);

        ImGui.PopID();
        ImGui.EndPopup();
    }

    private void Integrations(Chunk chunk, Payload? payload) {
        var registered = Ui.Plugin.Ipc.Registered;
        if (registered.Count == 0) {
            return;
        }

        var contentId = chunk.Message?.ContentId ?? 0;
        var sender = chunk.Message?.Sender
            .Select(chunk => chunk.Link)
            .FirstOrDefault(chunk => chunk is PlayerPayload) as PlayerPayload;

        if (ImGui.BeginMenu(Language.Context_Integrations)) {
            var cursor = ImGui.GetCursorPos();

            foreach (var id in registered) {
                try {
                    Ui.Plugin.Ipc.Invoke(id, sender, contentId, payload, chunk.Message?.SenderSource, chunk.Message?.ContentSource);
                } catch (Exception ex) {
                    Plugin.Log.Error(ex, "Error executing integration");
                }
            }

            if (cursor == ImGui.GetCursorPos()) {
                ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int) ImGuiCol.TextDisabled]);
                ImGui.Text("No integrations available");
                ImGui.PopStyleColor();
            }

            ImGui.EndMenu();
        }
    }

    private void ContextFooter(bool separator, Chunk chunk) {
        if (separator) {
            ImGui.Separator();
        }

        if (!ImGui.BeginMenu(Plugin.PluginName)) {
            return;
        }

        ImGui.Checkbox(Language.Context_ScreenshotMode, ref Ui.ScreenshotMode);

        if (ImGui.Selectable(Language.Context_HideChat)) {
            Log.UserHide();
        }

        if (chunk.Message is { } message) {
            if (ImGui.BeginMenu(Language.Context_Copy)) {
                var text = message.Sender
                    .Concat(message.Content)
                    .Where(chunk => chunk is TextChunk)
                    .Cast<TextChunk>()
                    .Select(text => text.Content)
                    .Aggregate(string.Concat);
                ImGui.InputTextMultiline(
                    "##chat2-copy",
                    ref text,
                    (uint) text.Length,
                    new Vector2(350, 100) * ImGuiHelpers.GlobalScale,
                    ImGuiInputTextFlags.ReadOnly
                );
                ImGui.EndMenu();
            }

            var col = ImGui.GetStyle().Colors[(int) ImGuiCol.TextDisabled];
            ImGui.PushStyleColor(ImGuiCol.Text, col);
            try
            {
                ImGui.TextUnformatted(message.Code.Type.Name());
            }
            finally
            {
                ImGui.PopStyleColor();
            }
        }

        ImGui.EndMenu();
    }

    internal void Click(Chunk chunk, Payload? payload, ImGuiMouseButton button) {
        switch (button) {
            case ImGuiMouseButton.Left:
                LeftClickPayload(chunk, payload);
                break;
            case ImGuiMouseButton.Right:
                RightClickPayload(chunk, payload);
                break;
        }
    }

    internal void Hover(Payload payload) {
        var hoverSize = 250f * ImGuiHelpers.GlobalScale;

        switch (payload) {
            case StatusPayload status: {
                DoHover(() => HoverStatus(status), hoverSize);
                break;
            }
            case ItemPayload item: {
                if (Ui.Plugin.Config.NativeItemTooltips) {
                    GameFunctions.GameFunctions.OpenItemTooltip(item.RawItemId);

                    _handleTooltips = true;
                    if (_hoveredItem != item.RawItemId) {
                        _hoveredItem = item.RawItemId;
                        _hoverCounter = _lastHoverCounter = 0;
                    } else {
                        _lastHoverCounter = _hoverCounter;
                    }

                    break;
                }

                DoHover(() => HoverItem(item), hoverSize);
                break;
            }
        }
    }

    private void DoHover(Action inside, float width) {
        ImGui.SetNextWindowSize(new Vector2(width, -1f));

        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos();
        ImGui.PushStyleColor(ImGuiCol.Text, Ui.DefaultText);

        try
        {
            inside();
        }
        finally
        {
            ImGui.PopStyleColor();
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
    }

    private static void InlineIcon(IDalamudTextureWrap icon) {
        var lineHeight = ImGui.CalcTextSize("A").Y;

        var cursor = ImGui.GetCursorPos();
        var size = new Vector2(icon.Width, icon.Height) * ImGuiHelpers.GlobalScale;
        ImGui.Image(icon.ImGuiHandle, size);
        ImGui.SameLine();
        ImGui.SetCursorPos(cursor + new Vector2(size.X + 4, size.Y / 2 - lineHeight / 2));
    }

    private void HoverStatus(StatusPayload status) {
        if (Ui.Plugin.TextureCache.GetStatus(status.Status) is { } icon) {
            InlineIcon(icon);
        }

        var name = ChunkUtil.ToChunks(status.Status.Name.ToDalamudString(), ChunkSource.None, null);
        Log.DrawChunks(name.ToList());
        ImGui.Separator();

        var desc = ChunkUtil.ToChunks(status.Status.Description.ToDalamudString(), ChunkSource.None, null);
        Log.DrawChunks(desc.ToList());
    }

    private void HoverItem(ItemPayload item) {
        if (item.Kind == ItemPayload.ItemKind.EventItem) {
            HoverEventItem(item);
            return;
        }

        if (item.Item == null) {
            return;
        }

        if (Ui.Plugin.TextureCache.GetItem(item.Item, item.IsHQ) is { } icon) {
            InlineIcon(icon);
        }

        var name = ChunkUtil.ToChunks(item.Item.Name.ToDalamudString(), ChunkSource.None, null);
        Log.DrawChunks(name.ToList());
        ImGui.Separator();

        var desc = ChunkUtil.ToChunks(item.Item.Description.ToDalamudString(), ChunkSource.None, null);
        Log.DrawChunks(desc.ToList());
    }

    private void HoverEventItem(ItemPayload payload) {
        var item = Plugin.DataManager.GetExcelSheet<EventItem>()?.GetRow(payload.RawItemId);
        if (item == null) {
            return;
        }

        if (Ui.Plugin.TextureCache.GetEventItem(item) is { } icon) {
            InlineIcon(icon);
        }

        var name = ChunkUtil.ToChunks(item.Name.ToDalamudString(), ChunkSource.None, null);
        Log.DrawChunks(name.ToList());
        ImGui.Separator();

        var help = Plugin.DataManager.GetExcelSheet<EventItemHelp>()?.GetRow(payload.RawItemId);
        if (help != null) {
            var desc = ChunkUtil.ToChunks(help.Description.ToDalamudString(), ChunkSource.None, null);
            Log.DrawChunks(desc.ToList());
        }
    }

    private void LeftClickPayload(Chunk chunk, Payload? payload) {
        switch (payload) {
            case MapLinkPayload map: {
                Plugin.GameGui.OpenMapWithMapLink(map);
                break;
            }
            case QuestPayload quest: {
                Ui.Plugin.Common.Functions.Journal.OpenQuest(quest.Quest);
                break;
            }
            case DalamudLinkPayload link: {
                ClickLinkPayload(chunk, payload, link);
                break;
            }
            case DalamudPartyFinderPayload pf: {
                if (pf.LinkType == DalamudPartyFinderPayload.PartyFinderLinkType.PartyFinderNotification) {
                    GameFunctions.GameFunctions.OpenPartyFinder();
                } else {
                    Ui.Plugin.Functions.OpenPartyFinder(pf.ListingId);
                }

                break;
            }
            case ChatTwoPartyFinderPayload pf: {
                Ui.Plugin.Functions.OpenPartyFinder(pf.Id);
                break;
            }
            case AchievementPayload achievement: {
                Ui.Plugin.Functions.OpenAchievement(achievement.Id);
                break;
            }
            case RawPayload raw: {
                if (Equals(raw, ChunkUtil.PeriodicRecruitmentLink)) {
                    GameFunctions.GameFunctions.OpenPartyFinder();
                }

                break;
            }
        }
    }

    private void ClickLinkPayload(Chunk chunk, Payload payload, DalamudLinkPayload link) {
        if (chunk.GetSeString() is not { } source) {
            return;
        }

        var start = source.Payloads.IndexOf(payload);
        var end = source.Payloads.IndexOf(RawPayload.LinkTerminator, start == -1 ? 0 : start);
        if (start == -1 || end == -1) {
            return;
        }

        var payloads = source.Payloads.Skip(start).Take(end - start + 1).ToList();
        if (!Plugin.ChatGui.RegisteredLinkHandlers.TryGetValue((link.Plugin, link.CommandId), out var value))
        {
            Plugin.Log.Warning("Could not find DalamudLinkHandlers");
            return;
        }

        try
        {
            // Running XivCommon SendChat instantly leads to a game freeze, for whatever reason
            Plugin.Framework.RunOnTick(() => value.Invoke(link.CommandId, new SeString(payloads)));
        } catch (Exception ex) {
            Plugin.Log.Error(ex, "Error executing DalamudLinkPayload handler");
        }
    }

    private void RightClickPayload(Chunk chunk, Payload? payload) {
        Popup = (chunk, payload);
        ImGui.OpenPopup(PopupId);
    }

    private void DrawItemPopup(ItemPayload payload) {
        if (payload.Kind == ItemPayload.ItemKind.EventItem) {
            DrawEventItemPopup(payload);
            return;
        }

        var item = Plugin.DataManager.GetExcelSheet<Item>()?.GetRow(payload.ItemId);
        if (item == null) {
            return;
        }

        var hq = payload.Kind == ItemPayload.ItemKind.Hq;

        if (Ui.Plugin.TextureCache.GetItem(item, hq) is { } icon) {
            InlineIcon(icon);
        }

        var name = item.Name.ToDalamudString();
        if (hq) {
            // hq symbol
            name.Payloads.Add(new TextPayload(" "));
        } else if (payload.Kind == ItemPayload.ItemKind.Collectible) {
            name.Payloads.Add(new TextPayload(" "));
        }

        Log.DrawChunks(ChunkUtil.ToChunks(name, ChunkSource.None, null).ToList(), false);
        ImGui.Separator();

        var realItemId = payload.RawItemId;
        if (item.EquipSlotCategory.Row != 0) {
            if (ImGui.Selectable(Language.Context_TryOn)) {
                Ui.Plugin.Functions.Context.TryOn(realItemId, 0);
            }

            if (ImGui.Selectable(Language.Context_ItemComparison)) {
                Ui.Plugin.Functions.Context.OpenItemComparison(realItemId);
            }
        }

        if (item.ItemSearchCategory.Value?.Category == 3) {
            if (ImGui.Selectable(Language.Context_SearchRecipes)) {
                Ui.Plugin.Functions.Context.SearchForRecipesUsingItem(payload.ItemId);
            }
        }

        if (ImGui.Selectable(Language.Context_SearchForItem)) {
            Ui.Plugin.Functions.Context.SearchForItem(realItemId);
        }

        if (ImGui.Selectable(Language.Context_Link)) {
            Ui.Plugin.Functions.Context.LinkItem(realItemId);
        }

        if (ImGui.Selectable(Language.Context_CopyItemName)) {
            ImGui.SetClipboardText(name.TextValue);
        }
    }

    private void DrawEventItemPopup(ItemPayload payload) {
        if (payload.Kind != ItemPayload.ItemKind.EventItem) {
            return;
        }

        var item = Plugin.DataManager.GetExcelSheet<EventItem>()?.GetRow(payload.ItemId);
        if (item == null) {
            return;
        }

        if (Ui.Plugin.TextureCache.GetEventItem(item) is { } icon) {
            InlineIcon(icon);
        }

        var name = item.Name.ToDalamudString();
        Log.DrawChunks(ChunkUtil.ToChunks(name, ChunkSource.None, null).ToList(), false);
        ImGui.Separator();

        var realItemId = payload.RawItemId;
        if (ImGui.Selectable(Language.Context_Link)) {
            Ui.Plugin.Functions.Context.LinkItem(realItemId);
        }

        if (ImGui.Selectable(Language.Context_CopyItemName)) {
            ImGui.SetClipboardText(name.TextValue);
        }
    }

    private void DrawPlayerPopup(Chunk chunk, PlayerPayload player)
    {
        // Possible that GMs return a null payload
        if (player == null)
            return;

        var world = player.World;
        if (chunk.Message?.Code.Type == ChatType.FreeCompanyLoginLogout) {
            if (Plugin.ClientState.LocalPlayer?.HomeWorld.GameData is { } homeWorld) {
                world = homeWorld;
            }
        }

        var name = new List<Chunk> { new TextChunk(ChunkSource.None, null, player.PlayerName) };
        if (world.IsPublic) {
            name.AddRange(new Chunk[] {
                new IconChunk(ChunkSource.None, null, BitmapFontIcon.CrossWorld),
                new TextChunk(ChunkSource.None, null, world.Name),
            });
        }

        Log.DrawChunks(name, false);
        ImGui.Separator();

        if (ImGui.Selectable(Language.Context_SendTell)) {
            Log.Chat = $"/tell {player.PlayerName}";
            if (world.IsPublic) {
                Log.Chat += $"@{world.Name}";
            }

            Log.Chat += " ";
            Log.Activate = true;
        }

        var validContentId = chunk.Message?.ContentId is not (null or 0);
        if (world.IsPublic) {
            var party = Plugin.PartyList;
            var leader = (ulong?) party[(int) party.PartyLeaderIndex]?.ContentId;
            var isLeader = party.Length == 0 || Plugin.ClientState.LocalContentId == leader;
            var member = party.FirstOrDefault(member => member.Name.TextValue == player.PlayerName && member.World.Id == world.RowId);
            var isInParty = member != default;
            var inInstance = Ui.Plugin.Functions.IsInInstance();
            var inPartyInstance = Plugin.DataManager.GetExcelSheet<TerritoryType>()!.GetRow(Plugin.ClientState.TerritoryType)?.TerritoryIntendedUse is (41 or 47 or 48 or 52 or 53);
            if (isLeader) {
                if (!isInParty) {
                    if (inInstance && inPartyInstance) {
                        if (validContentId && ImGui.Selectable(Language.Context_InviteToParty)) {
                            Ui.Plugin.Functions.Party.InviteInInstance(chunk.Message!.ContentId);
                        }
                    } else if (!inInstance && ImGui.BeginMenu(Language.Context_InviteToParty)) {
                        if (ImGui.Selectable(Language.Context_InviteToParty_SameWorld)) {
                            Ui.Plugin.Functions.Party.InviteSameWorld(player.PlayerName, (ushort) world.RowId, chunk.Message?.ContentId ?? 0);
                        }

                        if (validContentId && ImGui.Selectable(Language.Context_InviteToParty_DifferentWorld)) {
                            Ui.Plugin.Functions.Party.InviteOtherWorld(chunk.Message!.ContentId);
                        }

                        ImGui.EndMenu();
                    }
                }

                if (isInParty && member != null && (!inInstance || (inInstance && inPartyInstance))) {
                    if (ImGui.Selectable(Language.Context_Promote)) {
                        Ui.Plugin.Functions.Party.Promote(player.PlayerName, (ulong) member.ContentId);
                    }

                    if (ImGui.Selectable(Language.Context_KickFromParty)) {
                        Ui.Plugin.Functions.Party.Kick(player.PlayerName, (ulong) member.ContentId);
                    }
                }
            }

            var isFriend = Ui.Plugin.Common.Functions.FriendList.List.Any(friend => friend.Name.TextValue == player.PlayerName && friend.HomeWorld == world.RowId);
            if (!isFriend && ImGui.Selectable(Language.Context_SendFriendRequest)) {
                Ui.Plugin.Functions.SendFriendRequest(player.PlayerName, (ushort) world.RowId);
            }

            if (ImGui.Selectable(Language.Context_AddToBlacklist)) {
                Ui.Plugin.Functions.AddToBlacklist(player.PlayerName, (ushort) world.RowId);
            }

            if (Ui.Plugin.Functions.IsMentor() && ImGui.Selectable(Language.Context_InviteToNoviceNetwork)) {
                Ui.Plugin.Functions.Context.InviteToNoviceNetwork(player.PlayerName, (ushort) world.RowId);
            }
        }

        var inputChannel = chunk.Message?.Code.Type.ToInputChannel();
        if (inputChannel != null && ImGui.Selectable(Language.Context_ReplyInSelectedChatMode)) {
            Ui.Plugin.Functions.Chat.SetChannel(inputChannel.Value);
            Log.Activate = true;
        }

        if (ImGui.Selectable(Language.Context_Target) && FindCharacterForPayload(player) is { } obj) {
            Plugin.TargetManager.Target = obj;
        }

        if (validContentId && ImGui.Selectable(Language.Context_AdventurerPlate))
        {
            if (!Ui.Plugin.Functions.TryOpenAdventurerPlate(chunk.Message!.ContentId))
                WrapperUtil.AddNotification(Language.Context_AdventurerPlateError, NotificationType.Warning);
        }

        // View Party Finder 0x2E
    }

    private PlayerCharacter? FindCharacterForPayload(PlayerPayload payload) {
        foreach (var obj in Plugin.ObjectTable) {
            if (obj is not PlayerCharacter character)
                continue;

            if (character.Name.TextValue != payload.PlayerName)
                continue;

            if (payload.World.IsPublic && character.HomeWorld.Id != payload.World.RowId)
                continue;

            return character;
        }

        return null;
    }
}
