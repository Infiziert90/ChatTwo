using System.Numerics;
using ChatTwo.Resources;
using ChatTwo.Util;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace ChatTwo.Ui;

internal class LegacyMesasgeImporterWindow : Window
{
    private readonly MessageStore _store;

    private LegacyMessageImporterEligibility Eligibility { get; set; }
    private LegacyMessageImporter? Importer { get; set; }

    internal LegacyMesasgeImporterWindow(MessageStore store) : base("Chat 2 Legacy Importer###chat2-legacy-importer")
    {
        _store = store;
        Eligibility = LegacyMessageImporterEligibility.CheckEligibility();
        LogAndNotify();
    }

    public void Dispose()
    {
        Importer?.Dispose();
    }

    private void LogAndNotify()
    {
        Plugin.Log.Info(
            $"[Migration] Checked migration eligibility: {Eligibility.Status} - '{Eligibility.AdditionalIneligibilityInfo}'");

        switch (Eligibility.Status)
        {
            case LegacyMessageImporterEligibilityStatus.Eligible:
            {
                var notification = Plugin.Notification.AddNotification(new Notification
                {
                    Type = NotificationType.Info,
                    // The user needs to dismiss this for it to go away.
                    InitialDuration = TimeSpan.FromHours(6),
                    Title = "Chat 2 Migration",
                    Content = "Import messages from old database into new database? Click for more information.",
                });
                // TODO: clicking does not dismiss
                notification.Click += _ => IsOpen = true;
                break;
            }

            case LegacyMessageImporterEligibilityStatus.IneligibleLiteDbFailed:
            {
                var notification = Plugin.Notification.AddNotification(new Notification
                {
                    Type = NotificationType.Warning,
                    InitialDuration = TimeSpan.FromMinutes(1),
                    Title = "Chat Two Migration",
                    Content =
                        "Migration is not possible because the old database could not be opened. Click for more information."
                });
                // TODO: clicking does not dismiss
                notification.Click += _ => IsOpen = true;
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
        // TODO: pretty
        ImGui.Text("Import database messages from legacy LiteDB database to Sqlite database?");
        ImGui.Text($"Message count: {Eligibility.MessageCount}");
        ImGui.Text($"Database size: {Eligibility.DatabaseSizeBytes}");

        if (ImGui.Button("Yes, import messages"))
        {
            // Next draw call will run DrawImportStatus().
            Importer = Eligibility.StartImport(_store);
            return;
        }

        ImGui.SameLine();

        if (ImGuiUtil.CtrlShiftButton("No, do not import messages",
                "Ctrl+Shift: renames old database to avoid prompting again"))
        {
            Eligibility.RenameOldDatabase();
            IsOpen = false;
        }
    }

    private void DrawIneligible()
    {
        // TODO: pretty
        ImGui.Text("Your legacy LiteDB database is not eligible for import:");
        switch (Eligibility.Status)
        {
            case LegacyMessageImporterEligibilityStatus.IneligibleOriginalDbNotExists:
                ImGui.Text("The old database could not be found.");
                break;
            case LegacyMessageImporterEligibilityStatus.IneligibleMigrationDbExists:
                ImGui.Text("The migration process was already started.");
                break;
            case LegacyMessageImporterEligibilityStatus.IneligibleLiteDbFailed:
                ImGui.Text("The old database could not be opened.");
                break;
            case LegacyMessageImporterEligibilityStatus.IneligibleNoMessages:
                ImGui.Text("The old database contains no messages.");
                break;
            case LegacyMessageImporterEligibilityStatus.Eligible:
            default:
                throw new ArgumentOutOfRangeException();
        }
        if (!string.IsNullOrWhiteSpace(Eligibility.AdditionalIneligibilityInfo))
            ImGui.Text(Eligibility.AdditionalIneligibilityInfo);

        // LiteDB failures notify the user, so give them a chance to rename the
        // database to avoid prompting again.
        if (Eligibility.Status == LegacyMessageImporterEligibilityStatus.IneligibleLiteDbFailed)
        {
            if (ImGuiUtil.CtrlShiftButton("Rename old database",
                    "Ctrl+Shift: rename old database to avoid import prompt in the future"))
            {
                Eligibility.RenameOldDatabase();
                // TODO: notify success as this changes the status
            }
        }
    }

    private void DrawImportStatus()
    {
        // TODO: pretty
        if (Importer == null)
            return;

        var importStart = Importer.ImportStart;
        var importEnd = Importer.ImportComplete;
        var total = Importer.ImportCount;
        var successful = Importer!.SuccessfulMessages;
        var failed = Importer.FailedMessages;
        var remaining = Importer.RemainingMessages;

        if (importEnd != null)
        {
            ImGui.Text($"Completed migration in {Duration(importStart, importEnd.Value)}");
            ImGui.Text($"Successfully imported: {successful} messages");
            ImGui.Text($"Failed to import: {failed} messages");
            ImGui.Text($"Unaccounted for: {remaining}");
            ImGui.Text("See logs for more details: /xllog");
            return;
        }

        // TODO: implement Importer.MaxMessageRate slider in UI, values 0 (infinity) => 10000

        ImGui.Text($"Importing messages... {Importer.Progress:P}%");
        ImGui.Text($"Duration: {Duration(importStart, Environment.TickCount64)}");
        ImGui.Text($"Successfully imported: {successful} messages");
        ImGui.Text($"Failed to import: {failed} messages");
        ImGui.Text($"Progress: {Importer.ProcessedMessages}/{total} messages");
        ImGui.Text($"Remaining: {remaining} messages");
        ImGui.Text($"Messages per second: {Importer.CurrentMessageRate}");
        ImGui.Text($"Estimated time remaining: {Importer.EstimatedTimeRemaining}");
        ImGui.Text("See logs for more details: /xllog");

        // TODO: this doesn't render properly
        ImGui.ProgressBar(Importer.Progress, new Vector2(0.0f, 0.0f), $"{Importer.Progress:P}%");

        if (ImGuiUtil.CtrlShiftButton("Cancel import", "Ctrl+Shift: cancel import and close window"))
        {
            // TODO: This currently crashes the whole game because we don't ask
            // the importer thread to stop and wait for it to stop before
            // disposing it.
            // See LegacyMessageImporter.Dispose() for more details.
            /*
            Importer.Dispose();
            Importer = null;
            Eligibility = LegacyMessageImporterEligibility.CheckEligibility();
            */
        }
    }

    private static TimeSpan Duration(long startTicks, long endTicks)
    {
        return endTicks < startTicks ? TimeSpan.Zero : TimeSpan.FromTicks(endTicks - startTicks);
    }
}
