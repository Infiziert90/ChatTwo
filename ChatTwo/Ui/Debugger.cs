using System.Numerics;
using ChatTwo.Code;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using Lumina.Text.ReadOnly;

namespace ChatTwo.Ui;

public class DebuggerWindow : Window
{
    private readonly Plugin Plugin;
    private readonly ChatLogWindow ChatLogWindow;

    public DebuggerWindow(Plugin plugin) : base($"Debugger###chat2-debugger")
    {
        Plugin = plugin;
        ChatLogWindow = plugin.ChatLogWindow;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(475, 600),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        RespectCloseHotkey = false;
        DisableWindowSounds = true;

        #if DEBUG
        Plugin.Commands.Register("/chat2Debugger", showInHelp: false).Execute += Toggle;
        #endif
    }

    public void Dispose()
    {
        #if DEBUG
        Plugin.Commands.Register("/chat2Debugger", showInHelp: false).Execute -= Toggle;
        #endif
    }

    private void Toggle(string _, string __) => Toggle();

    public override unsafe void Draw()
    {
        var agent = (nint) AgentItemDetail.Instance();
        ImGui.TextUnformatted($"Current Cursor Pos: {ChatLogWindow.CursorPos}");
        if (ImGui.Selectable($"Agent Address: {agent:X}"))
            ImGui.SetClipboardText(agent.ToString("X"));

        ImGuiHelpers.ScaledDummy(5.0f);

        ImGui.TextUnformatted($"Handle Tooltips: {ChatLogWindow.PayloadHandler.HandleTooltips}");
        ImGui.TextUnformatted($"Hovered Item: {ChatLogWindow.PayloadHandler.HoveredItem}");
        ImGui.TextUnformatted($"Hover Counter: {ChatLogWindow.PayloadHandler.HoverCounter}");
        ImGui.TextUnformatted($"Last Hover Counter: {ChatLogWindow.PayloadHandler.LastHoverCounter}");

        ImGuiHelpers.ScaledDummy(5.0f);

        ImGui.TextColored(ImGuiColors.DalamudOrange, "Current Tab");
        ImGui.TextUnformatted($"Name: {Plugin.CurrentTab.Name}");
        ImGui.TextUnformatted($"Channel: {Plugin.CurrentTab.CurrentChannel.Channel.ToChatType().Name()}");
        ImGui.TextUnformatted($"Tell Target: {Plugin.CurrentTab.CurrentChannel.TellTarget?.ToTargetString() ?? "Null"}");
        ImGui.TextUnformatted($"Use Temp? {Plugin.CurrentTab.CurrentChannel.UseTempChannel}");
        ImGui.TextUnformatted($"Temp Channel: {Plugin.CurrentTab.CurrentChannel.TempChannel.ToChatType().Name()}");
        ImGui.TextUnformatted($"Temp Tell Target: {Plugin.CurrentTab.CurrentChannel.TempTellTarget?.ToTargetString() ?? "Null"}");
        ImGui.TextUnformatted($"Name Set? {Plugin.CurrentTab.CurrentChannel.Name.Count > 0}");
        ImGui.TextUnformatted($"Name {string.Join(" ", Plugin.CurrentTab.CurrentChannel.Name.Select(c => c.StringValue()))}");

        ImGuiHelpers.ScaledDummy(5.0f);

        ImGui.TextColored(ImGuiColors.DalamudOrange, "Vanilla Chat");
        ImGui.TextUnformatted($"Channel: {new ReadOnlySeString(AgentChatLog.Instance()->ChannelLabel).ExtractText()}");
    }
}
