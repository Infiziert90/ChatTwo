using System.Numerics;
using ChatTwo.Resources;
using ChatTwo.Util;
using Dalamud.Interface;
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

        ImGui.Spacing();

        if (ImGuiUtil.IconButton(FontAwesomeIcon.ExternalLinkAlt, "clickup"))
        {
            Dalamud.Utility.Util.OpenLink("https://sharing.clickup.com/b/h/6-122378074-2/1047d21a39a4140");
        }

        ImGui.SameLine();
        ImGui.TextUnformatted(Language.Options_About_ClickUp);

        if (ImGuiUtil.IconButton(FontAwesomeIcon.ExternalLinkAlt, "crowdin"))
        {
            Dalamud.Utility.Util.OpenLink("https://crowdin.com/project/chattwo");
        }

        ImGui.SameLine();
        ImGui.TextUnformatted(string.Format(Language.Options_About_CrowdIn, Plugin.PluginName));

        ImGui.Spacing();

        var height = ImGui.GetContentRegionAvail().Y - ImGui.CalcTextSize("A").Y - ImGui.GetStyle().ItemSpacing.Y * 2;
        if (ImGui.BeginChild("about", new Vector2(-1, height))) {
            if (ImGui.TreeNodeEx(Language.Options_About_Translators)) {
                if (ImGui.BeginChild("translators")) {
                    foreach (var translator in _translators) {
                        ImGui.TextUnformatted(translator);
                    }

                    ImGui.EndChild();
                }

                ImGui.TreePop();
            }

            ImGui.EndChild();
        }

        ImGuiUtil.HelpText($"{Plugin.PluginName} v{GetType().Assembly.GetName().Version?.ToString(3)}");
        ImGui.PopTextWrapPos();
    }
}
