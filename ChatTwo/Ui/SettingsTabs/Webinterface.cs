using ChatTwo.Resources;
using ChatTwo.Util;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.ImGuiNotification;
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
        if (ImGui.CollapsingHeader("Usage Notice", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGuiUtil.WrappedTextWithColor(ImGuiColors.DalamudWhite, Language.Options_Webinterface_Warning_Header);
            ImGui.Spacing();
            ImGuiUtil.WrappedTextWithColor(ImGuiColors.DalamudOrange, Language.Options_Webinterface_Warning_Reason);

            ImGui.Spacing();
            ImGui.Spacing();
            ImGuiUtil.WrappedTextWithColor(ImGuiColors.DalamudViolet, Language.Options_Webinterface_Warning_DoNot);
            using (ImRaii.PushIndent(15.0f))
            {
                ImGuiUtil.WrappedTextWithColor(ImGuiColors.DalamudViolet, Language.Options_Webinterface_DoNot_Port);
                ImGuiUtil.WrappedTextWithColor(ImGuiColors.DalamudViolet, Language.Options_Webinterface_DoNot_Share);
                ImGuiUtil.WrappedTextWithColor(ImGuiColors.DalamudViolet, Language.Options_Webinterface_DoNot_Multibox);
            }
            ImGui.Spacing();
            ImGuiUtil.WrappedTextWithColor(ImGuiColors.DalamudOrange, Language.Options_Webinterface_Warning_Support);

            ImGui.Spacing();
        }

        ImGui.Separator();
        ImGui.Spacing();

        ImGuiUtil.OptionCheckbox(ref Mutable.WebinterfaceEnabled, Language.Options_WebinterfaceEnable_Name, Language.Options_WebinterfaceEnable_Description);
        ImGui.Spacing();

        if (!Mutable.WebinterfaceEnabled)
            return;
        ImGui.Spacing();

        ImGuiUtil.OptionCheckbox(ref Mutable.WebinterfaceAutoStart, Language.Options_WebinterfaceAutoStart_Name, Language.Options_WebinterfaceAutoStart_Description);
        ImGui.Spacing();

        if (ImGuiUtil.InputIntVertical(Language.Webinterface_Option_Port_Name, Language.Webinterface_Option_Port_Description, ref Mutable.WebinterfacePort))
            Mutable.WebinterfacePort = Math.Clamp(Mutable.WebinterfacePort, 1024, 49151);
        ImGui.Spacing();

        ImGuiUtil.WrappedTextWithColor(ImGuiColors.DalamudOrange, Language.Webinterface_CurrentPassword);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(Mutable.WebinterfacePassword);
        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Recycle, tooltip: Language.Webinterface_PasswordReset_Tooltip))
        {
            Mutable.WebinterfacePassword = WebinterfaceUtil.GenerateSimpleAuthCode();
            Plugin.ServerCore.InvalidateSessions();
        }

        ImGui.TextUnformatted(Language.Webinterface_Controls);
        using (ImRaii.PushIndent(10.0f))
        {
            var isActive = Plugin.ServerCore.IsActive();
            using (ImRaii.Disabled(isActive || Plugin.ServerCore.IsStopping()))
            {
                if (ImGui.Button(Language.Webinterface_Button_Start))
                {
                    Task.Run(() =>
                    {
                        var ok = Plugin.ServerCore.Start();
                        if (ok)
                        {
                            Plugin.ServerCore.Run();
                            WrapperUtil.AddNotification(Language.Webinterface_Start_Success, NotificationType.Success);
                        }
                        else
                        {
                            WrapperUtil.AddNotification(Language.Webinterface_Start_Failed, NotificationType.Error);
                        }
                    });
                }
            }

            ImGui.SameLine();

            using (ImRaii.Disabled(!isActive || Plugin.ServerCore.IsStopping()))
            {
                if (ImGui.Button(Language.Webinterface_Button_Stop))
                {
                    Task.Run(async () =>
                    {
                        var ok = await Plugin.ServerCore.Stop();
                        if (ok)
                            WrapperUtil.AddNotification(Language.Webinterface_Stop_Success, NotificationType.Success);
                        else
                            WrapperUtil.AddNotification(Language.Webinterface_Stop_Failed, NotificationType.Error);
                    });
                }
            }

            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(Language.Webinterface_Controls_Active);
            ImGui.SameLine();
            using (Plugin.FontManager.FontAwesome.Push())
            using (ImRaii.PushColor(ImGuiCol.Text, isActive ? ImGuiColors.HealerGreen : ImGuiColors.DalamudRed))
            {
                ImGui.TextUnformatted(isActive ? FontAwesomeIcon.Check.ToIconString() : FontAwesomeIcon.Times.ToIconString());
            }

            Uri? uri;
            try {
                uri = new Uri($"http://{System.Net.Dns.GetHostName()}:{Mutable.WebinterfacePort}/");
            }
            catch(Exception)
            {
                uri = null;
            }

            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(Language.Webinterface_Controls_Url);
            ImGui.SameLine();
            if (uri is not null)
            {
                var clicked = false;
                clicked |= ImGui.Selectable(uri.AbsoluteUri);
                ImGui.SameLine();
                clicked |= ImGuiUtil.IconButton(FontAwesomeIcon.ExternalLinkAlt, "urlOpen");

                if (clicked)
                    WrapperUtil.TryOpenURI(uri);
            }
            else
            {
                ImGui.TextUnformatted(Language.Options_Webinterface_Hostname_Fail);
            }

            ImGuiUtil.WrappedTextWithColor(ImGuiColors.HealerGreen, Language.Options_Webinterface_Note);
        }

        ImGui.Spacing();
        ImGui.Spacing();
    }
}
