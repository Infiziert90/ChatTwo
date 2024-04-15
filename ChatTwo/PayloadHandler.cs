using System.Numerics;
using ChatTwo.Code;
using ChatTwo.Resources;
using ChatTwo.Ui;
using ChatTwo.Util;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Config;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Interface.Utility;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

using Action = System.Action;
using DalamudPartyFinderPayload = Dalamud.Game.Text.SeStringHandling.Payloads.PartyFinderPayload;
using ChatTwoPartyFinderPayload = ChatTwo.Util.PartyFinderPayload;

namespace ChatTwo;

public sealed class PayloadHandler {
    private const string PopupId = "chat2-context-popup";

    private ChatLogWindow LogWindow { get; }
    private (Chunk, Payload?)? Popup { get; set; }

    private bool _handleTooltips;
    private uint _hoveredItem;
    private uint _hoverCounter;
    private uint _lastHoverCounter;

    private readonly ExcelSheet<Item> ItemSheet;
    private readonly ExcelSheet<EventItem> EventItemSheet;
    private readonly ExcelSheet<TerritoryType> TerritorySheet;
    private readonly ExcelSheet<EventItemHelp> EventItemHelpSheet;

    private uint PopupSfx = 1u;

    internal PayloadHandler(ChatLogWindow logWindow)
    {
        LogWindow = logWindow;

        ItemSheet = Plugin.DataManager.GetExcelSheet<Item>()!;
        EventItemSheet = Plugin.DataManager.GetExcelSheet<EventItem>()!;
        TerritorySheet = Plugin.DataManager.GetExcelSheet<TerritoryType>()!;
        EventItemHelpSheet = Plugin.DataManager.GetExcelSheet<EventItemHelp>()!;
    }

    internal void Draw()
    {
        DrawPopups();

        if (_handleTooltips && ++_hoverCounter - _lastHoverCounter > 1)
        {
            GameFunctions.GameFunctions.CloseItemTooltip();
            _hoveredItem = 0;
            _hoverCounter = _lastHoverCounter = 0;
            _handleTooltips = false;
        }
    }

    private void DrawPopups()
    {
        if (Popup == null)
            return;

        var (chunk, payload) = Popup.Value;

        if (!ImGui.BeginPopup(PopupId))
        {
            Popup = null;
            return;
        }

        ImGui.PushID(PopupId);

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
            case URIPayload uri:
                DrawUriPopup(uri);
                drawn = true;
                break;
        }

        ContextFooter(drawn, chunk);
        Integrations(chunk, payload);

        ImGui.PopID();
        ImGui.EndPopup();
    }

    private void Integrations(Chunk chunk, Payload? payload)
    {
        var registered = LogWindow.Plugin.Ipc.Registered;
        if (registered.Count == 0)
            return;

        ImGui.Separator();

        var contentId = chunk.Message?.ContentId ?? 0;
        var sender = chunk.Message?.Sender.Select(c => c.Link).FirstOrDefault(p => p is PlayerPayload) as PlayerPayload;

        if (ImGui.BeginMenu(Language.Context_Integrations))
        {
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
                ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int) ImGuiCol.TextDisabled]);
                ImGui.Text("No integrations available");
                ImGui.PopStyleColor();
            }

            ImGui.EndMenu();
        }
    }

    private void ContextFooter(bool didCustomContext, Chunk chunk)
    {
        if (didCustomContext)
        {
            ImGui.Separator();

            // Only place these menu items in a submenu if we've already drawn
            // custom context menu items based on the payload.
            //
            // It makes it much more convenient in the majority of cases to
            // copy the message content without having to open a submenu.
            if (!ImGui.BeginMenu(Plugin.PluginName))
                return;
        }

        ImGui.Checkbox(Language.Context_ScreenshotMode, ref LogWindow.ScreenshotMode);

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

        if (didCustomContext) ImGui.EndMenu();
    }

    private static string StringifyMessage(Message? message, bool withSender = false)
    {
        if (message == null)
            return string.Empty;

        var chunks = withSender ? message.Sender.Concat(message.Content) : message.Content;
        return chunks
            .Where(chunk => chunk is TextChunk)
            .Cast<TextChunk>()
            .Select(text => text.Content)
            .Aggregate(string.Concat);
    }

    internal void Click(Chunk chunk, Payload? payload, ImGuiMouseButton button)
    {
        if (LogWindow.Plugin.Config.PlaySounds)
            UIModule.PlaySound(PopupSfx);

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

    internal void Hover(Payload payload) {
        var hoverSize = 250f * ImGuiHelpers.GlobalScale;

        switch (payload)
        {
            case StatusPayload status:
                DoHover(() => HoverStatus(status), hoverSize);
                break;
            case ItemPayload item:
                if (LogWindow.Plugin.Config.NativeItemTooltips)
                {
                    GameFunctions.GameFunctions.OpenItemTooltip(item.RawItemId);

                    _handleTooltips = true;
                    if (_hoveredItem != item.RawItemId)
                    {
                        _hoveredItem = item.RawItemId;
                        _hoverCounter = _lastHoverCounter = 0;
                    }
                    else
                    {
                        _lastHoverCounter = _hoverCounter;
                    }

                    break;
                }

                DoHover(() => HoverItem(item), hoverSize);
                break;
            case URIPayload uri:
                DoHover(() => HoverURI(uri), hoverSize);
                break;
        }
    }

    private void DoHover(Action inside, float width)
    {
        ImGui.SetNextWindowSize(new Vector2(width, -1f));

        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos();
        ImGui.PushStyleColor(ImGuiCol.Text, LogWindow.DefaultText);

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

    public unsafe void MoveTooltip(AddonEvent type, AddonArgs args)
    {
        if (!_handleTooltips)
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
        y -= isTop ? 0 : LogWindow.Plugin.Config.TooltipOffset; // offset to prevent cut-off on the bottom

        atk->SetPosition((short) x, (short) y);
    }

    private static void InlineIcon(IDalamudTextureWrap icon)
    {
        var lineHeight = ImGui.CalcTextSize("A").Y;

        var cursor = ImGui.GetCursorPos();
        var size = new Vector2(icon.Width, icon.Height) * ImGuiHelpers.GlobalScale;
        ImGui.Image(icon.ImGuiHandle, size);
        ImGui.SameLine();
        ImGui.SetCursorPos(cursor + new Vector2(size.X + 4, size.Y / 2 - lineHeight / 2));
    }

    private void HoverStatus(StatusPayload status)
    {
        if (LogWindow.Plugin.TextureCache.GetStatus(status.Status) is { } icon)
            InlineIcon(icon);

        var name = ChunkUtil.ToChunks(status.Status.Name.ToDalamudString(), ChunkSource.None, null);
        LogWindow.DrawChunks(name.ToList());
        ImGui.Separator();

        var desc = ChunkUtil.ToChunks(status.Status.Description.ToDalamudString(), ChunkSource.None, null);
        LogWindow.DrawChunks(desc.ToList());
    }

    private void HoverItem(ItemPayload item)
    {
        if (item.Kind == ItemPayload.ItemKind.EventItem)
        {
            HoverEventItem(item);
            return;
        }

        if (item.Item == null)
            return;

        if (LogWindow.Plugin.TextureCache.GetItem(item.Item, item.IsHQ) is { } icon)
            InlineIcon(icon);

        var name = ChunkUtil.ToChunks(item.Item.Name.ToDalamudString(), ChunkSource.None, null);
        LogWindow.DrawChunks(name.ToList());
        ImGui.Separator();

        var desc = ChunkUtil.ToChunks(item.Item.Description.ToDalamudString(), ChunkSource.None, null);
        LogWindow.DrawChunks(desc.ToList());
    }

    private void HoverEventItem(ItemPayload payload)
    {
        var item = EventItemSheet.GetRow(payload.RawItemId);
        if (item == null)
            return;

        if (LogWindow.Plugin.TextureCache.GetEventItem(item) is { } icon)
            InlineIcon(icon);

        var name = ChunkUtil.ToChunks(item.Name.ToDalamudString(), ChunkSource.None, null);
        LogWindow.DrawChunks(name.ToList());
        ImGui.Separator();

        var help = EventItemHelpSheet.GetRow(payload.RawItemId);
        if (help != null)
        {
            var desc = ChunkUtil.ToChunks(help.Description.ToDalamudString(), ChunkSource.None, null);
            LogWindow.DrawChunks(desc.ToList());
        }
    }

    private void HoverURI(URIPayload uri)
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
                LogWindow.Plugin.Common.Functions.Journal.OpenQuest(quest.Quest);
                break;
            case DalamudLinkPayload link:
                ClickLinkPayload(chunk, payload, link);
                break;
            case DalamudPartyFinderPayload pf:
                if (pf.LinkType == DalamudPartyFinderPayload.PartyFinderLinkType.PartyFinderNotification)
                    GameFunctions.GameFunctions.OpenPartyFinder();
                else
                    LogWindow.Plugin.Functions.OpenPartyFinder(pf.ListingId);
                break;
            case ChatTwoPartyFinderPayload pf:
                LogWindow.Plugin.Functions.OpenPartyFinder(pf.Id);
                break;
            case AchievementPayload achievement:
                LogWindow.Plugin.Functions.OpenAchievement(achievement.Id);
                break;
            case RawPayload raw:
                if (Equals(raw, ChunkUtil.PeriodicRecruitmentLink))
                    GameFunctions.GameFunctions.OpenPartyFinder();
                break;
            case URIPayload uri:
                TryOpenURI(uri.Uri);
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

        var item = ItemSheet.GetRow(payload.ItemId);
        if (item == null)
            return;

        var hq = payload.Kind == ItemPayload.ItemKind.Hq;
        if (LogWindow.Plugin.TextureCache.GetItem(item, hq) is { } icon)
            InlineIcon(icon);

        var name = item.Name.ToDalamudString();
        // hq symbol
        if (hq)
            name.Payloads.Add(new TextPayload(" "));
        else if (payload.Kind == ItemPayload.ItemKind.Collectible)
            name.Payloads.Add(new TextPayload(" "));

        LogWindow.DrawChunks(ChunkUtil.ToChunks(name, ChunkSource.None, null).ToList(), false);
        ImGui.Separator();

        var realItemId = payload.RawItemId;
        if (item.EquipSlotCategory.Row != 0)
        {
            if (ImGui.Selectable(Language.Context_TryOn))
                LogWindow.Plugin.Functions.Context.TryOn(realItemId, 0);

            if (ImGui.Selectable(Language.Context_ItemComparison))
                LogWindow.Plugin.Functions.Context.OpenItemComparison(realItemId);
        }

        if (item.ItemSearchCategory.Value?.Category == 3)
            if (ImGui.Selectable(Language.Context_SearchRecipes))
                LogWindow.Plugin.Functions.Context.SearchForRecipesUsingItem(payload.ItemId);

        if (ImGui.Selectable(Language.Context_SearchForItem))
            LogWindow.Plugin.Functions.Context.SearchForItem(realItemId);

        if (ImGui.Selectable(Language.Context_Link))
            LogWindow.Plugin.Functions.Context.LinkItem(realItemId);

        if (ImGui.Selectable(Language.Context_CopyItemName))
            ImGui.SetClipboardText(name.TextValue);
    }

    private void DrawEventItemPopup(ItemPayload payload)
    {
        if (payload.Kind != ItemPayload.ItemKind.EventItem)
            return;

        var item = EventItemSheet.GetRow(payload.ItemId);
        if (item == null)
            return;

        if (LogWindow.Plugin.TextureCache.GetEventItem(item) is { } icon)
            InlineIcon(icon);

        var name = item.Name.ToDalamudString();
        LogWindow.DrawChunks(ChunkUtil.ToChunks(name, ChunkSource.None, null).ToList(), false);
        ImGui.Separator();

        var realItemId = payload.RawItemId;
        if (ImGui.Selectable(Language.Context_Link))
            LogWindow.Plugin.Functions.Context.LinkItem(realItemId);

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
            if (Plugin.ClientState.LocalPlayer?.HomeWorld.GameData is { } homeWorld)
                world = homeWorld;

        var name = new List<Chunk> { new TextChunk(ChunkSource.None, null, player.PlayerName) };
        if (world.IsPublic)
        {
            name.AddRange(new Chunk[]
            {
                new IconChunk(ChunkSource.None, null, BitmapFontIcon.CrossWorld),
                new TextChunk(ChunkSource.None, null, world.Name),
            });
        }

        LogWindow.DrawChunks(name, false);
        ImGui.Separator();

        var validContentId = chunk.Message?.ContentId is not (null or 0);
        if (ImGui.Selectable(Language.Context_SendTell))
        {
            // Eureka and Bozja need special handling as tells work different
            if (TerritorySheet.GetRow(Plugin.ClientState.TerritoryType)?.TerritoryIntendedUse != 41)
            {
                LogWindow.Chat = $"/tell {player.PlayerName}";
                if (world.IsPublic)
                    LogWindow.Chat += $"@{world.Name}";

                LogWindow.Chat += " ";
            }
            else if (validContentId)
            {
                LogWindow.Plugin.Functions.Chat.SetEurekaTellChannel(player.PlayerName, world.Name.ToString(), (ushort) world.RowId, chunk.Message!.ContentId, 0, 0);
            }

            LogWindow.Activate = true;
        }

        if (world.IsPublic)
        {
            var party = Plugin.PartyList;
            var leader = (ulong?) party[(int) party.PartyLeaderIndex]?.ContentId;
            var isLeader = party.Length == 0 || Plugin.ClientState.LocalContentId == leader;
            var member = party.FirstOrDefault(member => member.Name.TextValue == player.PlayerName && member.World.Id == world.RowId);
            var isInParty = member != default;
            var inInstance = LogWindow.Plugin.Functions.IsInInstance();
            var inPartyInstance = TerritorySheet.GetRow(Plugin.ClientState.TerritoryType)?.TerritoryIntendedUse is (41 or 47 or 48 or 52 or 53);
            if (isLeader)
            {
                if (!isInParty)
                {
                    if (inInstance && inPartyInstance)
                    {
                        if (validContentId && ImGui.Selectable(Language.Context_InviteToParty))
                            LogWindow.Plugin.Functions.Party.InviteInInstance(chunk.Message!.ContentId);
                    }
                    else if (!inInstance && ImGui.BeginMenu(Language.Context_InviteToParty))
                    {
                        if (ImGui.Selectable(Language.Context_InviteToParty_SameWorld))
                            LogWindow.Plugin.Functions.Party.InviteSameWorld(player.PlayerName, (ushort) world.RowId, chunk.Message?.ContentId ?? 0);

                        if (validContentId && ImGui.Selectable(Language.Context_InviteToParty_DifferentWorld))
                            LogWindow.Plugin.Functions.Party.InviteOtherWorld(chunk.Message!.ContentId);

                        ImGui.EndMenu();
                    }
                }

                if (isInParty && member != null && (!inInstance || (inInstance && inPartyInstance)))
                {
                    if (ImGui.Selectable(Language.Context_Promote))
                        LogWindow.Plugin.Functions.Party.Promote(player.PlayerName, (ulong) member.ContentId);

                    if (ImGui.Selectable(Language.Context_KickFromParty))
                        LogWindow.Plugin.Functions.Party.Kick(player.PlayerName, (ulong) member.ContentId);
                }
            }

            var isFriend = LogWindow.Plugin.Common.Functions.FriendList.List.Any(friend => friend.Name.TextValue == player.PlayerName && friend.HomeWorld == world.RowId);
            if (!isFriend && ImGui.Selectable(Language.Context_SendFriendRequest))
                LogWindow.Plugin.Functions.SendFriendRequest(player.PlayerName, (ushort) world.RowId);

            if (ImGui.Selectable(Language.Context_AddToBlacklist))
                LogWindow.Plugin.Functions.AddToBlacklist(player.PlayerName, (ushort) world.RowId);

            if (LogWindow.Plugin.Functions.IsMentor() && ImGui.Selectable(Language.Context_InviteToNoviceNetwork))
                LogWindow.Plugin.Functions.Context.InviteToNoviceNetwork(player.PlayerName, (ushort) world.RowId);
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
            if (!LogWindow.Plugin.Functions.TryOpenAdventurerPlate(chunk.Message!.ContentId))
                WrapperUtil.AddNotification(Language.Context_AdventurerPlateError, NotificationType.Warning);

        // View Party Finder 0x2E
    }

    private PlayerCharacter? FindCharacterForPayload(PlayerPayload payload)
    {
        foreach (var obj in Plugin.ObjectTable)
        {
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

    private void DrawUriPopup(URIPayload uri)
    {
        ImGui.TextUnformatted(string.Format(Language.Context_URLDomain, uri.Uri.Authority));
        ImGuiUtil.WarningText(Language.Context_URLWarning, false);
        ImGui.Separator();

        if (ImGui.Selectable(Language.Context_OpenInBrowser))
            TryOpenURI(uri.Uri);

        if (ImGui.Selectable(Language.Context_CopyLink))
        {
            ImGui.SetClipboardText(uri.Uri.ToString());
            WrapperUtil.AddNotification(Language.Context_CopyLinkNotification, NotificationType.Info);
        }
    }

    private void TryOpenURI(Uri uri)
    {
        try
        {
            Plugin.Log.Debug($"Opening URI {uri} in default browser");
            Dalamud.Utility.Util.OpenLink(uri.ToString());
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Error opening URI: {ex}");
            WrapperUtil.AddNotification(Language.Context_OpenInBrowserError, NotificationType.Error);
        }
    }
}
