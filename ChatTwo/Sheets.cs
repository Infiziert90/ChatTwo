using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace ChatTwo;

public static class Sheets
{
    public static readonly ExcelSheet<Item> ItemSheet;
    public static readonly ExcelSheet<World> WorldSheet;
    public static readonly ExcelSheet<Status> StatusSheet;
    public static readonly ExcelSheet<LogFilter> LogFilterSheet;
    public static readonly ExcelSheet<EventItem> EventItemSheet;
    public static readonly ExcelSheet<Completion> CompletionSheet;
    public static readonly ExcelSheet<TerritoryType> TerritorySheet;
    public static readonly ExcelSheet<TextCommand> TextCommandSheet;
    public static readonly ExcelSheet<EventItemHelp> EventItemHelpSheet;

    static Sheets()
    {
        ItemSheet = Plugin.DataManager.GetExcelSheet<Item>();
        WorldSheet = Plugin.DataManager.GetExcelSheet<World>();
        StatusSheet = Plugin.DataManager.GetExcelSheet<Status>();
        EventItemSheet = Plugin.DataManager.GetExcelSheet<EventItem>();
        LogFilterSheet = Plugin.DataManager.GetExcelSheet<LogFilter>();
        CompletionSheet = Plugin.DataManager.GetExcelSheet<Completion>();
        TerritorySheet = Plugin.DataManager.GetExcelSheet<TerritoryType>();
        TextCommandSheet = Plugin.DataManager.GetExcelSheet<TextCommand>();
        EventItemHelpSheet = Plugin.DataManager.GetExcelSheet<EventItemHelp>();
    }
}