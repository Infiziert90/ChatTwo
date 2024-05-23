using System.Collections.Concurrent;
using System.Globalization;
using System.Numerics;
using ChatTwo.Code;
using ChatTwo.Resources;
using ChatTwo.Util;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace ChatTwo.Ui;

public class DbViewer : Window
{
    private readonly Plugin Plugin;

    private static readonly DateTime MinimalDate = new(2021, 1, 1);

    private DateTime AfterDate;
    private DateTime BeforeDate;

    private int CurrentPage = 1;
    private string SimpleSearchTerm = "";
    private bool OnlyCurrentCharacter = true;
    private readonly Dictionary<ChatType, ChatSource> ChatCodes;

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

    public DbViewer(Plugin plugin) : base("DBViewer###chat2-dbviewer")
    {
        Plugin = plugin;
        ChatCodes = TabsUtil.MostlyPlayer;

        DateFormat = CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern;
        DateTimeFormat = CultureInfo.CurrentCulture.DateTimeFormat.FullDateTimePattern;

        LastProcessed = (AfterDate, BeforeDate, CurrentPage, OnlyCurrentCharacter, ChatCodes.Count);
        DateReset();

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(475, 600),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        RespectCloseHotkey = false;
        DisableWindowSounds = true;

        Plugin.Commands.Register("/chat2Viewer", "Database Viewer", true).Execute += Toggle;
    }

    public void Dispose()
    {
        Plugin.Commands.Register("/chat2Viewer", "Database Viewer", true).Execute -= Toggle;
    }

    private void Toggle(string _, string __) => Toggle();

    public override void Draw()
    {
        var totalPages = (int)Math.Ceiling(Count / 500.0f);
        if (totalPages < 1)
            totalPages = 1;

        if (CurrentPage > totalPages)
            CurrentPage = 1;

        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(ImGuiColors.DalamudViolet, Language.DbViewer_DatePicker_FromTo);
        ImGui.SameLine();

        var spacing = 3.0f * ImGuiHelpers.GlobalScale;
        DateWidget.DatePickerWithInput("##FromDate", 1, ref MinDateString, ref AfterDate, DateFormat);
        DateWidget.DatePickerWithInput("##ToDate", 2, ref MaxDateString, ref BeforeDate, DateFormat, true);
        ImGui.SameLine(0, spacing);
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Recycle))
            DateReset();
        ImGuiUtil.DrawArrows(ref CurrentPage, 1, totalPages, spacing);
        ImGui.SameLine(0, spacing);
        ChannelSelection();

        var skipText = Language.DbViewer_CharacterOption;
        var textLength = ImGui.GetTextLineHeight() + ImGui.CalcTextSize(skipText).X + ImGui.GetStyle().ItemInnerSpacing.X + ImGui.GetStyle().FramePadding.X * 2;
        ImGui.SameLine(ImGui.GetContentRegionMax().X - textLength);
        ImGui.Checkbox(skipText, ref OnlyCurrentCharacter);

        var width = 350 * ImGuiHelpers.GlobalScale;
        var loadingIndicator = IsProcessing && ProcessingStart < Environment.TickCount64;

        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(string.Format(Language.DbViewer_Page, CurrentPage, totalPages, Count, loadingIndicator ? Language.DbViewer_LoadingIndicator : ""));
        ImGui.SameLine(ImGui.GetContentRegionMax().X - width);
        ImGui.SetNextItemWidth(width);
        if (ImGui.InputTextWithHint("##searchbar", Language.DbViewer_SearcHint, ref SimpleSearchTerm, 30))
            Filter();

        if (DateWidget.Validate(MinimalDate, ref AfterDate, ref BeforeDate))
            DateRefresh();

        if (!IsProcessing && LastProcessed != (AfterDate, BeforeDate, CurrentPage, OnlyCurrentCharacter, ChatCodes.Count))
        {
            // Page hasn't changed, so we reset it back to 1
            if (LastProcessed.Page == CurrentPage)
                CurrentPage = 1;

            AdjustDates();
            IsProcessing = true;
            ProcessingStart = Environment.TickCount64 + 1_000; // + 1 second
            LastProcessed = (AfterDate, BeforeDate, CurrentPage, OnlyCurrentCharacter, ChatCodes.Count);
            Task.Run(() =>
            {
                try
                {
                    ulong? character = OnlyCurrentCharacter ? Plugin.ClientState.LocalContentId : null;
                    var channels = ChatCodes.Select(c => (uint) c.Key).ToArray();

                    // We only want to fetch count if this is the first page
                    if (CurrentPage == 1)
                        Count = Plugin.MessageManager.Store.CountDateRange(AfterDate, BeforeDate, channels, character);
                    Messages = Plugin.MessageManager.Store.GetDateRange(AfterDate, BeforeDate, channels, character, CurrentPage - 1).ToArray();

                    Filter();
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
            ImGuiUtil.CenterText($"{message.Code.Raw}");
            ImGui.SetCursorPos(pos);
            ImGui.Dummy(columnWidth);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(message.Code.Type.Name());

            ImGui.TableNextColumn();
            Plugin.ChatLogWindow.DrawChunks(message.Sender);

            ImGui.TableNextColumn();
            Plugin.ChatLogWindow.DrawChunks(message.Content);
        }
    }

    private void ChannelSelection()
    {
        const string addTabPopup = "add-channel-popup";

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
            using var headerNode = ImRaii.TreeNode(header);
            if (!headerNode.Success)
                continue;

            foreach (var type in types)
            {
                if (type.IsGm())
                    continue;

                var enabled = ChatCodes.ContainsKey(type);
                if (ImGui.Checkbox($"##{type.Name()}", ref enabled))
                {
                    if (enabled)
                        ChatCodes[type] = ChatSourceExt.All;
                    else
                        ChatCodes.Remove(type);
                }

                ImGui.SameLine();
                ImGui.TextUnformatted(type.Name());
            }
        }
    }

    private void Filter()
    {
        if (SimpleSearchTerm == "")
        {
            Filtered = new ConcurrentStack<Message>(Messages.Reverse());
            return;
        }

        Filtered = new ConcurrentStack<Message>(
            Messages.Reverse().Where(m =>
                ChunkUtil.ToRawString(m.Sender).Contains(SimpleSearchTerm, StringComparison.InvariantCultureIgnoreCase) ||
                ChunkUtil.ToRawString(m.Content).Contains(SimpleSearchTerm, StringComparison.InvariantCultureIgnoreCase)
                ));
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
}
