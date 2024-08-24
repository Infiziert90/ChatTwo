using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace ChatTwo;

public static class Sheets
{
    public static readonly ExcelSheet<Item> ItemSheet;
    public static readonly ExcelSheet<World> WorldSheet;
    public static readonly ExcelSheet<LogFilter> LogFilterSheet;
    public static readonly ExcelSheet<TextCommand> TextCommandSheet;

    static Sheets()
    {
        ItemSheet = Plugin.DataManager.GetExcelSheet<Item>()!;
        WorldSheet = Plugin.DataManager.GetExcelSheet<World>()!;
        LogFilterSheet = Plugin.DataManager.GetExcelSheet<LogFilter>()!;
        TextCommandSheet = Plugin.DataManager.GetExcelSheet<TextCommand>()!;
    }
}