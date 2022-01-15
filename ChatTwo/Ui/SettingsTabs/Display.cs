using ImGuiNET;

namespace ChatTwo.Ui.SettingsTabs;

internal sealed class Display : ISettingsTab {
    private Configuration Mutable { get; }

    public string Name => "Display";

    internal Display(Configuration mutable) {
        this.Mutable = mutable;
    }

    public void Draw() {
        ImGui.Checkbox("Hide chat", ref this.Mutable.HideChat);
        ImGui.Checkbox("Show native item tooltips", ref this.Mutable.NativeItemTooltips);
        ImGui.Checkbox("Show tabs in a sidebar", ref this.Mutable.SidebarTabView);
        ImGui.Checkbox("Use modern timestamp layout", ref this.Mutable.PrettierTimestamps);

        if (this.Mutable.PrettierTimestamps) {
            ImGui.Checkbox("More compact modern layout", ref this.Mutable.MoreCompactPretty);
        }

        ImGui.DragFloat("Font size", ref this.Mutable.FontSize, .0125f, 12f, 36f, "%.1f");
        ImGui.Checkbox("Allow moving main window", ref this.Mutable.CanMove);
        ImGui.Checkbox("Allow resizing main window", ref this.Mutable.CanResize);
        ImGui.Checkbox("Show title bar for main window", ref this.Mutable.ShowTitleBar);
    }
}
