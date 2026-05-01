using System.Collections.Concurrent;
using System.Globalization;
using System.Numerics;
using System.Text;
using ChatTwo.Code;
using ChatTwo.Http.MessageProtocol;
using ChatTwo.Resources;
using ChatTwo.Util;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiNotification;
using Lumina.Data.Files;
using Lumina.Text.ReadOnly;
using MoreLinq;
using Newtonsoft.Json;

namespace ChatTwo.Ui;

public class DbViewer : Window
{
    public const float RowPerPage = 1000.0f;

    private readonly Plugin Plugin;

    private static readonly DateTime MinimalDate = new(2021, 1, 1);

    private DateTime AfterDate;
    private DateTime BeforeDate;

    private int CurrentPage = 1;
    private string SimpleSearchTerm = "";
    private bool OnlyCurrentCharacter = true;
    private readonly Dictionary<ChatType, (ChatSource, ChatSource)> SelectedChannels;

    private bool IsProcessing;
    private long ProcessingStart = Environment.TickCount64;
    private (DateTime Min, DateTime Max, int Page, bool Local, int ChannelCount) LastProcessed;

    private string MinDateString = "";
    private string MaxDateString = "";

    private readonly string DateFormat;
    private readonly string DateTimeFormat;

    private long Count;
    private Message[] Messages = [];  // Messages are only touched while processing is false
    private ConcurrentStack<Message> Filtered = [];  // Is used every frame, so ConcurrentStack for safety

    private bool IsExporting;
    private string InputPath = string.Empty;
    private IActiveNotification Notification = null!;

    private bool NeedsScrollReset;

    public DbViewer(Plugin plugin) : base("DBViewer###chat2-dbviewer")
    {
        Plugin = plugin;
        SelectedChannels = TabsUtil.MostlyPlayer;

        DateFormat = CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern;
        DateTimeFormat = "ddd, dd MMM yyy HH:mm:ss";

        LastProcessed = (AfterDate, BeforeDate, CurrentPage, OnlyCurrentCharacter, SelectedChannels.Count);
        DateReset();

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(475, 600),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        RespectCloseHotkey = false;
        DisableWindowSounds = true;

        Plugin.Commands.Register("/chat2Viewer", "Get access to your message history, with simple filter options.", true).Execute += Toggle;
    }

    public void Dispose()
    {
        Plugin.Commands.Register("/chat2Viewer", "Get access to your message history, with simple filter options.", true).Execute -= Toggle;
    }

    private void Toggle(string _, string __) => Toggle();

    public override void Draw()
    {
        var totalPages = (int)Math.Ceiling(Count / RowPerPage);
        if (totalPages < 1)
            totalPages = 1;

        if (CurrentPage > totalPages)
            CurrentPage = 1;

        // First row

        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(ImGuiColors.DalamudViolet, Language.DbViewer_DatePicker_FromTo);
        ImGui.SameLine();

        var spacing = 3.0f * ImGuiHelpers.GlobalScale;
        DateWidget.DatePickerWithInput("##FromDate", 1, ref MinDateString, ref AfterDate, DateFormat);
        DateWidget.DatePickerWithInput("##ToDate", 2, ref MaxDateString, ref BeforeDate, DateFormat, true);

        ImGui.SameLine(0, spacing);

        if (ImGuiUtil.IconButton(FontAwesomeIcon.Recycle))
            DateReset();

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Language.DbViewer_Date_Reset_Tooltip);

        ImGui.SameLine(0, spacing);

        ChannelSelection();

        var skipText = Language.DbViewer_CharacterOption;
        var textLength = ImGui.GetTextLineHeight() + ImGui.CalcTextSize(skipText).X + ImGui.GetStyle().ItemInnerSpacing.X + ImGui.GetStyle().FramePadding.X * 2;
        ImGui.SameLine(ImGui.GetContentRegionMax().X - textLength);
        ImGui.Checkbox(skipText, ref OnlyCurrentCharacter);

        // Second row

        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(ImGuiColors.DalamudViolet, "Export:");
        ImGui.SameLine(0, spacing);
        ImGui.SetNextItemWidth(ImGui.GetWindowWidth() * 0.4f);
        ImGui.InputText("##InputPath", ref InputPath, 255);
        ImGui.SameLine(0, spacing);
        if (ImGuiUtil.IconButton(FontAwesomeIcon.FolderClosed, "##folderPicker"))
            ImGui.OpenPopup("InputPathDialog");

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(Language.Folder_Export_Location_Tooltip);

        using (var innerPopup = ImRaii.Popup("InputPathDialog"))
        {
            if (innerPopup.Success)
                Plugin.FileDialogManager.OpenFolderDialog(Language.Folder_Selection_Header, (b, s) => { if (b) InputPath = s; }, null, true);
        }

        ImGui.SameLine(0, spacing);
        using (ImRaii.Disabled(InputPath.Length == 0 || IsExporting))
        {
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Save))
            {
                Notification = Plugin.Notification.AddNotification(
                    new Notification
                    {
                        Title = "Chat2 Text Export",
                        Content = Language.ChatExport_Initial,
                        Type = NotificationType.Info,
                        Minimized = false,
                        UserDismissable = false,
                        InitialDuration = TimeSpan.FromSeconds(10000),
                        Progress = 0.0f,
                    });
                CreateTxtBackup();
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(Language.Export_Txt_Tooltip);

        ImGui.SameLine(0, spacing);
        using (ImRaii.Disabled(InputPath.Length == 0 || IsExporting))
        {
            if (ImGuiUtil.IconButton(FontAwesomeIcon.FileExport))
            {
                Notification = Plugin.Notification.AddNotification(
                    new Notification
                    {
                        Title = "Chat2 Json Export",
                        Content = Language.ChatExport_Initial,
                        Type = NotificationType.Info,
                        Minimized = false,
                        UserDismissable = false,
                        InitialDuration = TimeSpan.FromSeconds(10000),
                        Progress = 0.0f,
                    });
                CreateTempJsonFile();
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(Language.Export_Json_Tooltip);

        var width = 350 * ImGuiHelpers.GlobalScale;
        var loadingIndicator = IsProcessing && ProcessingStart < Environment.TickCount64;

        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(string.Format(Language.DbViewer_Page, CurrentPage, totalPages, Count, loadingIndicator ? Language.DbViewer_LoadingIndicator : ""));
        ImGuiUtil.DrawArrows(ref CurrentPage, 1, totalPages, spacing, tooltipLeft: Language.Page_ArrowLeft_Tooltip, tooltipRight: Language.Page_ArrowRight_Tooltip);

        ImGui.SameLine(ImGui.GetContentRegionMax().X - width);
        ImGui.SetNextItemWidth(width);
        if (ImGui.InputTextWithHint("##searchbar", Language.DbViewer_SearcHint, ref SimpleSearchTerm, 30))
            Filtered = Filter(Messages);

        // Third row

        if (DateWidget.Validate(MinimalDate, ref AfterDate, ref BeforeDate))
            DateRefresh();

        if (!IsProcessing && LastProcessed != (AfterDate, BeforeDate, CurrentPage, OnlyCurrentCharacter, SelectedChannels.Count))
        {
            // Page hasn't changed, so we reset it back to 1
            if (LastProcessed.Page == CurrentPage)
                CurrentPage = 1;

            AdjustDates();
            IsProcessing = true;
            ProcessingStart = Environment.TickCount64 + 1_000; // + 1 second
            LastProcessed = (AfterDate, BeforeDate, CurrentPage, OnlyCurrentCharacter, SelectedChannels.Count);
            Task.Run(() =>
            {
                try
                {
                    ulong? character = OnlyCurrentCharacter ? Plugin.PlayerState.ContentId : null;
                    var channels = SelectedChannels.Select(pair => (byte) pair.Key).ToArray();

                    // We only want to fetch count if this is the first page
                    if (CurrentPage == 1)
                        Count = Plugin.MessageManager.Store.CountDateRange(AfterDate, BeforeDate, channels, character);

                    using var rangeMessageEnumerator = Plugin.MessageManager.Store.GetPagedDateRange(AfterDate, BeforeDate, channels, character, CurrentPage - 1);
                    Messages = rangeMessageEnumerator.ToArray();

                    Filtered = Filter(Messages);
                    NeedsScrollReset = true;
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error(ex, "Failed reading messages from database");
                }
                finally
                {
                    IsProcessing = false;
                }
            });
        }

        ImGuiHelpers.ScaledDummy(5.0f);

        if (Filtered.IsEmpty)
        {
            ImGui.TextUnformatted(SimpleSearchTerm == "" ? Language.DbViewer_Status_NothingFound : Language.DbViewer_Status_NoSearchResult);
            return;
        }

        using var child = ImRaii.Child("##tableChild");
        if (!child.Success)
            return;

        if (NeedsScrollReset)
        {
            NeedsScrollReset = false;
            ImGui.SetScrollY(0.0f);
        }

        using var table = ImRaii.Table("##messageHistory", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable);
        if (!table.Success)
            return;

        var columnWidth = ImGui.CalcTextSize(Language.DbViewer_TableField_Type);
        ImGui.TableSetupColumn(Language.DbViewer_TableField_Date, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize);
        ImGui.TableSetupColumn(Language.DbViewer_TableField_Type, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize, columnWidth.X);
        ImGui.TableSetupColumn(Language.DbViewer_TableField_Sender);
        ImGui.TableSetupColumn(Language.DbViewer_TableField_Content);

        ImGui.TableHeadersRow();
        foreach (var message in Filtered)
        {
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(message.Date.ToLocalTime().ToString(DateTimeFormat));

            ImGui.TableNextColumn();
            var pos = ImGui.GetCursorPos();
            ImGuiUtil.CenterText($"{(byte)message.Code.Type}");
            ImGui.SetCursorPos(pos);
            ImGui.Dummy(columnWidth);
            if (ImGui.IsItemHovered())
                ImGuiUtil.Tooltip(message.Code.Type.Name());

            ImGui.TableNextColumn();
            Plugin.ChatLogWindow.DrawChunks(message.Sender);

            ImGui.TableNextColumn();
            Plugin.ChatLogWindow.DrawChunks(message.Content);
        }
    }

    private void ChannelSelection()
    {
        const string addTabPopup = "add-channel-popup";
        var spacing = 3.0f * ImGuiHelpers.GlobalScale;

        if (ImGui.Button("Channels"))
            ImGui.OpenPopup(addTabPopup);

        using var popup = ImRaii.Popup(addTabPopup);
        if (!popup.Success)
            return;

        using var channelNode = ImRaii.TreeNode(Language.Options_Tabs_Channels);
        if (!channelNode.Success)
            return;

        foreach (var (header, types) in ChatTypeExt.SortOrder)
        {
            using var pushedId = ImRaii.PushId(header);

            if (ImGuiComponents.IconButton(FontAwesomeIcon.Check))
            {
                foreach (var type in types)
                    SelectedChannels.TryAdd(type, (ChatSourceExt.All, ChatSourceExt.All));
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Select all");

            ImGui.SameLine(0, spacing);

            if (ImGuiComponents.IconButton(FontAwesomeIcon.Times))
            {
                foreach (var type in types)
                    SelectedChannels.Remove(type);
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Unselect all");

            ImGui.SameLine(0, spacing);

            using var headerNode = ImRaii.TreeNode(header);
            if (!headerNode.Success)
                continue;

            foreach (var type in types)
            {
                if (type.IsGm())
                    continue;

                var enabled = SelectedChannels.ContainsKey(type);
                if (ImGui.Checkbox($"##{type.Name()}", ref enabled))
                {
                    if (enabled)
                        SelectedChannels[type] = (ChatSourceExt.All, ChatSourceExt.All);
                    else
                        SelectedChannels.Remove(type);
                }

                ImGui.SameLine();
                ImGui.TextUnformatted(type.Name());
            }
        }
    }

    private ConcurrentStack<Message> Filter(Message[] messages)
    {
        if (SimpleSearchTerm == "")
            return new ConcurrentStack<Message>(messages.Reverse().OrderByDescending(m => m.Date));

        return new ConcurrentStack<Message>(
            messages.Reverse().Where(m =>
                ChunkUtil.ToRawString(m.Sender).Contains(SimpleSearchTerm, StringComparison.InvariantCultureIgnoreCase) ||
                ChunkUtil.ToRawString(m.Content).Contains(SimpleSearchTerm, StringComparison.InvariantCultureIgnoreCase)
                ).OrderByDescending(m => m.Date));
    }

    private void DateRefresh()
    {
        MinDateString = AfterDate.ToString(DateFormat);
        MaxDateString = BeforeDate.ToString(DateFormat);
    }

    private void AdjustDates()
    {
        AfterDate = new DateTime(AfterDate.Year, AfterDate.Month, AfterDate.Day, 0, 0, 0);
        BeforeDate = new DateTime(BeforeDate.Year, BeforeDate.Month, BeforeDate.Day, 23, 59, 59);
    }

    private void DateReset()
    {
        AfterDate = DateTime.Now.AddDays(-5);
        BeforeDate = DateTime.Now;

        AdjustDates();
        DateRefresh();
    }

    private void CreateTxtBackup()
    {
        IsExporting = true;
        Task.Run(async () =>
        {
            try
            {
                ulong? character = OnlyCurrentCharacter ? Plugin.PlayerState.ContentId : null;
                var channels = SelectedChannels.Select(pair => (byte)pair.Key).ToArray();

                var rangeMessageEnumerator = Plugin.MessageManager.Store.GetDateRange(AfterDate, BeforeDate, channels, character);
                var messageHistory = rangeMessageEnumerator.ToArray();
                await rangeMessageEnumerator.DisposeAsync();

                var filteredHistory = Filter(messageHistory);

                var sb = new StringBuilder();
                await using var stream = new StreamWriter(Path.Join(InputPath, $"Chat2_{DateTime.Now:yyyy_dd_M__HH_mm_ss}.txt"));

                var batch = 0;
                foreach (var messages in filteredHistory.Batch(5000))
                {
                    await Plugin.Framework.RunOnTick(() =>
                    {
                        foreach (var message in messages)
                        {
                            if (!Sheets.LogKindSheet.TryGetRow((uint)message.Code.Type, out var logKind))
                                logKind = Sheets.LogKindSheet.GetRow(10); // default to say

                            var rossSender = new ReadOnlySeString(message.SenderSource.Encode());
                            var rossMessage = new ReadOnlySeString(message.ContentSource.Encode());

                            var timestamp = message.Date.ToLocalTime().ToString(DateTimeFormat);
                            var text = Plugin.Evaluator.Evaluate(logKind.Format, [rossSender, rossMessage]).ToString();
                            sb.AppendLine($"[{timestamp}][{message.Code.Type.Name()}]   {text}");

                            batch++;
                        }
                    }, delayTicks: 5);

                    Notification.Progress = (float)batch / filteredHistory.Count;
                    Notification.Content = $"Exported {batch} of {filteredHistory.Count} messages";
                    await stream.WriteAsync(sb.ToString());
                    sb.Clear();
                }

                await stream.WriteAsync(sb.ToString());
                sb.Clear();

                Notification.Progress = 1.0f;
                Notification.Content = "Done!!!";
                Notification.Type = NotificationType.Success;
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Failed creating txt backup");

                Notification.Content = "Error ...";
                Notification.Type = NotificationType.Error;
            }
            finally
            {
                IsExporting = false;
                Notification.UserDismissable = true;
            }
        });
    }

    private void CreateTempJsonFile()
    {
        IsExporting = true;
        Task.Run(async () =>
        {
            try
            {
                var channels = SelectedChannels.Select(pair => (byte)pair.Key).ToArray();

                var rangeMessageEnumerator = Plugin.MessageManager.Store.GetDateRange(AfterDate, BeforeDate, channels);
                var messageHistory = rangeMessageEnumerator.ToArray();
                await rangeMessageEnumerator.DisposeAsync();

                var filteredHistory = Filter(messageHistory);

                await using var stream = new StreamWriter(Path.Join(InputPath, $"Chat2_{DateTime.Now:yyyy_dd_M__HH_mm_ss}.json"));

                var batch = 0;
                var messageContainer = new Messages();
                List<MessageResponse> templates = [];
                foreach (var messages in filteredHistory.Batch(5000))
                {
                    foreach (var message in messages)
                    {
                        templates.Add(ReadMessageContent(message));
                        batch++;
                    }

                    Notification.Progress = (float)batch / filteredHistory.Count;
                    Notification.Content = $"Exported {batch} of {filteredHistory.Count} messages";

                    await Task.Delay(100);
                }

                messageContainer.Set = templates.ToArray();
                await stream.WriteAsync(JsonConvert.SerializeObject(messageContainer));
                templates.Clear();

                await using (var fileStream = File.Open(Path.Join(InputPath, "gfdata.gfd"), FileMode.OpenOrCreate))
                {
                    await using var byteWriter = new BinaryWriter(fileStream);
                    byteWriter.Write(Plugin.DataManager.GetFile("common/font/gfdata.gfd")!.Data);
                }

                await using (var fileStream = File.Open(Path.Join(InputPath, "fonticon_ps5.tex"), FileMode.OpenOrCreate))
                {
                    await using var byteWriter = new BinaryWriter(fileStream);
                    byteWriter.Write(Plugin.DataManager.GetFile<TexFile>("common/font/fonticon_ps5.tex")!.Data);
                }

                await using (var fileStream = File.Open(Path.Join(InputPath, "FFXIV_Lodestone_SSF.ttf"), FileMode.OpenOrCreate))
                {
                    await using var byteWriter = new BinaryWriter(fileStream);
                    byteWriter.Write(Plugin.FontManager.GameSymFont);
                }

                Notification.Progress = 1.0f;
                Notification.Content = "Done!!!";
                Notification.Type = NotificationType.Success;
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Failed creating txt backup");

                Notification.Content = "Error ...";
                Notification.Type = NotificationType.Error;
            }
            finally
            {
                IsExporting = false;
                Notification.UserDismissable = true;
            }
        });
    }

    private MessageResponse ReadMessageContent(Message message)
    {
        var response = new MessageResponse
        {
            Id = message.Id,
            Timestamp = message.Date.ToLocalTime().ToString("t", !Plugin.Config.Use24HourClock ? null : CultureInfo.CreateSpecificCulture("es-ES"))
        };

        var sender = message.Sender.Select(ProcessChunk);
        var content = message.Content.Select(ProcessChunk);
        response.Templates = sender.Concat(content).ToArray();

        return response;
    }

    private MessageTemplate ProcessChunk(Chunk chunk)
    {
        if (chunk is IconChunk { } icon)
        {
            var iconId = (uint)icon.Icon;
            return IconUtil.GfdFileView.TryGetEntry(iconId, out _) ? new MessageTemplate {PayloadType = WebPayloadType.Icon, IconId = iconId}: MessageTemplate.Empty;
        }

        if (chunk is TextChunk { } text)
        {
            if (chunk.Link is EmotePayload emotePayload && Plugin.Config.ShowEmotes)
            {
                var image = EmoteCache.GetEmote(emotePayload.Code);

                if (image is { Failed: false })
                    return new MessageTemplate { PayloadType = WebPayloadType.CustomEmote, Color = 0, Content = emotePayload.Code };
            }

            var color = text.Foreground;
            if (color == null && text.FallbackColour != null)
            {
                var type = text.FallbackColour.Value;
                color = Plugin.Config.ChatColours.TryGetValue(type, out var col) ? col : type.DefaultColor();
            }

            color ??= 0;

            var userContent = text.Content;
            if (Plugin.ChatLogWindow.ScreenshotMode)
            {
                if (chunk.Link is PlayerPayload playerPayload)
                    userContent = Plugin.ChatLogWindow.HidePlayerInString(userContent, playerPayload.PlayerName, playerPayload.World.RowId);
                else if (Plugin.PlayerState.IsLoaded)
                    userContent = Plugin.ChatLogWindow.HidePlayerInString(userContent, Plugin.PlayerState.CharacterName, Plugin.PlayerState.HomeWorld.RowId);
            }

            var isNotUrl = text.Link is not UriPayload;
            return new MessageTemplate { PayloadType = isNotUrl ? WebPayloadType.RawText : WebPayloadType.CustomUri, Color = color.Value, Content = userContent };
        }

        return MessageTemplate.Empty;
    }
}
