using System.Numerics;
using Dalamud.Interface.Style;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace ChatTwo.Ui;

internal class Popout : Window
{
    private readonly ChatLogWindow ChatLogWindow;
    private readonly Tab Tab;
    private readonly int Idx;

    private long FrameTime; // set every frame
    private long LastActivityTime = Environment.TickCount64;

    public Popout(ChatLogWindow chatLogWindow, Tab tab, int idx) : base($"{tab.Name}##popout")
    {
        ChatLogWindow = chatLogWindow;
        Tab = tab;
        Idx = idx;

        Size = new Vector2(350, 350);
        SizeCondition = ImGuiCond.FirstUseEver;

        IsOpen = true;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
    }

    public override void PreOpenCheck()
    {
        if (!Tab.PopOut)
            IsOpen = false;
    }

    public override bool DrawConditions()
    {
        FrameTime = Environment.TickCount64;
        if (ChatLogWindow.IsHidden)
            return false;

        if (!Plugin.Config.HideWhenInactive || (!Plugin.Config.InactivityHideActiveDuringBattle && ChatLogWindow.InBattle) || !Tab.UnhideOnActivity)
        {
            LastActivityTime = FrameTime;
            return true;
        }

        // Activity in the tab, this popout window, or the main chat log window.
        var lastActivityTime = Math.Max(Tab.LastActivity, LastActivityTime);
        lastActivityTime = Math.Max(lastActivityTime, ChatLogWindow.LastActivityTime);
        return FrameTime - lastActivityTime <= 1000 * Plugin.Config.InactivityHideTimeout;
    }

    public override void PreDraw()
    {
        if (Plugin.Config is { OverrideStyle: true, ChosenStyle: not null })
            StyleModel.GetConfiguredStyles()?.FirstOrDefault(style => style.Name == Plugin.Config.ChosenStyle)?.Push();

        Flags = ImGuiWindowFlags.None;
        if (!Plugin.Config.ShowPopOutTitleBar)
            Flags |= ImGuiWindowFlags.NoTitleBar;

        if (!Tab.CanMove)
            Flags |= ImGuiWindowFlags.NoMove;

        if (!Tab.CanResize)
            Flags |= ImGuiWindowFlags.NoResize;

        if (!ChatLogWindow.PopOutDocked[Idx])
        {
            var alpha = Tab.IndependentOpacity ? Tab.Opacity : Plugin.Config.WindowAlpha;
            BgAlpha = alpha / 100f;
        }
    }

    public override void Draw()
    {
        using var id = ImRaii.PushId($"popout-{Tab.Identifier}");

        if (!Plugin.Config.ShowPopOutTitleBar)
        {
            ImGui.TextUnformatted(Tab.Name);
            ImGui.Separator();
        }

        var handler = ChatLogWindow.HandlerLender.Borrow();
        ChatLogWindow.DrawMessageLog(Tab, handler, ImGui.GetContentRegionAvail().Y, false);

        if (ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows))
            LastActivityTime = FrameTime;
    }

    public override void PostDraw()
    {
        ChatLogWindow.PopOutDocked[Idx] = ImGui.IsWindowDocked();

        if (Plugin.Config is { OverrideStyle: true, ChosenStyle: not null })
            StyleModel.GetConfiguredStyles()?.FirstOrDefault(style => style.Name == Plugin.Config.ChosenStyle)?.Pop();
    }

    public override void OnClose()
    {
        ChatLogWindow.PopOutWindows.Remove(Tab.Identifier);
        ChatLogWindow.Plugin.WindowSystem.RemoveWindow(this);

        Tab.PopOut = false;
        ChatLogWindow.Plugin.SaveConfig();
    }
}
