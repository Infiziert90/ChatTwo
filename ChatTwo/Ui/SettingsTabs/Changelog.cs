using ChatTwo.Resources;
using ChatTwo.Util;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;

namespace ChatTwo.Ui.SettingsTabs;

internal sealed class Changelog : ISettingsTab
{
    private Configuration Mutable { get; }

    public string Name => Language.Options_Changelog_Tab + "###tabs-changelog";

    internal Changelog(Configuration mutable)
    {
        Mutable = mutable;
    }

    public void Draw(bool changed)
    {
        using var wrap = ImGuiUtil.TextWrapPos();

        ImGui.TextUnformatted(Language.Options_Warning_NotImplemented);
        ImGuiUtil.OptionCheckbox(ref Mutable.PrintChangelog, Language.Options_PrintChangelog_Name, Language.Options_PrintChangelog_Description);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var changelog = Plugin.Interface.Manifest.Changelog;
        if (changelog != null)
        {
            ImGui.TextUnformatted(Language.Options_Changelog_Header);
            ImGui.TextUnformatted($"Version {Plugin.Interface.Manifest.AssemblyVersion.ToString(3)}");
            ImGui.Spacing();
            foreach (var sentence in changelog.Split("\n"))
            {
                if (sentence == string.Empty)
                {
                    ImGui.NewLine();
                    continue;
                }

                var condition = sentence.StartsWith('-') || sentence.StartsWith("  -");
                using var indent = ImRaii.PushIndent(10.0f, true, condition);
                ImGui.TextUnformatted(sentence);
            }
        }
        ImGui.Spacing();
    }
}
