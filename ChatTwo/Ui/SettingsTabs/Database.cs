using ChatTwo.Resources;
using ChatTwo.Util;
using Dalamud.Interface.Internal.Notifications;
using ImGuiNET;

namespace ChatTwo.Ui.SettingsTabs;

internal sealed class Database : ISettingsTab
{
    private Configuration Mutable { get; }
    private Plugin Plugin { get; }

    public string Name => Language.Options_Database_Tab + "###tabs-database";

    internal Database(Configuration mutable, Plugin plugin)
    {
        Plugin = plugin;
        Mutable = mutable;
    }

    private bool ShowAdvanced;

    private long DatabaseLastRefreshTicks;
    private long DatabaseSize;
    private long DatabaseLogSize;
    private int DatabaseMessageCount;

    public void Draw(bool changed) {
        if (changed)
            ShowAdvanced = ImGui.GetIO().KeyShift;

        ImGuiUtil.OptionCheckbox(ref Mutable.DatabaseBattleMessages, Language.Options_DatabaseBattleMessages_Name, Language.Options_DatabaseBattleMessages_Description);
        ImGui.Spacing();

        if (ImGuiUtil.OptionCheckbox(ref Mutable.LoadPreviousSession, Language.Options_LoadPreviousSession_Name, Language.Options_LoadPreviousSession_Description))
        {
            if (Mutable.LoadPreviousSession)
                Mutable.FilterIncludePreviousSessions = true;
        }

        ImGui.Spacing();

        if (ImGuiUtil.OptionCheckbox(ref Mutable.FilterIncludePreviousSessions, Language.Options_FilterIncludePreviousSessions_Name, Language.Options_FilterIncludePreviousSessions_Description))
        {
            if (!Mutable.FilterIncludePreviousSessions)
                Mutable.LoadPreviousSession = false;
        }

        ImGuiUtil.OptionCheckbox(
            ref Mutable.SharedMode,
            Language.Options_SharedMode_Name,
            string.Format(Language.Options_SharedMode_Description, Plugin.PluginName)
        );
        ImGuiUtil.WarningText(string.Format(Language.Options_SharedMode_Warning, Plugin.PluginName));

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text(Language.Options_Database_Metadata_Heading);
        var style = ImGui.GetStyle();
        ImGui.Indent(style.IndentSpacing);

        // Refresh the database size and message count every 5 seconds to avoid
        // constant stat calls and spamming the database.
        if (DatabaseLastRefreshTicks + 5 * 1000 < Environment.TickCount64)
        {
            DatabaseSize = Store.DatabaseSize();
            DatabaseLogSize = Store.DatabaseLogSize();
            DatabaseMessageCount = Plugin.Store.MessageCount();
            DatabaseLastRefreshTicks = Environment.TickCount64;
        }

        ImGuiUtil.HelpText(string.Format(Language.Options_Database_Metadata_Path, Store.DatabasePath()));
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
        {
            // Copy the directory path instead of the file path so people can
            // paste it into their file explorer.
            var path = Path.GetDirectoryName(Store.DatabasePath());
            ImGui.SetClipboardText(path);
            WrapperUtil.AddNotification(Language.Options_Database_Metadata_CopyConfigPathNotification, NotificationType.Info);
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            ImGui.SetTooltip(Language.Options_Database_Metadata_CopyConfigPath);
        }

        ImGuiUtil.HelpText(string.Format(Language.Options_Database_Metadata_Size, StringUtil.BytesToString(DatabaseSize)));
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(DatabaseSize.ToString("N0") + "B");

        ImGuiUtil.HelpText(string.Format(Language.Options_Database_Metadata_LogSize, StringUtil.BytesToString(DatabaseLogSize)));
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(DatabaseLogSize.ToString("N0") + "B");

        ImGuiUtil.HelpText(string.Format(Language.Options_Database_Metadata_MessageCount, DatabaseMessageCount, Store.MessagesLimit));

        if (ImGuiUtil.CtrlShiftButton(Language.Options_ClearDatabase_Button, Language.Options_ClearDatabase_Tooltip))
        {
            Plugin.Log.Warning("Clearing database");
            Plugin.Store.ClearDatabase();
            foreach (var tab in Plugin.Config.Tabs)
                tab.Clear();

            // Refresh on next draw
            DatabaseLastRefreshTicks = 0;
            WrapperUtil.AddNotification(Language.Options_ClearDatabase_Success, NotificationType.Info);
        }

        ImGui.Unindent(style.IndentSpacing);
        ImGui.Spacing();

        if (ShowAdvanced && ImGui.TreeNodeEx(Language.Options_Database_Advanced))
        {
            ImGui.PushTextWrapPos();
            ImGuiUtil.WarningText(Language.Options_Database_Advanced_Warning);

            if (ImGuiUtil.CtrlShiftButton("Checkpoint", "Ctrl+Shift: Database.Checkpoint()"))
                Plugin.Store.Database.Checkpoint();

            if (ImGuiUtil.CtrlShiftButton("Rebuild", "Ctrl+Shift: Database.Rebuild()"))
                Plugin.Store.Database.Rebuild();

            ImGui.PopTextWrapPos();
            ImGui.TreePop();
        }

        ImGui.Spacing();
    }
}
