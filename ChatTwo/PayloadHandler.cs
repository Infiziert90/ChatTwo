using System.Numerics;
using System.Reflection;
using ChatTwo.Code;
using ChatTwo.Ui;
using ChatTwo.Util;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Logging;
using Dalamud.Utility;
using ImGuiNET;
using ImGuiScene;
using Lumina.Excel.GeneratedSheets;
using Action = System.Action;

namespace ChatTwo;

internal sealed class PayloadHandler {
    private PluginUi Ui { get; }
    private ChatLog Log { get; }

    private HashSet<(Chunk, Payload)> PopupPayloads { get; set; } = new();

    private bool _handleTooltips;
    private uint _hoveredItem;
    private uint _hoverCounter;
    private uint _lastHoverCounter;

    internal PayloadHandler(PluginUi ui, ChatLog log) {
        this.Ui = ui;
        this.Log = log;
    }

    internal void Draw() {
        this.DrawPopups();

        if (this._handleTooltips && ++this._hoverCounter - this._lastHoverCounter > 1) {
            GameFunctions.GameFunctions.CloseItemTooltip();
            this._hoveredItem = 0;
            this._hoverCounter = this._lastHoverCounter = 0;
            this._handleTooltips = false;
        }
    }

    private void DrawPopups() {
        var newPopups = new HashSet<(Chunk, Payload)>();
        foreach (var (chunk, payload) in this.PopupPayloads) {
            var id = PopupId(payload);
            if (id == null) {
                continue;
            }

            if (!ImGui.BeginPopup(id)) {
                continue;
            }

            newPopups.Add((chunk, payload));
            ImGui.PushID(id);

            switch (payload) {
                case PlayerPayload player: {
                    this.DrawPlayerPopup(chunk, player);
                    break;
                }
                case ItemPayload item: {
                    this.DrawItemPopup(item);
                    break;
                }
            }

            ImGui.PopID();
            ImGui.EndPopup();
        }

        this.PopupPayloads = newPopups;
    }

    internal void Click(Chunk chunk, Payload payload, ImGuiMouseButton button) {
        switch (button) {
            case ImGuiMouseButton.Left:
                this.LeftClickPayload(chunk, payload);
                break;
            case ImGuiMouseButton.Right:
                this.RightClickPayload(chunk, payload);
                break;
        }
    }

    internal void Hover(Payload payload) {
        var hoverSize = 250f * ImGuiHelpers.GlobalScale;

        switch (payload) {
            case StatusPayload status: {
                this.DoHover(() => this.HoverStatus(status), hoverSize);
                break;
            }
            case ItemPayload item: {
                if (this.Ui.Plugin.Config.NativeItemTooltips) {
                    GameFunctions.GameFunctions.OpenItemTooltip(item.RawItemId);

                    this._handleTooltips = true;
                    if (this._hoveredItem != item.RawItemId) {
                        this._hoveredItem = item.RawItemId;
                        this._hoverCounter = this._lastHoverCounter = 0;
                    } else {
                        this._lastHoverCounter = this._hoverCounter;
                    }

                    break;
                }

                this.DoHover(() => this.HoverItem(item), hoverSize);
                break;
            }
        }
    }

    private void DoHover(Action inside, float width) {
        ImGui.SetNextWindowSize(new Vector2(width, -1f));

        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos();

        ImGui.PushStyleColor(ImGuiCol.Text, this.Ui.DefaultText);
        try {
            inside();
        } finally {
            ImGui.PopStyleColor();
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
    }

    private static void InlineIcon(TextureWrap icon) {
        var lineHeight = ImGui.CalcTextSize("A").Y;

        var cursor = ImGui.GetCursorPos();
        var size = new Vector2(icon.Width, icon.Height) * ImGuiHelpers.GlobalScale;
        ImGui.Image(icon.ImGuiHandle, size);
        ImGui.SameLine();
        ImGui.SetCursorPos(cursor + new Vector2(size.X + 4, size.Y / 2 - lineHeight / 2));
    }

    private void HoverStatus(StatusPayload status) {
        if (this.Ui.Plugin.TextureCache.GetStatus(status.Status) is { } icon) {
            InlineIcon(icon);
        }

        var name = ChunkUtil.ToChunks(status.Status.Name.ToDalamudString(), null);
        this.Log.DrawChunks(name.ToList());
        ImGui.Separator();

        var desc = ChunkUtil.ToChunks(status.Status.Description.ToDalamudString(), null);
        this.Log.DrawChunks(desc.ToList());
    }

    private void HoverItem(ItemPayload item) {
        if (item.Item == null) {
            return;
        }

        if (this.Ui.Plugin.TextureCache.GetItem(item.Item, item.IsHQ) is { } icon) {
            InlineIcon(icon);
        }

        var name = ChunkUtil.ToChunks(item.Item.Name.ToDalamudString(), null);
        this.Log.DrawChunks(name.ToList());
        ImGui.Separator();

        var desc = ChunkUtil.ToChunks(item.Item.Description.ToDalamudString(), null);
        this.Log.DrawChunks(desc.ToList());
    }

    private void LeftClickPayload(Chunk chunk, Payload payload) {
        switch (payload) {
            case MapLinkPayload map: {
                this.Ui.Plugin.GameGui.OpenMapWithMapLink(map);
                break;
            }
            case QuestPayload quest: {
                this.Ui.Plugin.Common.Functions.Journal.OpenQuest(quest.Quest);
                break;
            }
            case DalamudLinkPayload link: {
                this.ClickLinkPayload(chunk, payload, link);
                break;
            }
            case PartyFinderPayload pf: {
                this.Ui.Plugin.Functions.OpenPartyFinder(pf.Id);
                break;
            }
            case AchievementPayload achievement: {
                this.Ui.Plugin.Functions.OpenAchievement(achievement.Id);
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
        if (chunk.Source is not { } source) {
            return;
        }

        var start = source.Payloads.IndexOf(payload);
        var end = source.Payloads.IndexOf(RawPayload.LinkTerminator);
        if (start == -1 || end == -1) {
            return;
        }

        var payloads = source.Payloads.Skip(start).Take(end - start + 1).ToList();

        var chatGui = this.Ui.Plugin.ChatGui;
        var field = chatGui.GetType().GetField("dalamudLinkHandlers", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null || field.GetValue(chatGui) is not Dictionary<(string PluginName, uint CommandId), Action<uint, SeString>> dict || !dict.TryGetValue((link.Plugin, link.CommandId), out var action)) {
            return;
        }

        try {
            action(link.CommandId, new SeString(payloads));
        } catch (Exception ex) {
            PluginLog.LogError(ex, "Error executing DalamudLinkPayload handler");
        }
    }

    private void RightClickPayload(Chunk chunk, Payload payload) {
        switch (payload) {
            case PlayerPayload:
            case ItemPayload: {
                this.PopupPayloads.Add((chunk, payload));
                ImGui.OpenPopup(PopupId(payload));
                break;
            }
        }
    }

    private void DrawItemPopup(ItemPayload payload) {
        if (payload.Kind == ItemPayload.ItemKind.EventItem) {
            this.DrawEventItemPopup(payload);
            return;
        }

        var item = this.Ui.Plugin.DataManager.GetExcelSheet<Item>()?.GetRow(payload.ItemId);
        if (item == null) {
            return;
        }

        var hq = payload.Kind == ItemPayload.ItemKind.Hq;

        if (this.Ui.Plugin.TextureCache.GetItem(item, hq) is { } icon) {
            InlineIcon(icon);
        }

        var name = item.Name.ToDalamudString();
        if (hq) {
            // hq symbol
            name.Payloads.Add(new TextPayload(" "));
        } else if (payload.Kind == ItemPayload.ItemKind.Collectible) {
            name.Payloads.Add(new TextPayload(" "));
        }

        this.Log.DrawChunks(ChunkUtil.ToChunks(name, null).ToList(), false);
        ImGui.Separator();

        var realItemId = payload.RawItemId;

        if (item.EquipSlotCategory.Row != 0) {
            if (ImGui.Selectable("Try On")) {
                this.Ui.Plugin.Functions.Context.TryOn(realItemId, 0);
            }

            if (ImGui.Selectable("Item Comparison")) {
                this.Ui.Plugin.Functions.Context.OpenItemComparison(realItemId);
            }
        }

        if (item.ItemSearchCategory.Value?.Category == 3) {
            if (ImGui.Selectable("Search Recipes Using This Material")) {
                this.Ui.Plugin.Functions.Context.SearchForRecipesUsingItem(payload.ItemId);
            }
        }

        if (ImGui.Selectable("Search for Item")) {
            this.Ui.Plugin.Functions.Context.SearchForItem(realItemId);
        }

        if (ImGui.Selectable("Link")) {
            this.Ui.Plugin.Functions.Context.LinkItem(realItemId);
        }

        if (ImGui.Selectable("Copy Item Name")) {
            ImGui.SetClipboardText(name.TextValue);
        }
    }

    private void DrawEventItemPopup(ItemPayload payload) {
        if (payload.Kind != ItemPayload.ItemKind.EventItem) {
            return;
        }

        var item = this.Ui.Plugin.DataManager.GetExcelSheet<EventItem>()?.GetRow(payload.ItemId);
        if (item == null) {
            return;
        }

        if (this.Ui.Plugin.TextureCache.GetEventItem(item) is { } icon) {
            InlineIcon(icon);
        }

        var name = item.Name.ToDalamudString();
        this.Log.DrawChunks(ChunkUtil.ToChunks(name, null).ToList(), false);
        ImGui.Separator();

        var realItemId = payload.RawItemId;

        if (ImGui.Selectable("Link")) {
            this.Ui.Plugin.Functions.Context.LinkItem(realItemId);
        }

        if (ImGui.Selectable("Copy Item Name")) {
            ImGui.SetClipboardText(name.TextValue);
        }
    }

    private void DrawPlayerPopup(Chunk chunk, PlayerPayload player) {
        var name = new List<Chunk> { new TextChunk(null, null, player.PlayerName) };
        if (player.World.IsPublic) {
            name.AddRange(new Chunk[] {
                new IconChunk(null, null, BitmapFontIcon.CrossWorld),
                new TextChunk(null, null, player.World.Name),
            });
        }

        this.Log.DrawChunks(name, false);
        ImGui.Separator();

        if (player.World.IsPublic) {
            if (ImGui.Selectable("Send Tell")) {
                this.Log.Chat = $"/tell {player.PlayerName}@{player.World.Name} ";
                this.Log.Activate = true;
            }

            var party = this.Ui.Plugin.PartyList;
            var leader = (ulong?) party[(int) party.PartyLeaderIndex]?.ContentId;
            var isLeader = party.Length == 0 || this.Ui.Plugin.ClientState.LocalContentId == leader;
            var member = party.FirstOrDefault(member => member.Name.TextValue == player.PlayerName && member.World.Id == player.World.RowId);
            var isInParty = member != default;
            if (isLeader) {
                if (!isInParty && ImGui.BeginMenu("Invite to Party")) {
                    if (ImGui.Selectable("Same world")) {
                        this.Ui.Plugin.Functions.Party.InviteSameWorld(player.PlayerName, (ushort) player.World.RowId, chunk.Message?.ContentId ?? 0);
                    }

                    if (chunk.Message?.ContentId is not null or 0 && ImGui.Selectable("Different world")) {
                        this.Ui.Plugin.Functions.Party.InviteOtherWorld(chunk.Message!.ContentId);
                    }

                    ImGui.EndMenu();
                }

                if (isInParty && member != null) {
                    if (ImGui.Selectable("Promote")) {
                        this.Ui.Plugin.Functions.Party.Promote(player.PlayerName, (ulong) member.ContentId);
                    }

                    if (ImGui.Selectable("Kick from Party")) {
                        this.Ui.Plugin.Functions.Party.Kick(player.PlayerName, (ulong) member.ContentId);
                    }
                }
            }

            var isFriend = this.Ui.Plugin.Common.Functions.FriendList.List.Any(friend => friend.Name.TextValue == player.PlayerName && friend.HomeWorld == player.World.RowId);
            if (!isFriend && ImGui.Selectable("Send Friend Request")) {
                this.Ui.Plugin.Functions.SendFriendRequest(player.PlayerName, (ushort) player.World.RowId);
            }

            if (ImGui.Selectable("Add to Blacklist")) {
                this.Ui.Plugin.Functions.AddToBlacklist(player.PlayerName, (ushort) player.World.RowId);
            }

            if (this.Ui.Plugin.Functions.IsMentor() && ImGui.Selectable("Invite to Novice Network")) {
                this.Ui.Plugin.Functions.Context.InviteToNoviceNetwork(player.PlayerName, (ushort) player.World.RowId);
            }
        }

        var inputChannel = chunk.Message?.Code.Type.ToInputChannel();
        if (inputChannel != null && ImGui.Selectable("Reply in Selected Chat Mode")) {
            this.Ui.Plugin.Functions.Chat.SetChannel(inputChannel.Value);
            this.Log.Activate = true;
        }

        if (ImGui.Selectable("Target") && this.FindCharacterForPayload(player) is { } obj) {
            this.Ui.Plugin.TargetManager.SetTarget(obj);
        }

        ImGui.Separator();

        ImGui.Checkbox("Screenshot mode", ref this.Ui.ScreenshotMode);

        // View Party Finder 0x2E
    }

    private PlayerCharacter? FindCharacterForPayload(PlayerPayload payload) {
        foreach (var obj in this.Ui.Plugin.ObjectTable) {
            if (obj is not PlayerCharacter character) {
                continue;
            }

            if (character.Name.TextValue != payload.PlayerName) {
                continue;
            }

            if (payload.World.IsPublic && character.HomeWorld.Id != payload.World.RowId) {
                continue;
            }

            return character;
        }

        return null;
    }

    private static string? PopupId(Payload payload) => payload switch {
        PlayerPayload player => $"###player-{player.PlayerName}@{player.World}",
        ItemPayload item => $"###item-{item.RawItemId}",
        _ => null,
    };
}
