using ChatTwo.Resources;
using ChatTwo.Util;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;

namespace ChatTwo.Ui.SettingsTabs;

internal sealed class Webinterface(Plugin plugin, Configuration mutable) : ISettingsTab
{
    private Plugin Plugin { get; } = plugin;
    private Configuration Mutable { get; } = mutable;
    public string Name => Language.Options_Webinterface_Tab + "###tabs-Webinterface";

    public void Draw(bool changed)
    {
        ImGuiUtil.WrappedTextWithColor(ImGuiColors.DalamudWhite, "On checking 'Enabled' this will enable and load up Chat2's built-in web interface, which will allow devices on your network to access in-game chat. This feature may be used to allow a phone or another computer to see Chat2 activity, switch channels, and send messages as though you were typing in FFXIV itself.");
        ImGui.Spacing();
        ImGuiUtil.WrappedTextWithColor(ImGuiColors.HealerGreen, "Note: This will require at least a semi-modern browser in order to function correctly.");
        ImGui.Spacing();
        ImGuiUtil.WrappedTextWithColor(ImGuiColors.DalamudOrange, "For reasons of account security, this feature is not intended for use outside of your local network, you have been warned!");

        ImGui.Spacing();
        ImGui.Spacing();
        ImGuiUtil.WrappedTextWithColor(ImGuiColors.DalamudViolet, "Do Not:");
        using (ImRaii.PushIndent(15.0f))
        {
            ImGuiUtil.WrappedTextWithColor(ImGuiColors.DalamudViolet, "- Forward the port used (9000)");
            ImGuiUtil.WrappedTextWithColor(ImGuiColors.DalamudViolet, "- Share your authentication code with anyone else");
            ImGuiUtil.WrappedTextWithColor(ImGuiColors.DalamudViolet, "- Expect multi-boxing to work with this (only first client is tracked and utilised)");
        }
        ImGui.Spacing();
        ImGuiUtil.WrappedTextWithColor(ImGuiColors.DalamudOrange, "No support will be provided if any of the 'Do Not' clauses aren't respected and adhered to appropriately.");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Checkbox(Language.Options_WebinterfaceEnable_Name, ref Mutable.WebinterfaceEnabled);
        ImGuiUtil.HelpText(Language.Options_WebinterfaceEnable_Description);
        ImGui.Spacing();

        if (!Mutable.WebinterfaceEnabled)
            return;

        ImGui.Separator();
        ImGui.Spacing();

        ImGuiUtil.WrappedTextWithColor(ImGuiColors.DalamudOrange, Language.Webinterface_CurrentPassword);
        ImGui.TextUnformatted(Mutable.WebinterfacePassword);
        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Recycle, tooltip: Language.Webinterface_PasswordReset_Tooltip))
        {
            Mutable.WebinterfacePassword = WebinterfaceUtil.GenerateSimpleAuthCode();
            Plugin.ServerCore.InvalidateSessions();
        }

        ImGuiUtil.WrappedTextWithColor(ImGuiColors.HealerGreen, Language.Webinterface_Status);
        using (ImRaii.PushIndent(10.0f))
        {
            ImGui.TextUnformatted(Language.Webinterface_Status_Active);
            ImGui.SameLine();

            var isActive = Plugin.ServerCore.GetStats();
            using (Plugin.FontManager.FontAwesome.Push())
            using (ImRaii.PushColor(ImGuiCol.Text, isActive ? ImGuiColors.HealerGreen : ImGuiColors.DalamudOrange))
            {
                ImGui.TextUnformatted($"{(isActive ? FontAwesomeIcon.Check.ToIconString() : FontAwesomeIcon.Cross.ToIconString())}");
            }
        }
    }
}
