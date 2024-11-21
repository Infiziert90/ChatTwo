using ChatTwo.Resources;
using ChatTwo.Util;
using ImGuiNET;

namespace ChatTwo.Ui.SettingsTabs;

internal sealed class Preview : ISettingsTab
{
    private Configuration Mutable { get; }

    public string Name => $"{Language.Options_Preview_Tab}###tabs-preview";

    internal Preview(Configuration mutable)
    {
        Mutable = mutable;
    }

    public void Draw(bool changed)
    {
        using var wrap = ImGuiUtil.TextWrapPos();

        using (var combo = ImGuiUtil.BeginComboVertical(Language.Options_Preview_Name, Mutable.PreviewPosition.Name()))
        {
            if (combo)
            {
                foreach (var position in Enum.GetValues<PreviewPosition>())
                    if (ImGui.Selectable(position.Name(), Mutable.PreviewPosition == position))
                        Mutable.PreviewPosition = position;
            }
        }
        ImGuiUtil.HelpText(Language.Options_Preview_Description);
        ImGui.Spacing();

        if (ImGuiUtil.InputIntVertical(Language.Options_PreviewMinimum_Name, Language.Options_PreviewMinimum_Description, ref Mutable.PreviewMinimum))
            Mutable.PreviewMinimum = Math.Clamp(Mutable.PreviewMinimum, 1, 250);
        ImGui.Spacing();
        ImGuiUtil.OptionCheckbox(ref Mutable.OnlyPreviewIf, Language.Options_PreviewOnlyIf_Name, Language.Options_PreviewOnlyIf_Description);

        ImGui.Spacing();
    }
}
