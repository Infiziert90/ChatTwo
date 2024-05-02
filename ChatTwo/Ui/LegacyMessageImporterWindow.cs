using System.Numerics;
using ChatTwo.Util;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.ImGuiNotification.EventArgs;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace ChatTwo.Ui;

internal class LegacyMessageImporterWindow : Window
{
    private readonly Plugin Plugin;
    private readonly MessageStore _store;

    private LegacyMessageImporterEligibility Eligibility { get; set; }
    private LegacyMessageImporter? Importer { get; set; }

    internal LegacyMessageImporterWindow(Plugin plugin) : base("Chat 2 Legacy Importer###chat2-legacy-importer")
    {
        Plugin = plugin;

        Flags = ImGuiWindowFlags.NoResize;
        Size = new Vector2(500, 400);

        RespectCloseHotkey = false;
        DisableWindowSounds = true;

        _store = plugin.MessageManager.Store;
        Eligibility = LegacyMessageImporterEligibility.CheckEligibility();
        LogAndNotify();
    }

    public void Dispose()
    {
        Importer?.DisposeAsync().AsTask().Wait();
    }

    private void NotificationClicked(INotificationClickArgs args)
    {
        IsOpen = true;
        args.Notification.DismissNow();
    }

    private void LogAndNotify()
    {
        Plugin.Log.Info($"[Migration] Checked migration eligibility: {Eligibility.Status} - '{Eligibility.AdditionalIneligibilityInfo}'");

        switch (Eligibility.Status)
        {
            case LegacyMessageImporterEligibilityStatus.Eligible:
            {
                var notification = Plugin.Notification.AddNotification(new Notification
                {
                    // The user needs to dismiss this for it to go away.
                    Type = NotificationType.Info,
                    InitialDuration = TimeSpan.FromHours(24),
                    Title = "Chat2 Migration",
                    Content = "Import messages from old database into new database?\nClick for more information.",
                    Minimized = false,
                });

                notification.Click += NotificationClicked;
                break;
            }

            case LegacyMessageImporterEligibilityStatus.IneligibleLiteDbFailed:
            {
                var notification = Plugin.Notification.AddNotification(new Notification
                {
                    Type = NotificationType.Warning,
                    InitialDuration = TimeSpan.FromMinutes(1),
                    Title = "Chat2 Migration",
                    Content = "Migration is not possible because the old database could not be opened.\nClick for more information.",
                    Minimized = false,
                });

                notification.Click += NotificationClicked;
                break;
            }
        }
    }

    public override void Draw()
    {
        if (Importer != null)
        {
            DrawImportStatus();
            return;
        }

        if (Eligibility.Status == LegacyMessageImporterEligibilityStatus.Eligible)
            DrawEligible();
        else
            DrawIneligible();
    }

    private void DrawEligible()
    {
        ImGui.TextWrapped("Import database messages from legacy LiteDB database to SQLite database?");
        ImGui.TextWrapped($"Message count: {Eligibility.MessageCount:N0}");
        ImGui.TextWrapped($"Database size: {StringUtil.BytesToString(Eligibility.DatabaseSizeBytes)}");

        ImGui.Spacing();

        var colorNormal = new Vector4(0.0f, 0.70f, 0.0f, 1.0f);
        var colorHovered = new Vector4(0.059f, 0.49f, 0.0f, 1.0f);
        using (ImRaii.PushColor(ImGuiCol.Button, colorNormal))
        using (ImRaii.PushColor(ImGuiCol.ButtonHovered, colorHovered))
        {
            if (ImGui.Button("Yes, import messages"))
            {
                // Next draw call will run DrawImportStatus().
                Importer = Eligibility.StartImport(_store, plugin: Plugin);
                return;
            }
        }

        ImGui.SameLine();

        if (ImGuiUtil.CtrlShiftButtonColored("No, do not import messages", "Ctrl+Shift: renames old database to avoid prompting again"))
        {
            Eligibility.RenameOldDatabase();
            IsOpen = false;
        }
    }

    private void DrawIneligible()
    {
        ImGui.TextWrapped("Your legacy LiteDB database is not eligible for import:");
        switch (Eligibility.Status)
        {
            case LegacyMessageImporterEligibilityStatus.IneligibleOriginalDbNotExists:
                ImGui.TextWrapped("The old database could not be found.");
                break;
            case LegacyMessageImporterEligibilityStatus.IneligibleMigrationDbExists:
                ImGui.TextWrapped("The migration process was already started.");
                break;
            case LegacyMessageImporterEligibilityStatus.IneligibleLiteDbFailed:
                ImGui.TextWrapped("The old database could not be opened.");
                break;
            case LegacyMessageImporterEligibilityStatus.IneligibleNoMessages:
                ImGui.TextWrapped("The old database contains no messages.");
                break;
            case LegacyMessageImporterEligibilityStatus.Eligible:
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (!string.IsNullOrWhiteSpace(Eligibility.AdditionalIneligibilityInfo))
            ImGui.TextWrapped(Eligibility.AdditionalIneligibilityInfo);

        // LiteDB failures notify the user, so give them a chance to rename the
        // database to avoid prompting again.
        if (Eligibility.Status == LegacyMessageImporterEligibilityStatus.IneligibleLiteDbFailed)
        {
            if (ImGuiUtil.CtrlShiftButton("Rename old database", "Ctrl+Shift: rename old database to avoid import prompt in the future"))
            {
                if (Eligibility.RenameOldDatabase())
                    WrapperUtil.AddNotification("Successfully renamed the old database.", NotificationType.Success);
                else
                    WrapperUtil.AddNotification("Rename failed, please check /xllog for more information.", NotificationType.Error);
            }
        }
    }

    private void DrawImportStatus()
    {
        if (Importer == null)
            return;

        if (Importer.ImportComplete != null)
        {
            ImGui.TextUnformatted($"Completed migration in {Duration(Importer.ImportStart, Importer.ImportComplete.Value):g}");
            ImGui.TextUnformatted($"Successfully imported: {Importer.SuccessfulMessages:N0} messages");
            ImGui.TextUnformatted($"Failed to import: {Importer.FailedMessages:N0} messages");
            ImGui.TextUnformatted($"Unaccounted for: {Importer.RemainingMessages:N0}");
            ImGui.TextUnformatted("See logs for more details: /xllog");

            ImGui.Spacing();

            if (ImGui.Button("Finish"))
                IsOpen = false;

            return;
        }

        ImGui.TextUnformatted($"Importing messages ... {Importer.Progress:P}");
        ImGuiHelpers.ScaledDummy(10.0f);

        ImGui.TextUnformatted($"Duration: {Duration(Importer.ImportStart, Environment.TickCount64):g}");
        ImGui.TextUnformatted($"Progress: {Importer.ProcessedMessages:N0}/{Importer.ImportCount:N0} messages ({Importer.FailedMessages:N0} failed)");
        ImGuiHelpers.ScaledDummy(10.0f);

        var width = ImGui.GetContentRegionAvail().X / 2;
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Import speed:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(width);
        ImGui.SliderInt("##speedSlider", ref Importer.MaxMessageRate, 1, 10000, "%d msgs/sec", ImGuiSliderFlags.AlwaysClamp);
        ImGui.TextUnformatted($"Current speed: {Importer.CurrentMessageRate:N0} msgs/sec");
        ImGui.TextUnformatted($"Estimated time remaining: {Importer.EstimatedTimeRemaining:g}");
        ImGui.TextUnformatted("See logs for more details: /xllog");
        ImGuiHelpers.ScaledDummy(10.0f);

        ImGui.ProgressBar(Importer.Progress, new Vector2(-1, 0), $"{Importer.Progress:P}");
        ImGui.Spacing();

        if (ImGuiUtil.CtrlShiftButton("Cancel import", "Ctrl+Shift: cancel import and close window"))
        {
            Task.Run(async () =>
            {
                await Importer.DisposeAsync();
                Importer = null;
                Eligibility = LegacyMessageImporterEligibility.CheckEligibility();
            });
        }
    }

    private static TimeSpan Duration(long startTicks, long endTicks)
    {
        return endTicks < startTicks ? TimeSpan.Zero : TimeSpan.FromMilliseconds(endTicks - startTicks);
    }
}
