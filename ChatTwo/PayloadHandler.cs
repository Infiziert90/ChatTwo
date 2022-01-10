using System.Numerics;
using System.Reflection;
using ChatTwo.Ui;
using ChatTwo.Util;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Logging;
using Dalamud.Utility;
using ImGuiNET;
using ImGuiScene;

namespace ChatTwo;

internal sealed class PayloadHandler {
    private PluginUi Ui { get; }
    private ChatLog Log { get; }

    private HashSet<Payload> PopupPayloads { get; set; } = new();

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
            this.Ui.Plugin.Functions.CloseItemTooltip();
            this._hoveredItem = 0;
            this._hoverCounter = this._lastHoverCounter = 0;
            this._handleTooltips = false;
        }
    }

    private void DrawPopups() {
        var newPopups = new HashSet<Payload>();
        foreach (var payload in this.PopupPayloads) {
            var id = PopupId(payload);
            if (id == null) {
                continue;
            }

            if (!ImGui.BeginPopup(id)) {
                continue;
            }

            newPopups.Add(payload);
            ImGui.PushID(id);

            switch (payload) {
                case PlayerPayload player: {
                    this.DrawPlayerPopup(player);
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
                this.RightClickPayload(payload);
                break;
        }
    }

    internal void Hover(Payload payload) {
        switch (payload) {
            case StatusPayload status: {
                this.DoHover(() => this.HoverStatus(status), 250f);
                break;
            }
            case ItemPayload item: {
                if (this.Ui.Plugin.Config.NativeItemTooltips) {
                    this.Ui.Plugin.Functions.OpenItemTooltip(item.ItemId);

                    this._handleTooltips = true;
                    if (this._hoveredItem != item.ItemId) {
                        this._hoveredItem = item.ItemId;
                        this._hoverCounter = this._lastHoverCounter = 0;
                    } else {
                        this._lastHoverCounter = this._hoverCounter;
                    }

                    break;
                }

                this.DoHover(() => this.HoverItem(item), 250f);
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
        ImGui.Image(icon.ImGuiHandle, new Vector2(icon.Width, icon.Height));
        ImGui.SameLine();
        ImGui.SetCursorPos(cursor + new Vector2(icon.Width + 4, (float) icon.Height / 2 - lineHeight / 2));
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
        if (this.Ui.Plugin.TextureCache.GetItem(item.Item) is { } icon) {
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

    private void RightClickPayload(Payload payload) {
        switch (payload) {
            case PlayerPayload:
            case ItemPayload: {
                this.PopupPayloads.Add(payload);
                ImGui.OpenPopup(PopupId(payload));
                break;
            }
        }
    }

    private void DrawItemPopup(ItemPayload item) {
        if (this.Ui.Plugin.TextureCache.GetItem(item.Item) is { } icon) {
            InlineIcon(icon);
        }

        var name = item.Item.Name.ToDalamudString();
        if (item.IsHQ) {
            // hq symbol
            name.Payloads.Add(new TextPayload(" "));
        }

        this.Log.DrawChunks(ChunkUtil.ToChunks(name, null).ToList(), false);
        ImGui.Separator();

        var realItemId = (uint) (item.ItemId + (item.IsHQ ? GameFunctions.HqItemOffset : 0));

        if (item.Item.EquipSlotCategory.Row != 0) {
            if (ImGui.Selectable("Try On")) {
                this.Ui.Plugin.Functions.TryOn(realItemId, 0);
            }

            if (ImGui.Selectable("Item Comparison")) {
                this.Ui.Plugin.Functions.OpenItemComparison(realItemId);
            }
        }

        if (ImGui.Selectable("Link")) {
            this.Ui.Plugin.Functions.LinkItem(realItemId);
        }

        if (ImGui.Selectable("Copy Item Name")) {
            ImGui.SetClipboardText(name.TextValue);
        }
    }

    private void DrawPlayerPopup(PlayerPayload player) {
        var name = player.PlayerName;
        if (player.World.IsPublic) {
            name += $"{player.World.Name}";
        }

        ImGui.TextUnformatted(name);
        ImGui.Separator();

        if (player.World.IsPublic) {
            if (ImGui.Selectable("Send Tell")) {
                this.Log.Chat = $"/tell {player.PlayerName}@{player.World.Name} ";
                this.Log.Activate = true;
            }

            if (ImGui.Selectable("Invite to Party")) {
                // FIXME: don't show if player is in your party or if you're in their party
                // FIXME: don't show if in party and not leader
                this.Ui.Plugin.Functions.InviteToParty(player.PlayerName, (ushort) player.World.RowId);
            }

            if (ImGui.Selectable("Send Friend Request")) {
                // FIXME: this shows window, clicking yes doesn't work
                // FIXME: only show if not already friend
                this.Ui.Plugin.Functions.SendFriendRequest(player.PlayerName, (ushort) player.World.RowId);
            }

            if (ImGui.Selectable("Invite to Novice Network")) {
                // FIXME: only show if character is mentor and target is sprout/returner
                this.Ui.Plugin.Functions.InviteToNoviceNetwork(player.PlayerName, (ushort) player.World.RowId);
            }
        }

        if (ImGui.Selectable("Target") && this.FindCharacterForPayload(player) is { } obj) {
            this.Ui.Plugin.TargetManager.SetTarget(obj);
        }

        ImGui.Separator();

        ImGui.Checkbox("Screenshot mode", ref this.Ui.ScreenshotMode);

        // Add to Blacklist 0x1C
        // View Party Finder 0x2E
        // Reply in Selected Chat Mode 0x64
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
        ItemPayload item => $"###item-{item.ItemId}{item.IsHQ}",
        _ => null,
    };
}
