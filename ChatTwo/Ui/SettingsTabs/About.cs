using System.Numerics;
using ChatTwo.Resources;
using ChatTwo.Util;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using ImGuiNET;

namespace ChatTwo.Ui.SettingsTabs;

internal sealed class About : ISettingsTab {
    public string Name => string.Format(Language.Options_About_Tab, Plugin.PluginName) + "###tabs-about";

    private readonly List<string> _translators =
    [
        "q673135110", "Akizem", "d0tiKs",
        "Moonlight_Everlit", "Dark32", "andreycout",
        "Button_", "Cali666", "cassandra308",
        "lokinmodar", "jtabox", "AkiraYorumoto",
        "MKhayle", "elena.space", "imlisa",
        "andrei5125", "ShivaMaheshvara", "aislinn87",
        "nishinatsu051", "lichuyuan", "Risu64",
        "yummypillow", "witchymary", "Yuzumi",
        "zomsakura", "Sirayuki"
    ];

    internal About() {
        _translators.Sort((a, b) => string.Compare(a.ToLowerInvariant(), b.ToLowerInvariant(), StringComparison.Ordinal));
    }

    public void Draw(bool changed) {
        ImGui.PushTextWrapPos();

        ImGui.TextUnformatted(string.Format(Language.Options_About_Opening, Plugin.PluginName));

        ImGuiHelpers.ScaledDummy(10.0f);

        ImGui.TextUnformatted(Language.Options_About_Authors);
        ImGui.SameLine();
        ImGui.TextColored(ImGuiColors.ParsedGold, Plugin.Authors);

        ImGui.TextUnformatted(Language.Options_About_Discord);
        ImGui.SameLine();
        ImGui.TextColored(ImGuiColors.ParsedGold, "@infi");

        ImGui.TextUnformatted(Language.Options_About_Version);
        ImGui.SameLine();
        ImGui.TextColored(ImGuiColors.ParsedOrange, Plugin.Version);

        ImGuiHelpers.ScaledDummy(10.0f);

        ImGui.TextUnformatted(Language.Options_About_Discord_Thread);
        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.ExternalLinkAlt, "discordThread"))
            Dalamud.Utility.Util.OpenLink("https://canary.discord.com/channels/581875019861328007/1224865018789761126");

        ImGui.Spacing();

        ImGui.TextUnformatted(Language.Options_About_Github_Issues);
        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.ExternalLinkAlt, "githubIssues"))
            Dalamud.Utility.Util.OpenLink("https://github.com/Infiziert90/ChatTwo/issues");

        ImGuiHelpers.ScaledDummy(10.0f);

        ImGui.TextUnformatted(Language.Options_About_CrowdIn);
        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.ExternalLinkAlt, "crowdin"))
            Dalamud.Utility.Util.OpenLink("https://crowdin.com/project/chattwo");

        ImGui.Spacing();

        var height = ImGui.GetContentRegionAvail().Y - ImGui.CalcTextSize("A").Y - ImGui.GetStyle().ItemSpacing.Y * 2;
        if (ImGui.BeginChild("about", new Vector2(-1, height))) {
            if (ImGui.TreeNodeEx(Language.Options_About_Translators)) {
                if (ImGui.BeginChild("translators")) {
                    foreach (var translator in _translators) {
                        ImGui.TextUnformatted(translator);
                    }

                }
                ImGui.EndChild();

                ImGui.TreePop();
            }
        }
        ImGui.EndChild();

        ImGui.Spacing();
        ImGui.PopTextWrapPos();
    }
}
