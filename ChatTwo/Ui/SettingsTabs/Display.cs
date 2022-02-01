using ImGuiNET;

namespace ChatTwo.Ui.SettingsTabs;

internal sealed class Display : ISettingsTab {
    private Configuration Mutable { get; }

    public string Name => "Display";

    internal Display(Configuration mutable) {
        this.Mutable = mutable;
    }

    public void Draw() {
        ImGui.Checkbox("Hide vanilla chat", ref this.Mutable.HideChat);
        ImGui.Checkbox("Hide chat during cutscenes", ref this.Mutable.HideDuringCutscenes);
        ImGui.Checkbox("Show native item tooltips", ref this.Mutable.NativeItemTooltips);
        ImGui.Checkbox("Show tabs in a sidebar", ref this.Mutable.SidebarTabView);
        ImGui.Checkbox("Use modern timestamp layout", ref this.Mutable.PrettierTimestamps);

        if (this.Mutable.PrettierTimestamps) {
            ImGui.Checkbox("More compact modern layout", ref this.Mutable.MoreCompactPretty);
        }

        ImGui.DragFloat("Font size", ref this.Mutable.FontSize, .0125f, 12f, 36f, $"{this.Mutable.FontSize:N1}");
        if (ImGui.DragFloat("Window opacity", ref this.Mutable.WindowAlpha, .0025f, 0f, 1f, $"{this.Mutable.WindowAlpha * 100f:N2}%%")) {
            switch (this.Mutable.WindowAlpha) {
                case > 1f and <= 100f:
                    this.Mutable.WindowAlpha /= 100f;
                    break;
                case < 0f or > 100f:
                    this.Mutable.WindowAlpha = 1f;
                    break;
            }
        }

        ImGui.Checkbox("Allow moving main window", ref this.Mutable.CanMove);
        ImGui.Checkbox("Allow resizing main window", ref this.Mutable.CanResize);
        ImGui.Checkbox("Show title bar for main window", ref this.Mutable.ShowTitleBar);
    }
}
