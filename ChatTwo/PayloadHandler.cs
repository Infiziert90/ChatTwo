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

    private HashSet<PlayerPayload> Popups { get; set; } = new();

    private bool _handleTooltips;
    private uint _hoveredItem;
    private uint _hoverCounter;
    private uint _lastHoverCounter;

    internal PayloadHandler(PluginUi ui, ChatLog log) {
        this.Ui = ui;
        this.Log = log;
    }

    internal void Draw() {
        var newPopups = new HashSet<PlayerPayload>();
        foreach (var player in this.Popups) {
            var id = PopupId(player);
            if (!ImGui.BeginPopup(id)) {
                continue;
            }

            newPopups.Add(player);
            ImGui.PushID(id);

            this.DrawPlayerPopup(player);

            ImGui.PopID();
            ImGui.EndPopup();
        }

        this.Popups = newPopups;

        if (this._handleTooltips && ++this._hoverCounter - this._lastHoverCounter > 1) {
            this.Ui.Plugin.Functions.CloseItemTooltip();
            this._hoveredItem = 0;
            this._hoverCounter = this._lastHoverCounter = 0;
            this._handleTooltips = false;
        }
    }

    internal void Click(Chunk chunk, Payload payload, ImGuiMouseButton button) {
        PluginLog.Log($"clicked {payload} with {button}");

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
            case PlayerPayload player: {
                this.Popups.Add(player);
                ImGui.OpenPopup(PopupId(player));
                break;
            }
        }
    }

    private void DrawPlayerPopup(PlayerPayload player) {
        var name = player.PlayerName;
        if (player.World.IsPublic) {
            name += $"{player.World.Name}";
        }

        ImGui.TextUnformatted(name);
        ImGui.Separator();

        if (player.World.IsPublic && ImGui.Selectable("Send Tell")) {
            this.Log.Chat = $"/tell {player.PlayerName}@{player.World.Name} ";
            this.Log.Activate = true;
        }

        if (ImGui.Selectable("Target")) {
            foreach (var obj in this.Ui.Plugin.ObjectTable) {
                if (obj is not PlayerCharacter character) {
                    continue;
                }

                if (character.Name.TextValue != player.PlayerName) {
                    continue;
                }

                if (player.World.IsPublic && character.HomeWorld.Id != player.World.RowId) {
                    continue;
                }

                this.Ui.Plugin.TargetManager.SetTarget(obj);
                break;
            }
        }
    }

    private static string PopupId(PlayerPayload player) {
        return $"###player-{player.PlayerName}@{player.World}";
    }
}
