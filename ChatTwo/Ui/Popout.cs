using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace ChatTwo.Ui;

internal class Popout : Window
{
    private readonly ChatLogWindow ChatLogWindow;
    private readonly Tab Tab;
    private readonly int Idx;

    public Popout(ChatLogWindow chatLogWindow, Tab tab, int idx) : base($"{tab.Name}##popout")
    {
        ChatLogWindow = chatLogWindow;
        Tab = tab;
        Idx = idx;

        Size = new Vector2(350, 350);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void PreDraw()
    {
        Flags = ImGuiWindowFlags.None;
        if (!ChatLogWindow.Plugin.Config.ShowPopOutTitleBar)
            Flags |= ImGuiWindowFlags.NoTitleBar;

        if (!ChatLogWindow.PopOutDocked[Idx]) {
            var alpha = Tab.IndependentOpacity ? Tab.Opacity : ChatLogWindow.Plugin.Config.WindowAlpha;
            BgAlpha = alpha / 100f;
        }
    }

    public override void Draw()
    {
        ImGui.PushID($"popout-{Tab.Name}");

        if (!ChatLogWindow.Plugin.Config.ShowPopOutTitleBar) {
            ImGui.TextUnformatted(Tab.Name);
            ImGui.Separator();
        }

        var handler = ChatLogWindow.HandlerLender.Borrow();
        ChatLogWindow.DrawMessageLog(Tab, handler, ImGui.GetContentRegionAvail().Y, false);

        ImGui.PopID();
    }

    public override void PostDraw()
    {
        ChatLogWindow.PopOutDocked[Idx] = ImGui.IsWindowDocked();
    }

    public override void OnClose()
    {
        ChatLogWindow.PopOutWindows.Remove($"{Tab.Name}{Idx}");
        ChatLogWindow.Plugin.WindowSystem.RemoveWindow(this);

        Tab.PopOut = false;
        ChatLogWindow.Plugin.SaveConfig();
    }
}
