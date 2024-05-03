using System.Diagnostics;
using ChatTwo.Code;
using ChatTwo.Resources;
using ChatTwo.Util;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;

namespace ChatTwo.Ui.SettingsTabs;

internal sealed class Database : ISettingsTab
{
    private Plugin Plugin { get; }
    private Configuration Mutable { get; }

    public string Name => Language.Options_Database_Tab + "###tabs-database";

    internal Database(Plugin plugin, Configuration mutable)
    {
        Plugin = plugin;
        Mutable = mutable;
    }

    private bool ShowAdvanced;

    private long DatabaseLastRefreshTicks;
    private long DatabaseSize;
    private long DatabaseLogSize;
    private int DatabaseMessageCount;

    public void Draw(bool changed)
    {
        if (changed)
            ShowAdvanced = ImGui.GetIO().KeyShift;

        ImGuiUtil.OptionCheckbox(ref Mutable.DatabaseBattleMessages, Language.Options_DatabaseBattleMessages_Name, Language.Options_DatabaseBattleMessages_Description);
        ImGui.Spacing();

        if (ImGuiUtil.OptionCheckbox(ref Mutable.LoadPreviousSession, Language.Options_LoadPreviousSession_Name, Language.Options_LoadPreviousSession_Description))
            if (Mutable.LoadPreviousSession)
                Mutable.FilterIncludePreviousSessions = true;

        ImGui.Spacing();

        if (ImGuiUtil.OptionCheckbox(ref Mutable.FilterIncludePreviousSessions, Language.Options_FilterIncludePreviousSessions_Name, Language.Options_FilterIncludePreviousSessions_Description))
            if (!Mutable.FilterIncludePreviousSessions)
                Mutable.LoadPreviousSession = false;

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var old = new FileInfo(Path.Join(Plugin.Interface.ConfigDirectory.FullName, "chat.db"));
        var migratedOld = new FileInfo(Path.Join(Plugin.Interface.ConfigDirectory.FullName, "chat-litedb.db"));
        if (old.Exists)
        {
            ImGui.TextUnformatted(Language.Options_Database_Old_Heading);
            ImGui.Spacing();

            if (ImGui.Button(Language.Options_Database_Old_Migration))
                Plugin.LegacyMessageImporterWindow.IsOpen = true;

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        }
        else if (migratedOld.Exists)
        {
            ImGui.TextUnformatted(Language.Options_Database_Old_Heading);
            ImGui.Spacing();

            if (ImGuiUtil.CtrlShiftButton(Language.Options_Database_Old_Delete, Language.Options_Database_Old_Delete_Tooltip))
            {
                try
                {
                    migratedOld.Delete();
                    WrapperUtil.AddNotification(Language.Options_Database_Old_Delete_Success, NotificationType.Success);
                }
                catch (Exception e)
                {
                    Plugin.Log.Error(e, "Unable to delete old database");
                    WrapperUtil.AddNotification(Language.Options_Database_Old_Delete_Error, NotificationType.Error);
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        }

        ImGui.TextUnformatted(Language.Options_Database_Metadata_Heading);
        using (ImRaii.PushIndent(ImGui.GetStyle().IndentSpacing, false))
        {
            // Refresh the database size and message count every 5 seconds to avoid
            // constant stat calls and spamming the database.
            if (DatabaseLastRefreshTicks + 5 * 1000 < Environment.TickCount64)
            {
                DatabaseSize = Plugin.MessageManager.Store.DatabaseSize();
                DatabaseLogSize = Plugin.MessageManager.Store.DatabaseLogSize();
                DatabaseMessageCount = Plugin.MessageManager.Store.MessageCount();
                DatabaseLastRefreshTicks = Environment.TickCount64;
            }

            // Copy the directory path instead of the file path so people can
            // paste it into their file explorer.
            ImGuiUtil.HelpText(string.Format(Language.Options_Database_Metadata_Path, MessageManager.DatabasePath()));
            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
            {
                var path = Path.GetDirectoryName(MessageManager.DatabasePath());
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
                ImGui.SetTooltip(StringUtil.BytesToString(DatabaseSize));

            ImGuiUtil.HelpText(string.Format(Language.Options_Database_Metadata_LogSize, StringUtil.BytesToString(DatabaseLogSize)));
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(StringUtil.BytesToString(DatabaseLogSize));

            ImGuiUtil.HelpText(string.Format(Language.Options_Database_Metadata_MessageCount, DatabaseMessageCount));

            if (ImGuiUtil.CtrlShiftButton(Language.Options_ClearDatabase_Button, Language.Options_ClearDatabase_Tooltip))
            {
                Plugin.Log.Warning("Clearing messages from database");
                Plugin.MessageManager.Store.ClearMessages();
                Plugin.MessageManager.ClearAllTabs();

                // Refresh on next draw
                DatabaseLastRefreshTicks = 0;
                WrapperUtil.AddNotification(Language.Options_ClearDatabase_Success, NotificationType.Info);
            }
        }

        ImGui.Spacing();

        if (!ShowAdvanced)
            return;

        using var treeNode = ImRaii.TreeNode(Language.Options_Database_Advanced);
        ImGui.PushTextWrapPos();
        ImGuiUtil.WarningText(Language.Options_Database_Advanced_Warning);

        if (ImGuiUtil.CtrlShiftButton("Perform maintenance", "Ctrl+Shift: MessageManager.Store.PerformMaintenance()"))
            Plugin.MessageManager.Store.PerformMaintenance();

        if (ImGuiUtil.CtrlShiftButton("Reload messages from database", "Ctrl+Shift: MessageManager.FilterAllTabs(false)"))
        {
            Plugin.MessageManager.ClearAllTabs();
            Plugin.MessageManager.FilterAllTabsAsync(false);
        }

        if (ImGuiUtil.CtrlShiftButton("Inject 10,000 messages", "Ctrl+Shift: creates 10,000 unique messages (async)"))
            new Thread(() => InsertMessages(10_000)).Start();

        ImGui.PopTextWrapPos();
        ImGui.Spacing();
    }

    private void InsertMessages(int count)
    {
        Plugin.Log.Info($"Inserting {count} messages due to user request");

        // Generate
        var stopwatch = Stopwatch.StartNew();
        var playerName = Plugin.ClientState.LocalPlayer?.Name.ToString() ?? "Unknown Player";
        var worldId = Plugin.ClientState.LocalPlayer?.HomeWorld.Id ?? 0;
        var senderSource = new SeStringBuilder()
            .AddText("<")
            .Add(new PlayerPayload(playerName, worldId))
            .AddText("Random Message")
            .Add(RawPayload.LinkTerminator)
            .AddText(">: ")
            .Build();
        var senderChunks = ChunkUtil.ToChunks(senderSource, ChunkSource.Sender, ChatType.Debug).ToList();
        var messages = new List<Message>(count);
        for (var i = 0; i < count; i++)
        {
            var contentSource = new SeStringBuilder()
                .AddText("Random message payload - ")
                .AddItalics(Guid.NewGuid().ToString())
                .Build();
            var contentChunks = ChunkUtil.ToChunks(contentSource, ChunkSource.Content, ChatType.Debug).ToList();

            messages.Add(new Message(
                Guid.NewGuid(),
                Plugin.MessageManager.CurrentContentId,
                Plugin.MessageManager.CurrentContentId,
                DateTimeOffset.UtcNow,
                new ChatCode(10),
                senderChunks,
                contentChunks,
                senderSource,
                contentSource,
                new SortCode(ChatType.Debug, ChatSource.Self),
                Guid.Empty
            ));
        }

        var elapsedTicks = stopwatch.ElapsedTicks;
        stopwatch.Stop();
        Plugin.Log.Info($"Crafted {count} messages in {elapsedTicks} ticks ({elapsedTicks / TimeSpan.TicksPerMillisecond}ms)");

        // Insert
        stopwatch = Stopwatch.StartNew();
        foreach (var message in messages)
            Plugin.MessageManager.Store.UpsertMessage(message);

        elapsedTicks = stopwatch.ElapsedTicks;
        stopwatch.Stop();
        Plugin.Log.Info($"Upserted {count} messages in {elapsedTicks} ticks ({elapsedTicks / TimeSpan.TicksPerMillisecond}ms)");

        // Clear tabs during framework frame
        Plugin.Framework.Run(() =>
        {
            stopwatch = Stopwatch.StartNew();
            Plugin.MessageManager.ClearAllTabs();
            elapsedTicks = stopwatch.ElapsedTicks;
            stopwatch.Stop();
            Plugin.Log.Info($"Cleared {Plugin.Config.Tabs.Count} tabs in {elapsedTicks} ticks ({elapsedTicks / TimeSpan.TicksPerMillisecond}ms)");
        }).Wait();

        // Fetch and filter during framework frame
        Plugin.Framework.Run(() =>
        {
            stopwatch = Stopwatch.StartNew();
            // Intentionally synchronous
            Plugin.MessageManager.FilterAllTabs(false);
            elapsedTicks = stopwatch.ElapsedTicks;
            stopwatch.Stop();
            Plugin.Log.Info($"Fetched and filtered all tabs in {elapsedTicks} ticks ({elapsedTicks / TimeSpan.TicksPerMillisecond}ms)");
        }).Wait();
    }
}
