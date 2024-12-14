using System.Numerics;
using ChatTwo.Code;
using ChatTwo.Resources;
using ChatTwo.Ui;
using ChatTwo.Util;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Config;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.Sheets;

using Action = System.Action;
using DalamudPartyFinderPayload = Dalamud.Game.Text.SeStringHandling.Payloads.PartyFinderPayload;
using ChatTwoPartyFinderPayload = ChatTwo.Util.PartyFinderPayload;

namespace ChatTwo;

public sealed class PayloadHandler
{
    private const string PopupId = "chat2-context-popup";

    private ChatLogWindow LogWindow { get; }
    private (Chunk, Payload?)? Popup { get; set; }

    public bool HandleTooltips;
    public uint HoveredItem;
    public uint HoverCounter;
    public uint LastHoverCounter;

    private const uint PopupSfx = 1u;

    internal PayloadHandler(ChatLogWindow logWindow)
    {
        LogWindow = logWindow;
    }

    internal void Draw()
    {
        DrawPopups();

        if (HandleTooltips && ++HoverCounter - LastHoverCounter > 1)
        {
            GameFunctions.GameFunctions.CloseItemTooltip();
            HoveredItem = 0;
            HoverCounter = LastHoverCounter = 0;
            HandleTooltips = false;
        }
    }

    private void DrawPopups()
    {
        if (Popup == null)
            return;

        var (chunk, payload) = Popup.Value;

        using var popup = ImRaii.Popup(PopupId);
        if (!popup.Success)
        {
            Popup = null;
            return;
        }

        using var id = ImRaii.PushId(PopupId);
        var drawn = false;
        switch (payload)
        {
            case PlayerPayload player:
                DrawPlayerPopup(chunk, player);
                drawn = true;
                break;
            case ItemPayload item:
                DrawItemPopup(item);
                drawn = true;
                break;
            case UriPayload uri:
                DrawUriPopup(uri);
                drawn = true;
                break;
            case StatusPayload status:
                DrawStatusPopup(status);
                drawn = true;
                break;
        }

        ContextFooter(drawn, chunk);
        Integrations(chunk, payload);
    }

    private void Integrations(Chunk chunk, Payload? payload)
    {
        var registered = LogWindow.Plugin.Ipc.Registered;
        if (registered.Count == 0)
            return;

        ImGui.Separator();

        var contentId = chunk.Message?.ContentId ?? 0;
        var sender = chunk.Message?.Sender.Select(c => c.Link).FirstOrDefault(p => p is PlayerPayload) as PlayerPayload;

        using var menu = ImGuiUtil.Menu(Language.Context_Integrations);
        if (!menu.Success)
            return;

        var cursor = ImGui.GetCursorPos();
        foreach (var id in registered)
        {
            try
            {
                LogWindow.Plugin.Ipc.Invoke(id, sender, contentId, payload, chunk.Message?.SenderSource, chunk.Message?.ContentSource);
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Error executing integration");
            }
        }

        if (cursor == ImGui.GetCursorPos())
        {
            using var pushedColor = ImRaii.PushColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
            ImGui.Text("No integrations available");
        }
    }

    private void ContextFooter(bool didCustomContext, Chunk chunk)
    {
        ImRaii.IEndObject? menu = null;
        if (didCustomContext)
        {
            ImGui.Separator();

            // Only place these menu items in a submenu if we've already drawn
            // custom context menu items based on the payload.
            //
            // It makes it much more convenient in the majority of cases to
            // copy the message content without having to open a submenu.
            menu = ImGuiUtil.Menu(Plugin.PluginName);
            if (!menu.Success)
                return;
        }

        // ScreenshotMode changed, so we inform the webinterface about the new message format
        if (ImGui.Checkbox(Language.Context_ScreenshotMode, ref LogWindow.ScreenshotMode))
            LogWindow.Plugin.ServerCore.SendBulkMessageList();

        if (ImGui.Selectable(Language.Context_HideChat))
            LogWindow.UserHide();

        if (chunk.Message is { } message)
        {
            if (ImGui.Selectable(Language.Context_Copy))
            {
                ImGui.SetClipboardText(StringifyMessage(message, true));
                WrapperUtil.AddNotification(Language.Context_CopySuccess, NotificationType.Info);
            }

            // Only show a separate "Copy content" option if the message has
            // Sender chunks, so it doesn't show for system messages.
            if (message.Sender.Count > 0 && ImGui.Selectable(Language.Context_CopyContent))
            {
                ImGui.SetClipboardText(StringifyMessage(message));
                WrapperUtil.AddNotification(Language.Context_CopyContentSuccess, NotificationType.Info);
            }

            using var pushedColor = ImRaii.PushColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int) ImGuiCol.TextDisabled]);
            ImGui.TextUnformatted(message.Code.Type.Name());
        }

        menu?.Dispose();
    }

    private static string StringifyMessage(Message? message, bool withSender = false)
    {
        if (message == null)
            return string.Empty;

        var chunks = withSender ? message.Sender.Concat(message.Content) : message.Content;
        return chunks.Where(chunk => chunk is TextChunk)
            .Cast<TextChunk>()
            .Select(text => text.Content)
            .Aggregate(string.Concat);
    }

    internal void Click(Chunk chunk, Payload? payload, ImGuiMouseButton button)
    {
        if (Plugin.Config.PlaySounds)
            UIGlobals.PlaySoundEffect(PopupSfx);

        switch (button)
        {
            case ImGuiMouseButton.Left:
                LeftClickPayload(chunk, payload);
                break;
            case ImGuiMouseButton.Right:
                RightClickPayload(chunk, payload);
                break;
        }
    }

    internal void Hover(Payload payload)
    {
        var hoverSize = 350f * ImGuiHelpers.GlobalScale;

        switch (payload)
        {
            case StatusPayload status:
                DoHover(() => HoverStatus(status), hoverSize);
                break;
            case ItemPayload item:
                if (Plugin.Config.NativeItemTooltips)
                {
                    if (!HandleTooltips || HoveredItem != item.RawItemId)
                    {
                        HandleTooltips = true;
                        HoveredItem = item.RawItemId;
                        HoverCounter = LastHoverCounter = 0;

                        GameFunctions.GameFunctions.OpenItemTooltip(item.RawItemId, item.Kind);
                    }
                    else
                    {
                        LastHoverCounter = HoverCounter;
                    }

                    return;
                }

                DoHover(() => HoverItem(item), hoverSize);
                break;
            case UriPayload uri:
                DoHover(() => HoverURI(uri), hoverSize);
                break;
        }
    }

    private void DoHover(Action inside, float width)
    {
        ImGui.SetNextWindowSize(new Vector2(width, -1f));

        using (ImRaii.Tooltip())
        using (ImGuiUtil.TextWrapPos())
        using (ImRaii.PushColor(ImGuiCol.Text, LogWindow.DefaultText))
        {
            inside();
        }
    }

    public unsafe void MoveTooltip(AddonEvent type, AddonArgs args)
    {
        if (!HandleTooltips)
            return;

        // Only move if user has "Next to Cursor" option selected
        if (!Plugin.GameConfig.TryGet(UiControlOption.DetailTrackingType, out uint selected) || selected != 0)
            return;

        if (LogWindow.LastViewport != ImGuiHelpers.MainViewport.NativePtr)
            return;

        var atk = (AtkUnitBase*) args.Addon;
        if (atk->WindowNode == null)
            return;

        var component = atk->WindowNode->AtkResNode;

        var atkSize = (X: component.GetWidth() * component.ScaleX, Y: component.GetHeight() * component.GetScaleY());
        var viewportSize = ImGuiHelpers.MainViewport.Size;
        var window = LogWindow.LastWindowPos + LogWindow.LastWindowSize;
        var isLeft = window.X < viewportSize.X / 2;
        var isTop = window.Y < viewportSize.Y / 2;

        var x = isLeft ? window.X : LogWindow.LastWindowPos.X - atkSize.X;
        var y = Math.Clamp(window.Y - atkSize.Y, 0, float.MaxValue);
        y -= isTop ? 0 : Plugin.Config.TooltipOffset; // offset to prevent cut-off on the bottom

        atk->SetPosition((short) x, (short) y);
    }

    private static void InlineIcon(IDalamudTextureWrap icon)
    {
        var cursor = ImGui.GetCursorPos();
        var size = ImGuiHelpers.ScaledVector2(32, 32);
        ImGui.Image(icon.ImGuiHandle, size);
        ImGui.SameLine();
        ImGui.SetCursorPos(cursor + new Vector2(size.X + 4, size.Y - ImGui.GetTextLineHeightWithSpacing()));
    }

    private void HoverStatus(StatusPayload status)
    {
        if (Plugin.TextureProvider.GetFromGameIcon(status.Status.Value.Icon).GetWrapOrDefault() is { } icon)
            InlineIcon(icon);

        var builder = new SeStringBuilder();
        var nameValue = status.Status.Value.Name.ToDalamudString().TextValue;
        switch (status.Status.Value.StatusCategory)
        {
            case 1:
                builder.AddUiForeground($"{SeIconChar.Buff.ToIconString()}{nameValue}", 517);
                break;
            case 2:
                builder.AddUiForeground($"{SeIconChar.Debuff.ToIconString()}{nameValue}", 518);
                break;
            default:
                builder.AddUiForeground(nameValue, 1);
                break;
        };

        var name = ChunkUtil.ToChunks(builder.BuiltString, ChunkSource.None, null);
        LogWindow.DrawChunks(name.ToList());
        ImGui.Separator();

        var descString = status.Status.Value.Description.ToDalamudString();
        var desc = ChunkUtil.ToChunks(descString, ChunkSource.None, null);
        LogWindow.DrawChunks(desc.ToList());
    }

    private void HoverItem(ItemPayload item)
    {
        if (item.Kind == ItemPayload.ItemKind.EventItem)
        {
            HoverEventItem(item);
            return;
        }

        item.Item.TryGetValue(out Item resolvedItem);
        if (Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(resolvedItem.Icon, item.IsHQ)).GetWrapOrDefault() is { } icon)
            InlineIcon(icon);

        var name = ChunkUtil.ToChunks(resolvedItem.Name.ToDalamudString(), ChunkSource.None, null);
        LogWindow.DrawChunks(name.ToList());
        ImGui.Separator();

        var desc = ChunkUtil.ToChunks(resolvedItem.Description.ToDalamudString(), ChunkSource.None, null);
        LogWindow.DrawChunks(desc.ToList());
    }

    private void HoverEventItem(ItemPayload payload)
    {
        if (!Sheets.EventItemSheet.TryGetRow(payload.RawItemId, out var itemRow))
            return;

        if (Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(itemRow.Icon)).GetWrapOrDefault() is { } icon)
            InlineIcon(icon);

        var name = ChunkUtil.ToChunks(itemRow.Name.ToDalamudString(), ChunkSource.None, null);
        LogWindow.DrawChunks(name.ToList());
        ImGui.Separator();

        if (!Sheets.EventItemHelpSheet.TryGetRow(payload.RawItemId, out var itemHelpRow))
            return;

        LogWindow.DrawChunks(ChunkUtil.ToChunks(itemHelpRow.Description.ToDalamudString(), ChunkSource.None, null).ToList());
    }

    private void HoverURI(UriPayload uri)
    {
        ImGui.TextUnformatted(string.Format(Language.Context_URLDomain, uri.Uri.Authority));
        ImGuiUtil.WarningText(Language.Context_URLWarning);
    }

    private void LeftClickPayload(Chunk chunk, Payload? payload)
    {
        switch (payload)
        {
            case MapLinkPayload map:
                Plugin.GameGui.OpenMapWithMapLink(map);
                break;
            case QuestPayload quest:
                GameFunctions.GameFunctions.OpenQuestLog(quest.Quest);
                break;
            case DalamudLinkPayload link:
                ClickLinkPayload(chunk, payload, link);
                break;
            case DalamudPartyFinderPayload pf:
                if (pf.LinkType == DalamudPartyFinderPayload.PartyFinderLinkType.PartyFinderNotification)
                    GameFunctions.GameFunctions.OpenPartyFinder();
                else
                    GameFunctions.GameFunctions.OpenPartyFinder(pf.ListingId);
                break;
            case ChatTwoPartyFinderPayload pf:
                GameFunctions.GameFunctions.OpenPartyFinder(pf.Id);
                break;
            case AchievementPayload achievement:
                GameFunctions.GameFunctions.OpenAchievement(achievement.Id);
                break;
            case RawPayload raw:
                if (Equals(raw, ChunkUtil.PeriodicRecruitmentLink))
                    GameFunctions.GameFunctions.OpenPartyFinder();
                break;
            case UriPayload uri:
                WrapperUtil.TryOpenURI(uri.Uri);
                break;
            default:
                RightClickPayload(chunk, payload);
                break;
        }
    }

    private void ClickLinkPayload(Chunk chunk, Payload payload, DalamudLinkPayload link)
    {
        if (chunk.GetSeString() is not { } source)
            return;

        var start = source.Payloads.IndexOf(payload);
        var end = source.Payloads.IndexOf(RawPayload.LinkTerminator, start == -1 ? 0 : start);
        if (start == -1 || end == -1)
            return;

        var payloads = source.Payloads.Skip(start).Take(end - start + 1).ToList();
        if (!Plugin.ChatGui.RegisteredLinkHandlers.TryGetValue((link.Plugin, link.CommandId), out var value))
        {
            Plugin.Log.Warning("Could not find DalamudLinkHandlers");
            return;
        }

        try
        {
            // Running XivCommon SendChat instantly, without RunOnTick, leads to a game freeze, for whatever reason
            Plugin.Framework.RunOnTick(() => value.Invoke(link.CommandId, new SeString(payloads)));
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error executing DalamudLinkPayload handler");
        }
    }

    private void RightClickPayload(Chunk chunk, Payload? payload)
    {
        Popup = (chunk, payload);
        ImGui.OpenPopup(PopupId);
    }

    private void DrawItemPopup(ItemPayload payload)
    {
        if (payload.Kind == ItemPayload.ItemKind.EventItem)
        {
            DrawEventItemPopup(payload);
            return;
        }

        if (!Sheets.ItemSheet.TryGetRow(payload.ItemId, out var itemRow))
            return;

        var hq = payload.Kind == ItemPayload.ItemKind.Hq;
        if (Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(itemRow.Icon, hq)).GetWrapOrDefault() is { } icon)
            InlineIcon(icon);

        var name = itemRow.Name.ToDalamudString();
        // hq symbol
        if (hq)
            name.Payloads.Add(new TextPayload(" "));
        else if (payload.Kind == ItemPayload.ItemKind.Collectible)
            name.Payloads.Add(new TextPayload(" "));

        LogWindow.DrawChunks(ChunkUtil.ToChunks(name, ChunkSource.None, null).ToList(), false);
        ImGui.Separator();

        var realItemId = payload.RawItemId;
        if (itemRow.EquipSlotCategory.RowId != 0)
        {
            if (ImGui.Selectable(Language.Context_TryOn))
                GameFunctions.Context.TryOn(realItemId, 0);

            if (ImGui.Selectable(Language.Context_ItemComparison))
                GameFunctions.Context.OpenItemComparison(realItemId);
        }

        if (itemRow.ItemSearchCategory.Value.Category == 3)
            if (ImGui.Selectable(Language.Context_SearchRecipes))
                GameFunctions.Context.SearchForRecipesUsingItem(payload.ItemId);

        if (ImGui.Selectable(Language.Context_SearchForItem))
            GameFunctions.Context.SearchForItem(realItemId);

        if (ImGui.Selectable(Language.Context_Link))
            GameFunctions.Context.LinkItem(realItemId);

        if (ImGui.Selectable(Language.Context_CopyItemName))
            ImGui.SetClipboardText(name.TextValue);
    }

    private void DrawEventItemPopup(ItemPayload payload)
    {
        if (payload.Kind != ItemPayload.ItemKind.EventItem)
            return;

        if (!Sheets.EventItemSheet.HasRow(payload.ItemId))
            return;

        var item = Sheets.EventItemSheet.GetRow(payload.ItemId);
        if (Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(item.Icon)).GetWrapOrDefault() is { } icon)
            InlineIcon(icon);

        var name = item.Name.ToDalamudString();
        LogWindow.DrawChunks(ChunkUtil.ToChunks(name, ChunkSource.None, null).ToList(), false);
        ImGui.Separator();

        var realItemId = payload.RawItemId;
        if (ImGui.Selectable(Language.Context_Link))
            GameFunctions.Context.LinkItem(realItemId);

        if (ImGui.Selectable(Language.Context_CopyItemName))
            ImGui.SetClipboardText(name.TextValue);
    }

    private void DrawPlayerPopup(Chunk chunk, PlayerPayload player)
    {
        // Possible that GMs return a null payload
        if (player == null)
            return;

        var world = player.World;
        if (chunk.Message?.Code.Type == ChatType.FreeCompanyLoginLogout)
            if (Plugin.ClientState.LocalPlayer?.HomeWorld.IsValid == true)
                world = Plugin.ClientState.LocalPlayer.HomeWorld;

        var name = new List<Chunk> { new TextChunk(ChunkSource.None, null, player.PlayerName) };
        if (world.Value.IsPublic)
        {
            name.AddRange(new Chunk[]
            {
                new IconChunk(ChunkSource.None, null, BitmapFontIcon.CrossWorld),
                new TextChunk(ChunkSource.None, null, world.Value.Name.ExtractText()),
            });
        }

        LogWindow.DrawChunks(name, false);
        ImGui.Separator();

        var validContentId = chunk.Message?.ContentId is not (null or 0);
        if (ImGui.Selectable(Language.Context_SendTell))
        {
            // Eureka and Bozja need special handling as tells work different
            if (Sheets.TerritorySheet.GetRow(Plugin.ClientState.TerritoryType).TerritoryIntendedUse.RowId != 41)
            {
                LogWindow.Chat = $"/tell {player.PlayerName}";
                if (world.Value.IsPublic)
                    LogWindow.Chat += $"@{world.Value.Name}";

                LogWindow.Chat += " ";
            }
            else if (validContentId)
            {
                LogWindow.Plugin.Functions.Chat.SetEurekaTellChannel(player.PlayerName, world.Value.Name.ToString(), (ushort) world.RowId, 0, chunk.Message!.ContentId, 0, false);
            }

            LogWindow.Activate = true;
        }

        if (world.Value.IsPublic)
        {
            var party = Plugin.PartyList;
            var leader = (ulong?) party[(int) party.PartyLeaderIndex]?.ContentId;
            var isLeader = party.Length == 0 || Plugin.ClientState.LocalContentId == leader;
            var member = party.FirstOrDefault(member => member.Name.TextValue == player.PlayerName && member.World.RowId == world.RowId);
            var isInParty = member != default;
            var inInstance = GameFunctions.GameFunctions.IsInInstance();
            var inPartyInstance = Sheets.TerritorySheet.GetRow(Plugin.ClientState.TerritoryType).TerritoryIntendedUse.RowId is (41 or 47 or 48 or 52 or 53);
            if (isLeader)
            {
                if (!isInParty)
                {
                    if (inInstance && inPartyInstance)
                    {
                        if (validContentId && ImGui.Selectable(Language.Context_InviteToParty))
                            GameFunctions.Party.InviteInInstance(chunk.Message!.ContentId);
                    }
                    else if (!inInstance)
                    {
                        using var menu = ImGuiUtil.Menu(Language.Context_InviteToParty);
                        if (menu.Success)
                        {
                            if (ImGui.Selectable(Language.Context_InviteToParty_SameWorld))
                                GameFunctions.Party.InviteSameWorld(player.PlayerName, (ushort)world.RowId, chunk.Message?.ContentId ?? 0);

                            if (validContentId && ImGui.Selectable(Language.Context_InviteToParty_DifferentWorld))
                                GameFunctions.Party.InviteOtherWorld(chunk.Message!.ContentId, (ushort)world.RowId);
                        }
                    }
                }

                if (isInParty && member != null && (!inInstance || (inInstance && inPartyInstance)))
                {
                    if (ImGui.Selectable(Language.Context_Promote))
                        GameFunctions.Party.Promote(player.PlayerName, (ulong) member.ContentId);

                    if (ImGui.Selectable(Language.Context_KickFromParty))
                        GameFunctions.Party.Kick(player.PlayerName, (ulong) member.ContentId);
                }
            }

            var isFriend = GameFunctions.GameFunctions.GetFriends().Any(friend => friend.NameString == player.PlayerName && friend.HomeWorld == world.RowId);
            if (!isFriend && ImGui.Selectable(Language.Context_SendFriendRequest))
                LogWindow.Plugin.Functions.SendFriendRequest(player.PlayerName, (ushort) world.RowId);

            if (ImGui.Selectable(Language.Context_AddToBlacklist))
                LogWindow.Plugin.Functions.AddToBlacklist(player.PlayerName, (ushort) world.RowId);

            if (GameFunctions.GameFunctions.IsMentor() && ImGui.Selectable(Language.Context_InviteToNoviceNetwork))
                GameFunctions.Context.InviteToNoviceNetwork(player.PlayerName, (ushort) world.RowId);
        }

        var inputChannel = chunk.Message?.Code.Type.ToInputChannel();
        if (inputChannel != null && ImGui.Selectable(Language.Context_ReplyInSelectedChatMode))
        {
            LogWindow.SetChannel(inputChannel.Value);
            LogWindow.Activate = true;
        }

        if (ImGui.Selectable(Language.Context_Target) && FindCharacterForPayload(player) is { } obj)
            Plugin.TargetManager.Target = obj;

        if (validContentId && ImGui.Selectable(Language.Context_AdventurerPlate))
            if (!GameFunctions.GameFunctions.TryOpenAdventurerPlate(chunk.Message!.ContentId))
                WrapperUtil.AddNotification(Language.Context_AdventurerPlateError, NotificationType.Warning);
    }

    private IPlayerCharacter? FindCharacterForPayload(PlayerPayload payload)
    {
        foreach (var obj in Plugin.ObjectTable)
        {
            if (obj is not IPlayerCharacter character)
                continue;

            if (character.Name.TextValue != payload.PlayerName)
                continue;

            if (payload.World.Value.IsPublic && character.HomeWorld.RowId != payload.World.RowId)
                continue;

            return character;
        }

        return null;
    }

    private void DrawUriPopup(UriPayload uri)
    {
        ImGui.TextUnformatted(string.Format(Language.Context_URLDomain, uri.Uri.Authority));
        ImGuiUtil.WarningText(Language.Context_URLWarning, false);
        ImGui.Separator();

        if (ImGui.Selectable(Language.Context_OpenInBrowser))
            WrapperUtil.TryOpenURI(uri.Uri);

        if (ImGui.Selectable(Language.Context_CopyLink))
        {
            ImGui.SetClipboardText(uri.Uri.ToString());
            WrapperUtil.AddNotification(Language.Context_CopyLinkNotification, NotificationType.Info);
        }
    }

    private void DrawStatusPopup(StatusPayload status)
    {
        if (Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(status.Status.Value.Icon)).GetWrapOrDefault() is { } icon)
            InlineIcon(icon);

        var builder = new SeStringBuilder();
        var nameValue = status.Status.Value.Name.ToDalamudString().TextValue;
        switch (status.Status.Value.StatusCategory)
        {
            case 1:
                builder.AddUiForeground($"{SeIconChar.Buff.ToIconString()}{nameValue}", 517);
                break;
            case 2:
                builder.AddUiForeground($"{SeIconChar.Debuff.ToIconString()}{nameValue}", 518);
                break;
            default:
                builder.AddUiForeground(nameValue, 1);
                break;
        };

        LogWindow.DrawChunks(ChunkUtil.ToChunks(builder.BuiltString, ChunkSource.None, null).ToList(), false);
        ImGui.Separator();

        if (ImGui.Selectable(Language.Context_Link))
        {
            GameFunctions.Context.LinkStatus(status.Status.RowId);
            LogWindow.Chat += " <status>";
        }
    }
}
