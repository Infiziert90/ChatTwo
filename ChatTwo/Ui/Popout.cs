using System.Numerics;
using Dalamud.Interface.Style;
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
        if (ChatLogWindow.Plugin.Config.OverrideStyle)
        {
            var styles = StyleModel.GetConfiguredStyles();
            styles?.First(style => style.Name.Equals(ChatLogWindow.Plugin.Config.ChosenStyle)).Push();
        }
        Flags = ImGuiWindowFlags.None;
        if (!ChatLogWindow.Plugin.Config.ShowPopOutTitleBar)
            Flags |= ImGuiWindowFlags.NoTitleBar;

        RespectCloseHotkey = false;

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
        if (ChatLogWindow.Plugin.Config.OverrideStyle)
        {
            var styles = StyleModel.GetConfiguredStyles();
            styles?.First(style => style.Name.Equals(ChatLogWindow.Plugin.Config.ChosenStyle)).Pop();
        }
    }

    public override void OnClose()
    {
        ChatLogWindow.PopOutWindows.Remove($"{Tab.Name}{Idx}");
        ChatLogWindow.Plugin.WindowSystem.RemoveWindow(this);

        Tab.PopOut = false;
        ChatLogWindow.Plugin.SaveConfig();
    }
}
