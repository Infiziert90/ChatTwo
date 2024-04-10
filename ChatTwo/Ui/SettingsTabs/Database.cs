using ChatTwo.Resources;
using ChatTwo.Util;
using ImGuiNET;

namespace ChatTwo.Ui.SettingsTabs;

internal sealed class Database : ISettingsTab {
    private Configuration Mutable { get; }
    private Store Store { get; }

    public string Name => Language.Options_Database_Tab + "###tabs-database";

    internal Database(Configuration mutable, Store store) {
        Store = store;
        Mutable = mutable;
    }

    private bool _showAdvanced;

    public void Draw(bool changed) {
        if (changed) {
            _showAdvanced = ImGui.GetIO().KeyShift;
        }

        ImGuiUtil.OptionCheckbox(ref Mutable.DatabaseBattleMessages, Language.Options_DatabaseBattleMessages_Name, Language.Options_DatabaseBattleMessages_Description);
        ImGui.Spacing();

        if (ImGuiUtil.OptionCheckbox(ref Mutable.LoadPreviousSession, Language.Options_LoadPreviousSession_Name, Language.Options_LoadPreviousSession_Description)) {
            if (Mutable.LoadPreviousSession) {
                Mutable.FilterIncludePreviousSessions = true;
            }
        }

        ImGui.Spacing();

        if (ImGuiUtil.OptionCheckbox(ref Mutable.FilterIncludePreviousSessions, Language.Options_FilterIncludePreviousSessions_Name, Language.Options_FilterIncludePreviousSessions_Description)) {
            if (!Mutable.FilterIncludePreviousSessions) {
                Mutable.LoadPreviousSession = false;
            }
        }

        ImGuiUtil.OptionCheckbox(
            ref Mutable.SharedMode,
            Language.Options_SharedMode_Name,
            string.Format(Language.Options_SharedMode_Description, Plugin.PluginName)
        );
        ImGuiUtil.WarningText(string.Format(Language.Options_SharedMode_Warning, Plugin.PluginName));

        ImGui.Spacing();

        if (_showAdvanced && ImGui.TreeNodeEx(Language.Options_Database_Advanced)) {
            ImGui.PushTextWrapPos();
            ImGuiUtil.WarningText(Language.Options_Database_Advanced_Warning);

            if (ImGui.Button("Checkpoint")) {
                Store.Database.Checkpoint();
            }

            if (ImGui.Button("Rebuild")) {
                Store.Database.Rebuild();
            }

            ImGui.PopTextWrapPos();
            ImGui.TreePop();
        }
    }
}
