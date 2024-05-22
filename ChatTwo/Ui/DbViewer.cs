using System.Collections.Concurrent;
using System.Globalization;
using System.Numerics;
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

    private bool IsProcessing;
    private long ProcessingStart = Environment.TickCount64;
    private (DateTime Min, DateTime Max, int Page, bool Local) LastProcessed;

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

        DateFormat = CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern;
        DateTimeFormat = CultureInfo.CurrentCulture.DateTimeFormat.FullDateTimePattern;

        LastProcessed = (AfterDate, BeforeDate, CurrentPage, OnlyCurrentCharacter);
        DateReset();

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(475, 600),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        RespectCloseHotkey = false;
        DisableWindowSounds = true;

        Plugin.Commands.Register("/chat2Viewer").Execute += Toggle;
    }

    public void Dispose()
    {
        Plugin.Commands.Register("/chat2Viewer").Execute -= Toggle;
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

        if (!IsProcessing && LastProcessed != (AfterDate, BeforeDate, CurrentPage, OnlyCurrentCharacter))
        {
            // Page hasn't changed, so we reset it back to 1
            if (LastProcessed.Page == CurrentPage)
                CurrentPage = 1;

            AdjustDates();
            IsProcessing = true;
            ProcessingStart = Environment.TickCount64 + 1_000; // + 1 second
            LastProcessed = (AfterDate, BeforeDate, CurrentPage, OnlyCurrentCharacter);
            Task.Run(() =>
            {
                try
                {
                    ulong? character = OnlyCurrentCharacter ? Plugin.ClientState.LocalContentId : null;

                    // We only want to fetch count if this is the first page
                    if (CurrentPage == 1)
                        Count = Plugin.MessageManager.Store.CountDateRange(AfterDate, BeforeDate, character);
                    Messages = Plugin.MessageManager.Store.GetDateRange(AfterDate, BeforeDate, character, CurrentPage - 1).ToArray();

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

        using var table = ImRaii.Table("##messageHistory", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable);
        if (!table.Success)
            return;

        ImGui.TableSetupColumn(Language.DbViewer_TableField_Date, ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn(Language.DbViewer_TableField_Sender);
        ImGui.TableSetupColumn(Language.DbViewer_TableField_Content);

        ImGui.TableHeadersRow();
        foreach (var message in Filtered)
        {
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(message.Date.ToLocalTime().ToString(DateTimeFormat));

            ImGui.TableNextColumn();
            Plugin.ChatLogWindow.DrawChunks(message.Sender);

            ImGui.TableNextColumn();
            Plugin.ChatLogWindow.DrawChunks(message.Content);
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
