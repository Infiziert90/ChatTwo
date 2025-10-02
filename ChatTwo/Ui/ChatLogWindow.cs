﻿using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using ChatTwo.Code;
using ChatTwo.GameFunctions;
using ChatTwo.GameFunctions.Types;
using ChatTwo.Resources;
using ChatTwo.Util;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Interface.Style;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.UI;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;

namespace ChatTwo.Ui;

public sealed class ChatLogWindow : Window
{
    private const string ChatChannelPicker = "chat-channel-picker";
    private const string AutoCompleteId = "##chat2-autocomplete";

    private const ImGuiInputTextFlags InputFlags = ImGuiInputTextFlags.CallbackAlways | ImGuiInputTextFlags.CallbackCharFilter |
                                                   ImGuiInputTextFlags.CallbackCompletion | ImGuiInputTextFlags.CallbackHistory;

    internal Plugin Plugin { get; }

    internal bool ScreenshotMode;
    private string Salt { get; }

    internal Vector4 DefaultText { get; set; }

    internal bool FocusedPreview;
    internal bool Activate;
    private int ActivatePos = -1;
    internal string Chat = string.Empty;
    private readonly List<string> InputBacklog = [];
    private int InputBacklogIdx = -1;
    public bool TellSpecial;
    private readonly Stopwatch LastResize = new();
    private AutoCompleteInfo? AutoCompleteInfo;
    private bool AutoCompleteOpen;
    private List<AutoTranslateEntry>? AutoCompleteList;
    private bool FixCursor;
    private int AutoCompleteSelection;
    private bool AutoCompleteShouldScroll;

    // Used to detect channel changes for the webinterface
    public Chunk[] PreviousChannel = [];

    public int CursorPos;

    public Vector2 LastWindowPos { get; private set; } = Vector2.Zero;
    public Vector2 LastWindowSize { get; private set; } = Vector2.Zero;

    public unsafe ImGuiViewport* LastViewport;
    private bool WasDocked;

    public PayloadHandler PayloadHandler { get; }
    internal Lender<PayloadHandler> HandlerLender { get; }
    private Dictionary<string, ChatType> TextCommandChannels { get; } = new();
    private HashSet<string> AllCommands { get; } = [];

    private const uint ChatOpenSfx = 35u;
    private const uint ChatCloseSfx = 3u;
    private bool PlayedClosingSound = true;

    private long FrameTime; // set every frame
    internal long LastActivityTime = Environment.TickCount64;

    internal ChatLogWindow(Plugin plugin) : base($"{Plugin.PluginName}###chat2")
    {
        Plugin = plugin;
        Salt = new Random().Next().ToString();

        Size = new Vector2(500, 250);
        SizeCondition = ImGuiCond.FirstUseEver;

        PositionCondition = ImGuiCond.Always;

        IsOpen = true;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;

        PayloadHandler = new PayloadHandler(this);
        HandlerLender = new Lender<PayloadHandler>(() => new PayloadHandler(this));

        SetUpTextCommandChannels();
        SetUpAllCommands();

        Plugin.Commands.Register("/clearlog2", "Clear the Chat 2 chat log").Execute += ClearLog;
        Plugin.Commands.Register("/chat2").Execute += ToggleChat;

        Plugin.ClientState.Login += Login;
        Plugin.ClientState.Logout += Logout;

        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "ItemDetail", PayloadHandler.MoveTooltip);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "ActionDetail", PayloadHandler.MoveTooltip);
    }

    public void Dispose()
    {
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "ItemDetail", PayloadHandler.MoveTooltip);
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "ActionDetail", PayloadHandler.MoveTooltip);
        Plugin.ClientState.Logout -= Logout;
        Plugin.ClientState.Login -= Login;
        Plugin.Commands.Register("/chat2").Execute -= ToggleChat;
        Plugin.Commands.Register("/clearlog2").Execute -= ClearLog;
    }

    private void Logout(int _, int __)
    {
        Plugin.MessageManager.ClearAllTabs();
    }

    private void Login()
    {
        Plugin.MessageManager.FilterAllTabsAsync();
    }

    internal unsafe void Activated(ChatActivatedArgs args)
    {
        TellSpecial = args.TellSpecial;

        Activate = true;
        PlayedClosingSound = false;
        if (Plugin.Config.PlaySounds)
            UIGlobals.PlaySoundEffect(ChatOpenSfx);

        // Don't set the channel or text content when activating a disabled tab.
        if (Plugin.CurrentTab.InputDisabled)
        {
            // The closing sound would've been immediately played in this case.
            PlayedClosingSound = true;
            return;
        }

        if (args.AddIfNotPresent != null && !Chat.Contains(args.AddIfNotPresent))
            Chat += args.AddIfNotPresent;

        if (args.Input != null)
            Chat += args.Input;

        var (info, reason, target) = (args.ChannelSwitchInfo, args.TellReason, args.TellTarget);

        if (info.Channel != null)
        {
            var targetChannel = info.Channel;
            if (info.Channel is InputChannel.Tell)
            {
                if (info.Rotate != RotateMode.None)
                {
                    var idx = Plugin.CurrentTab.CurrentChannel.TempChannel != InputChannel.Tell
                        ? 0 : info.Rotate == RotateMode.Reverse
                            ? -1 : 1;

                    var tellInfo = Plugin.Functions.Chat.GetTellHistoryInfo(idx);
                    if (tellInfo != null && reason != null)
                        Plugin.CurrentTab.CurrentChannel.TempTellTarget = new TellTarget(tellInfo.Name, (ushort) tellInfo.World, tellInfo.ContentId, reason.Value);
                }
                else
                {
                    Plugin.CurrentTab.CurrentChannel.TellTarget = null;
                    if (target != null)
                    {
                        if (info.Permanent)
                        {
                            Plugin.CurrentTab.CurrentChannel.TellTarget = target;
                        }
                        else
                        {
                            Plugin.CurrentTab.CurrentChannel.UseTempChannel = true;
                            Plugin.CurrentTab.CurrentChannel.TempTellTarget = target;
                        }
                    }
                }
            }
            else
            {
                Plugin.CurrentTab.CurrentChannel.TellTarget = null;
            }

            if (info.Channel is InputChannel.Linkshell1 or InputChannel.CrossLinkshell1 && info.Rotate != RotateMode.None)
            {
                var module = UIModule.Instance();

                // If any of these operations fail, do nothing.
                if (info.Permanent)
                {
                    // Rotate using the game's code.
                    if (info.Channel == InputChannel.Linkshell1)
                    {
                        GameFunctions.Chat.RotateLinkshellHistory(info.Rotate);
                        targetChannel = info.Channel + (uint)module->LinkshellCycle;
                    }
                    else
                    {
                        GameFunctions.Chat.RotateCrossLinkshellHistory(info.Rotate);
                        targetChannel = info.Channel + (uint)module->CrossWorldLinkshellCycle;
                    }
                }
                else
                {
                    targetChannel = GameFunctions.Chat.ResolveTempInputChannel(Plugin.CurrentTab.CurrentChannel.TempChannel, info.Channel.Value, info.Rotate);
                }
            }

            if (targetChannel == null || !GameFunctions.Chat.ValidAnyLinkshell(targetChannel.Value))
            {
                Plugin.Log.Warning($"Channel was set to an invalid value '{targetChannel}', ignoring");
                return;
            }

            if (info.Permanent)
            {
                SetChannel(targetChannel);
            }
            else
            {
                Plugin.CurrentTab.CurrentChannel.UseTempChannel = true;
                Plugin.CurrentTab.CurrentChannel.TempChannel = targetChannel.Value;
            }
        }

        if (info.Text != null && Chat.Length == 0)
            Chat = info.Text;
    }

    private bool IsValidCommand(string command)
    {
        return Plugin.CommandManager.Commands.ContainsKey(command) || AllCommands.Contains(command);
    }

    private void ClearLog(string command, string arguments)
    {
        switch (arguments)
        {
            case "all":
                Plugin.MessageManager.ClearAllTabs();
                break;
            case "help":
                Plugin.ChatGui.Print("- /clearlog2: clears the active tab's log");
                Plugin.ChatGui.Print("- /clearlog2 all: clears all tabs' logs and the global history");
                Plugin.ChatGui.Print("- /clearlog2 help: shows this help");
                break;
            default:
                if (Plugin.LastTab > -1 && Plugin.LastTab < Plugin.Config.Tabs.Count)
                    Plugin.Config.Tabs[Plugin.LastTab].Clear();
                break;
        }
    }

    private void ToggleChat(string _, string arguments)
    {
        switch (arguments)
        {
            case "hide":
                CurrentHideState = HideState.User;
                break;
            case "show":
                CurrentHideState = HideState.None;
                break;
            case "toggle":
                CurrentHideState = CurrentHideState switch
                {
                    HideState.User or HideState.CutsceneOverride => HideState.None,
                    HideState.Cutscene => HideState.CutsceneOverride,
                    HideState.None => HideState.User,
                    _ => CurrentHideState,
                };
                break;
        }
    }

    private void SetUpTextCommandChannels()
    {
        TextCommandChannels.Clear();

        foreach (var input in Enum.GetValues<InputChannel>())
        {
            var commands = input.TextCommands(Plugin.DataManager);
            if (commands == null)
                continue;

            var type = input.ToChatType();
            foreach (var command in commands)
                AddTextCommandChannel(command, type);
        }

        if (Sheets.TextCommandSheet.HasRow(116))
        {
            var echo = Sheets.TextCommandSheet.GetRow(116);
            AddTextCommandChannel(echo, ChatType.Echo);
        }
    }

    private void AddTextCommandChannel(TextCommand command, ChatType type)
    {
        TextCommandChannels[command.Command.ExtractText()] = type;
        TextCommandChannels[command.ShortCommand.ExtractText()] = type;
        TextCommandChannels[command.Alias.ExtractText()] = type;
        TextCommandChannels[command.ShortAlias.ExtractText()] = type;
    }

    private void SetUpAllCommands()
    {
        if (Plugin.DataManager.GetExcelSheet<TextCommand>() is not { } commands)
            return;

        var commandNames = commands.SelectMany(cmd => new[]
        {
            cmd.Command.ExtractText(),
            cmd.ShortCommand.ExtractText(),
            cmd.Alias.ExtractText(),
            cmd.ShortAlias.ExtractText(),
        });

        foreach (var command in commandNames)
            AllCommands.Add(command);
    }

    private void AddBacklog(string message)
    {
        for (var i = 0; i < InputBacklog.Count; i++)
        {
            if (InputBacklog[i] != message)
                continue;

            InputBacklog.RemoveAt(i);
            break;
        }

        InputBacklog.Add(message);
    }

    private float GetRemainingHeightForMessageLog()
    {
        var lineHeight = ImGui.CalcTextSize("A").Y;
        var height = ImGui.GetContentRegionAvail().Y - lineHeight * 2 - ImGui.GetStyle().ItemSpacing.Y - ImGui.GetStyle().FramePadding.Y * 2;

        if (Plugin.Config.PreviewPosition is PreviewPosition.Inside)
            height -= Plugin.InputPreview.PreviewHeight;

        return height;
    }

    internal void ChangeTab(int index) {
        Plugin.WantedTab = index;
        LastActivityTime = FrameTime;
    }

    internal void ChangeTabDelta(int offset)
    {
        var newIndex = (Plugin.LastTab + offset) % Plugin.Config.Tabs.Count;
        while (newIndex < 0)
            newIndex += Plugin.Config.Tabs.Count;
        ChangeTab(newIndex);
    }

    private void TabSwitched(Tab newTab, Tab previousTab)
    {
        // Use the fixed channel if set by the user, or set it to the current tabs channel if this tab wasn't accessed before
        if (newTab.Channel is not null)
            newTab.CurrentChannel.Channel = newTab.Channel.Value;
        else if (newTab.CurrentChannel.Channel is InputChannel.Invalid)
            newTab.CurrentChannel = previousTab.CurrentChannel;

        SetChannel(newTab.CurrentChannel.Channel);

        // Inform the webinterface about tab switch
        // TODO implement tabs in the webinterface
        Plugin.ServerCore.SendNewLogin();
    }

    private enum HideState
    {
        None,
        Cutscene,
        CutsceneOverride,
        User,
        Battle,
        NewGamePlus,
    }

    private HideState CurrentHideState = HideState.None;

    public bool IsHidden;
    public void HideStateCheck()
    {
        // if the chat has no hide state set, and the player has entered battle, we hide chat if they have configured it
        if (Plugin.Config.HideInBattle && CurrentHideState == HideState.None && Plugin.InBattle)
            CurrentHideState = HideState.Battle;

        // If the chat is hidden because of battle, we reset it here
        if (CurrentHideState is HideState.Battle && !Plugin.InBattle)
            CurrentHideState = HideState.None;

        // if the chat has no hide state and in a cutscene, set the hide state to cutscene
        if (Plugin.Config.HideDuringCutscenes && CurrentHideState == HideState.None && (Plugin.CutsceneActive || Plugin.GposeActive))
        {
            if (Plugin.Functions.Chat.CheckHideFlags())
                CurrentHideState = HideState.Cutscene;
        }

        var newGamePlusOpen = GameFunctions.GameFunctions.IsAddonInteractable("QuestRedo");
        if (Plugin.Config.HideInNewGamePlusMenu && CurrentHideState == HideState.None && newGamePlusOpen)
            CurrentHideState = HideState.NewGamePlus;

        if (CurrentHideState is HideState.NewGamePlus && !newGamePlusOpen)
            CurrentHideState = HideState.None;

        // if the chat is hidden because of a cutscene and no longer in a cutscene, set the hide state to none
        if (CurrentHideState is HideState.Cutscene or HideState.CutsceneOverride && !Plugin.CutsceneActive && !Plugin.GposeActive)
            CurrentHideState = HideState.None;

        // if the chat is hidden because of a cutscene and the chat has been activated, show chat
        if (CurrentHideState == HideState.Cutscene && Activate)
            CurrentHideState = HideState.CutsceneOverride;

        // if the user hid the chat and is now activating chat, reset the hide state
        if (CurrentHideState == HideState.User && Activate)
            CurrentHideState = HideState.None;

        if (CurrentHideState is HideState.Cutscene or HideState.User or HideState.Battle or HideState.NewGamePlus || (Plugin.Config.HideWhenNotLoggedIn && !Plugin.ClientState.IsLoggedIn))
        {
            IsHidden = true;
            return;
        }

        IsHidden = false;
    }

    public override unsafe void PreOpenCheck()
    {
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoFocusOnAppearing;
        if (!Plugin.Config.CanMove)
            Flags |= ImGuiWindowFlags.NoMove;

        if (!Plugin.Config.CanResize)
            Flags |= ImGuiWindowFlags.NoResize;

        if (!Plugin.Config.ShowTitleBar)
            Flags |= ImGuiWindowFlags.NoTitleBar;

        if (LastViewport == ImGuiHelpers.MainViewport.Handle && !WasDocked)
            BgAlpha = Plugin.Config.WindowAlpha / 100f;

        LastViewport = ImGui.GetWindowViewport().Handle;
        WasDocked = ImGui.IsWindowDocked();
    }

    public override bool DrawConditions()
    {
        FrameTime = Environment.TickCount64;
        if (IsHidden)
            return false;

        if (!Plugin.Config.HideWhenInactive || (!Plugin.Config.InactivityHideActiveDuringBattle && Plugin.InBattle) || Activate)
        {
            LastActivityTime = FrameTime;
            return true;
        }

        var currentTab = Plugin.CurrentTab; // local to avoid calling the getter repeatedly
        var lastActivityTime = Plugin.Config.Tabs
            .Where(tab => !tab.PopOut && (tab.UnhideOnActivity || tab == currentTab))
            .Select(tab => tab.LastActivity)
            .Append(LastActivityTime)
            .Max();
        return FrameTime - lastActivityTime <= 1000 * Plugin.Config.InactivityHideTimeout;
    }

    public override void PreDraw()
    {
        if (Plugin.Config.KeepInputFocus && Activate)
            ImGui.SetWindowFocus(WindowName);

        if (Plugin.Config is { OverrideStyle: true, ChosenStyle: not null })
            StyleModel.GetConfiguredStyles()?.FirstOrDefault(style => style.Name == Plugin.Config.ChosenStyle)?.Push();
    }

    public override void PostDraw()
    {
        // Set Activate to false after draw to avoid repeatedly trying to focus
        // the text input in a tab with input disabled. The usual way that
        // Activate gets disabled is via the text input callback, but that
        // doesn't get called if the input is disabled.
        if (Plugin.CurrentTab.InputDisabled)
            Activate = false;

        if (Plugin.Config is { OverrideStyle: true, ChosenStyle: not null })
            StyleModel.GetConfiguredStyles()?.FirstOrDefault(style => style.Name == Plugin.Config.ChosenStyle)?.Pop();
    }

    public override void OnClose()
    {
        // We force the main log to be always open
        IsOpen = true;
    }

    public override void Draw()
    {
        try
        {
            DrawChatLog();
            AddPopOutsToDraw();
            DrawAutoComplete();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error drawing Chat Log window");
            // Prevent recurring draw failures from constantly trying to grab
            // input focus, which breaks every other ImGui window.
            Activate = false;
        }
    }

    private static bool IsChatMode => Plugin.Config.PreviewPosition is PreviewPosition.Inside or PreviewPosition.Tooltip;
    private unsafe void DrawChatLog()
    {
        // Position change has applied, so we set it to null again
        Position = null;

        var currentSize = ImGui.GetWindowSize();
        var resized = LastWindowSize != currentSize;
        LastWindowSize = currentSize;
        LastWindowPos = ImGui.GetWindowPos();

        if (resized)
            LastResize.Restart();

        LastViewport = ImGui.GetWindowViewport().Handle;
        WasDocked = ImGui.IsWindowDocked();

        if (IsChatMode && Plugin.InputPreview.IsDrawable)
            Plugin.InputPreview.CalculatePreview();

        if (Plugin.Config.SidebarTabView)
            DrawTabSidebar();
        else
            DrawTabBar();

        var activeTab = Plugin.CurrentTab;

        // This tab has a fixed channel, so we force this channel to be always set as current
        if (activeTab.Channel is not null)
            activeTab.CurrentChannel.SetChannel(activeTab.Channel.Value);

        if (Plugin.Config.PreviewPosition is PreviewPosition.Inside && Plugin.InputPreview.IsDrawable)
            Plugin.InputPreview.DrawPreview();

        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero))
        {
            DrawChannelName(activeTab);
        }

        var beforeIcon = ImGui.GetCursorPos();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Comment) && activeTab.Channel is null)
            ImGui.OpenPopup(ChatChannelPicker);

        if (activeTab.Channel is not null && ImGui.IsItemHovered())
            ImGuiUtil.Tooltip(Language.ChatLog_SwitcherDisabled);

        using (var popup = ImRaii.Popup(ChatChannelPicker))
        {
            if (popup)
            {
                var channels = GetValidChannels();
                foreach (var (name, channel) in channels)
                    if (ImGui.Selectable(name))
                        SetChannel(channel);
            }
        }

        ImGui.SameLine();
        var afterIcon = ImGui.GetCursorPos();

        var buttonWidth = afterIcon.X - beforeIcon.X;
        var showNovice = Plugin.Config.ShowNoviceNetwork && GameFunctions.GameFunctions.IsMentor();
        var buttonsRight = (showNovice ? 1 : 0) + (Plugin.Config.ShowHideButton ? 1 : 0);
        var inputWidth = ImGui.GetContentRegionAvail().X - buttonWidth * (1 + buttonsRight);

        var inputType = activeTab.CurrentChannel.UseTempChannel ? activeTab.CurrentChannel.TempChannel.ToChatType() : activeTab.CurrentChannel.Channel.ToChatType();
        var isCommand = Chat.Trim().StartsWith('/');
        if (isCommand)
        {
            var command = Chat.Split(' ')[0];
            if (TextCommandChannels.TryGetValue(command, out var channel))
                inputType = channel;

            if (!IsValidCommand(command))
                inputType = ChatType.Error;
        }

        var normalColor = ImGui.GetColorU32(ImGuiCol.Text);
        var inputColour = Plugin.Config.ChatColours.TryGetValue(inputType, out var inputCol) ? inputCol : inputType.DefaultColor();

        if (!isCommand && Plugin.ExtraChat.ChannelOverride is var (_, overrideColour))
            inputColour = overrideColour;

        if (isCommand && Plugin.ExtraChat.ChannelCommandColours.TryGetValue(Chat.Split(' ')[0], out var ecColour))
            inputColour = ecColour;

        var push = inputColour != null;
        using (ImRaii.PushColor(ImGuiCol.Text, push ? ColourUtil.RgbaToAbgr(inputColour!.Value) : 0, push))
        {
            var isChatEnabled = activeTab is { InputDisabled: false };
            if (isChatEnabled && (Activate || FocusedPreview))
            {
                FocusedPreview = false;
                ImGui.SetKeyboardFocusHere();
            }

            var chatCopy = Chat;
            using (ImRaii.Disabled(!isChatEnabled))
            {
                var flags = InputFlags | (!isChatEnabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None);
                ImGui.SetNextItemWidth(inputWidth);
                ImGui.InputTextWithHint("##chat2-input", isChatEnabled ? "": Language.ChatLog_DisabledInput, ref Chat, 500, flags, Callback);
            }

            var tooltipDraw = Plugin.Config.PreviewPosition is PreviewPosition.Tooltip && Plugin.InputPreview.IsDrawable;
            if (tooltipDraw && ImGui.IsItemHovered())
            {
                ImGui.SetNextWindowSize(new Vector2(500 * ImGuiHelpers.GlobalScale, -1));
                using var tooltip = ImRaii.Tooltip();
                if (tooltip)
                    Plugin.InputPreview.DrawPreview();
            }

            if (ImGui.IsItemDeactivated())
            {
                if (ImGui.IsKeyDown(ImGuiKey.Escape))
                {
                    Chat = chatCopy;

                    if (activeTab.CurrentChannel.UseTempChannel)
                    {
                        activeTab.CurrentChannel.ResetTempChannel();
                        SetChannel(activeTab.CurrentChannel.Channel);
                    }
                }

                if (ImGui.IsKeyDown(ImGuiKey.Enter) || ImGui.IsKeyDown(ImGuiKey.KeypadEnter))
                {
                    Plugin.CommandHelpWindow.IsOpen = false;
                    SendChatBox(activeTab);

                    if (activeTab.CurrentChannel.UseTempChannel)
                    {
                        activeTab.CurrentChannel.ResetTempChannel();
                        SetChannel(activeTab.CurrentChannel.Channel);
                    }
                }
            }

            // Process keybinds that have modifiers while the chat is focused.
            if (ImGui.IsItemActive())
            {
                Plugin.Functions.KeybindManager.HandleKeybinds(KeyboardSource.ImGui, true, true);
                LastActivityTime = FrameTime;
            }

            // Only trigger unfocused if we are currently not calling the auto complete
            if (!Activate && !ImGui.IsItemActive() && AutoCompleteInfo == null)
            {
                if (Plugin.Config.PlaySounds && !PlayedClosingSound)
                {
                    PlayedClosingSound = true;
                    UIGlobals.PlaySoundEffect(ChatCloseSfx);
                }

                if (activeTab.CurrentChannel.UseTempChannel)
                {
                    activeTab.CurrentChannel.ResetTempChannel();
                    SetChannel(Plugin.CurrentTab.CurrentChannel.Channel);
                }
            }

            using (var context = ImRaii.ContextPopupItem("ChatInputContext"))
            {
                if (context)
                {
                    using var pushedColor = ImRaii.PushColor(ImGuiCol.Text, normalColor);
                    if (ImGui.Selectable(Language.ChatLog_HideChat))
                        UserHide();
                }
            }
        }

        ImGui.SameLine();

        if (ImGuiUtil.IconButton(FontAwesomeIcon.Cog, width: (int)buttonWidth))
            Plugin.SettingsWindow.Toggle();

        if (Plugin.Config.ShowHideButton)
        {
            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.EyeSlash, width: (int)buttonWidth))
                UserHide();
        }

        if (ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows))
            LastActivityTime = FrameTime;

        if (!showNovice)
            return;

        ImGui.SameLine();

        if (ImGuiUtil.IconButton(FontAwesomeIcon.Leaf))
            GameFunctions.GameFunctions.ClickNoviceNetworkButton();
    }

    internal Dictionary<string, InputChannel> GetValidChannels()
    {
        var channels = new Dictionary<string, InputChannel>();
        foreach (var channel in Enum.GetValues<InputChannel>())
        {
            if (!channel.IsValid())
                continue;

            var name = Sheets.LogFilterSheet.FirstOrNull(row => row.LogKind == (byte) channel.ToChatType())?.Name.ExtractText() ?? channel.ToChatType().Name();
            if (channel.IsLinkshell())
            {
                var lsName = Plugin.Functions.Chat.GetLinkshellName(channel.LinkshellIndex());
                if (string.IsNullOrWhiteSpace(lsName))
                    continue;

                name += $": {lsName}";
            }

            if (channel.IsCrossLinkshell())
            {
                var lsName = Plugin.Functions.Chat.GetCrossLinkshellName(channel.LinkshellIndex());
                if (string.IsNullOrWhiteSpace(lsName))
                    continue;

                name += $": {lsName}";
            }

            // Check if the linkshell with this index is registered in
            // the ExtraChat plugin by seeing if the command is
            // registered. The command gets registered only if a
            // linkshell is assigned (and even gets unassigned if the
            // index changes!).
            if (channel.IsExtraChatLinkshell())
                if (!Plugin.CommandManager.Commands.ContainsKey(channel.Prefix()))
                    continue;

            channels.Add(name, channel);
        }

        return channels;
    }

    private void DrawChannelName(Tab activeTab)
    {
        var currentChannel = ReadChannelName(activeTab);
        if (!currentChannel.SequenceEqual(PreviousChannel))
        {
            PreviousChannel = currentChannel;
            Plugin.ServerCore?.SendChannelSwitch(currentChannel);
        }

        DrawChunks(currentChannel);
    }

    private Chunk[] ReadChannelName(Tab activeTab)
    {
        Chunk[] channelNameChunks;
        // Check the temp channel before others
        if (activeTab.CurrentChannel.UseTempChannel)
        {
            if (activeTab.CurrentChannel.TempTellTarget != null && activeTab.CurrentChannel.TempTellTarget.IsSet())
            {
                channelNameChunks = GenerateTellTargetName(activeTab.CurrentChannel.TempTellTarget);
            }
            else
            {
                string name;
                if (activeTab.CurrentChannel.TempChannel.IsLinkshell())
                {
                    var idx = (uint) activeTab.CurrentChannel.TempChannel - (uint) InputChannel.Linkshell1;
                    var lsName = Plugin.Functions.Chat.GetLinkshellName(idx);
                    name = $"LS #{idx + 1}: {lsName}";
                }
                else if (activeTab.CurrentChannel.TempChannel.IsCrossLinkshell())
                {
                    var idx = (uint) activeTab.CurrentChannel.TempChannel - (uint) InputChannel.CrossLinkshell1;
                    var cwlsName = Plugin.Functions.Chat.GetCrossLinkshellName(idx);
                    name = $"CWLS [{idx + 1}]: {cwlsName}";
                }
                else
                {
                    name = activeTab.CurrentChannel.TempChannel.ToChatType().Name();
                }

                channelNameChunks = [new TextChunk(ChunkSource.None, null, name)];
            }
        }
        else if (activeTab.CurrentChannel.TellTarget?.IsSet() == true)
        {
            channelNameChunks = GenerateTellTargetName(activeTab.CurrentChannel.TellTarget);
        }
        else if (activeTab is { Channel: { } channel })
        {
            // We cannot lookup ExtraChat channel names from index over
            // IPC so we just don't show the name if it's the tabs channel.
            //
            // We don't call channel.ToChatType().Name() as it has the
            // long name as used in the settings window.
            channelNameChunks = [new TextChunk(ChunkSource.None, null, channel.IsExtraChatLinkshell() ? $"ECLS [{channel.LinkshellIndex() + 1}]" : channel.ToChatType().Name())];
        }
        else if (Plugin.ExtraChat.ChannelOverride is var (overrideName, _))
        {
            // If the current channel is not an ExtraChat Linkshell add a warning for the user
            var warning = activeTab.CurrentChannel.Channel.IsExtraChatLinkshell()
                ? ""
                : $" (Warning: {activeTab.CurrentChannel.Channel.ToChatType().Name()})";

            channelNameChunks = [new TextChunk(ChunkSource.None, null, $"{overrideName}{warning}")];
        }
        else if (ScreenshotMode && activeTab.CurrentChannel.Channel is InputChannel.Tell && activeTab.CurrentChannel.TellTarget != null)
        {
            if (!string.IsNullOrWhiteSpace(activeTab.CurrentChannel.TellTarget.Name) && activeTab.CurrentChannel.TellTarget.World != 0)
            {
                // Note: don't use HidePlayerInString here because abbreviation settings do not affect this.
                var playerName = HashPlayer(activeTab.CurrentChannel.TellTarget.Name, activeTab.CurrentChannel.TellTarget.World);
                var world = Sheets.WorldSheet.TryGetRow(activeTab.CurrentChannel.TellTarget.World, out var worldRow)
                    ? worldRow.Name.ExtractText()
                    : "???";

                channelNameChunks =
                [
                    new TextChunk(ChunkSource.None, null, "Tell "),
                    new TextChunk(ChunkSource.None, null, playerName),
                    new IconChunk(ChunkSource.None, null, BitmapFontIcon.CrossWorld),
                    new TextChunk(ChunkSource.None, null, world)
                ];
            }
            else
            {
                // We still need to censor the name if we couldn't read valid data.
                channelNameChunks = [new TextChunk(ChunkSource.None, null, "Tell")];
            }
        }
        else
        {
            channelNameChunks = activeTab.CurrentChannel.Name.Count > 0
                ? activeTab.CurrentChannel.Name.ToArray()
                : [new TextChunk(ChunkSource.None, null, activeTab.CurrentChannel.Channel.ToChatType().Name())];
        }

        return channelNameChunks;
    }

    internal void SetChannel(InputChannel? channel)
    {
        channel ??= InputChannel.Say;
        if (channel != InputChannel.Tell)
        {
            Plugin.CurrentTab.CurrentChannel.TellTarget = null;
            Plugin.CurrentTab.CurrentChannel.TempTellTarget = null;
        }

        // Instead of calling SetChannel(), we ask the ExtraChat plugin to set a
        // channel override by just calling the command directly.
        if (channel.Value.IsExtraChatLinkshell())
        {
            // Check that the command is registered in Dalamud so the game code
            // never sees the command itself.
            if (!Plugin.CommandManager.Commands.ContainsKey(channel.Value.Prefix()))
                return;

            // Send the command through the game chat. We can't call
            // ICommandManager.ProcessCommand() here because ExtraChat only
            // registers stub handlers and actually processes its commands in a
            // SendMessage detour.
            var bytes = Encoding.UTF8.GetBytes(channel.Value.Prefix());
            ChatBox.SendMessageUnsafe(bytes);

            Plugin.CurrentTab.CurrentChannel.Channel = channel.Value;
            return;
        }

        var target = Plugin.CurrentTab.CurrentChannel.TempTellTarget ?? Plugin.CurrentTab.CurrentChannel.TellTarget;
        Plugin.Functions.Chat.SetChannel(channel.Value, target);
    }

    private Chunk[] GenerateTellTargetName(TellTarget tellTarget)
    {
        var playerName = tellTarget.Name;
        if (ScreenshotMode)
            // Note: don't use HidePlayerInString here because
            // abbreviation settings do not affect this.
            playerName = HashPlayer(tellTarget.Name, tellTarget.World);

        var world = Sheets.WorldSheet.TryGetRow(tellTarget.World, out var worldRow)
            ? worldRow.Name.ExtractText()
            : "???";

        return
        [
            new TextChunk(ChunkSource.None, null, "Tell "),
            new TextChunk(ChunkSource.None, null, playerName),
            new IconChunk(ChunkSource.None, null, BitmapFontIcon.CrossWorld),
            new TextChunk(ChunkSource.None, null, world)
        ];
    }

    internal void SendChatBox(Tab activeTab)
    {
        if (!string.IsNullOrWhiteSpace(Chat))
        {
            var trimmed = Chat.Trim();
            AddBacklog(trimmed);
            InputBacklogIdx = -1;

            if (TellSpecial)
            {
                var tellBytes = Encoding.UTF8.GetBytes(trimmed);
                AutoTranslate.ReplaceWithPayload(ref tellBytes);

                Plugin.Functions.Chat.SendTellUsingCommandInner(tellBytes);

                TellSpecial = false;

                activeTab.CurrentChannel.ResetTempChannel();
                Chat = string.Empty;
                return;
            }

            if (!trimmed.StartsWith('/'))
            {
                var target = activeTab.CurrentChannel.TempTellTarget ?? activeTab.CurrentChannel.TellTarget;
                if (target != null)
                {
                    // ContentId 0 is a case where we can't directly send messages, so we send a /tell formatted message and let the game handle it
                    if (target.ContentId == 0)
                    {
                        trimmed = $"/tell {target.ToTargetString()} {trimmed}";
                        var tellBytes = Encoding.UTF8.GetBytes(trimmed);
                        AutoTranslate.ReplaceWithPayload(ref tellBytes);

                        ChatBox.SendMessageUnsafe(tellBytes);

                        activeTab.CurrentChannel.ResetTempChannel();
                        Chat = string.Empty;
                        return;
                    }

                    var reason = target.Reason;
                    var world = Sheets.WorldSheet.GetRow(target.World);
                    if (world is { IsPublic: true })
                    {
                        if (reason == TellReason.Reply && GameFunctions.GameFunctions.GetFriends().Any(friend => friend.ContentId == target.ContentId))
                            reason = TellReason.Friend;

                        var tellBytes = Encoding.UTF8.GetBytes(trimmed);
                        AutoTranslate.ReplaceWithPayload(ref tellBytes);

                        Plugin.Functions.Chat.SendTell(reason, target.ContentId, target.Name, (ushort) world.RowId, tellBytes, trimmed);
                    }

                    activeTab.CurrentChannel.ResetTempChannel();
                    Chat = string.Empty;
                    return;
                }

                if (activeTab.CurrentChannel.UseTempChannel)
                    trimmed = $"{activeTab.CurrentChannel.TempChannel.Prefix()} {trimmed}";
                else
                    trimmed = $"{activeTab.CurrentChannel.Channel.Prefix()} {trimmed}";
            }

            var bytes = Encoding.UTF8.GetBytes(trimmed);
            AutoTranslate.ReplaceWithPayload(ref bytes);

            ChatBox.SendMessageUnsafe(bytes);
        }

        activeTab.CurrentChannel.ResetTempChannel();
        Chat = string.Empty;
    }

    internal void UserHide()
    {
        CurrentHideState = HideState.User;
    }

    internal void DrawMessageLog(Tab tab, PayloadHandler handler, float childHeight, bool switchedTab)
    {
        using var child = ImRaii.Child("##chat2-messages", new Vector2(-1, childHeight));
        if (!child.Success)
            return;

        if (tab.DisplayTimestamp && Plugin.Config.PrettierTimestamps)
            DrawLogTableStyle(tab, handler, switchedTab);
        else
            DrawLogNormalStyle(tab, handler, switchedTab);
    }

    private void DrawLogNormalStyle(Tab tab, PayloadHandler handler, bool switchedTab)
    {
        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero))
            DrawMessages(tab, handler, false);

        if (switchedTab || ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
            ImGui.SetScrollHereY(1f);

        handler.Draw();
    }

    private void DrawLogTableStyle(Tab tab, PayloadHandler handler, bool switchedTab)
    {
        var compact = Plugin.Config.MoreCompactPretty;
        var oldItemSpacing = ImGui.GetStyle().ItemSpacing;
        var oldCellPadding = ImGui.GetStyle().CellPadding;

        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero))
        using (ImRaii.PushStyle(ImGuiStyleVar.CellPadding, oldCellPadding with { Y = 0 }, compact))
        {
            using var table = ImRaii.Table("timestamp-table", 2, ImGuiTableFlags.PreciseWidths);
            if (!table.Success)
                return;

            ImGui.TableSetupColumn("timestamps", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("messages", ImGuiTableColumnFlags.WidthStretch);

            DrawMessages(tab, handler, true, compact, oldCellPadding.Y);

            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, oldItemSpacing))
            using (ImRaii.PushStyle(ImGuiStyleVar.CellPadding, oldCellPadding))
            {
                // Custom styles can have cellPadding that go above 4, which GetScrollY isn't respecting
                var cellPaddingOffset = !compact && oldCellPadding.Y > 4f ? oldCellPadding.Y - 4f : 0f;
                if (switchedTab || ImGui.GetScrollY() + cellPaddingOffset >= ImGui.GetScrollMaxY())
                    ImGui.SetScrollHereY(1f);

                handler.Draw();
            }
        }
    }

    private void DrawMessages(Tab tab, PayloadHandler handler, bool isTable, bool moreCompact = false, float oldCellPaddingY = 0)
    {
        try
        {
            // This may produce ApplicationException which is catched below.
            using var messages = tab.Messages.GetReadOnly(3);

            var reset = false;
            if (LastResize is { IsRunning: true, Elapsed.TotalSeconds: > 0.25 })
            {
                LastResize.Stop();
                LastResize.Reset();
                reset = true;
            }

            var lastPosY = ImGui.GetCursorPosY();
            var lastTimestamp = string.Empty;
            int? lastMessageHash = null;
            var sameCount = 0;

            var maxLines = Plugin.Config.MaxLinesToRender;
            var startLine = messages.Count > maxLines ? messages.Count - maxLines : 0;
            for (var i = startLine; i < messages.Count; i++)
            {
                var message = messages[i];
                if (reset)
                {
                    message.Height[tab.Identifier] = null;
                    message.IsVisible[tab.Identifier] = false;
                }

                if (Plugin.Config.CollapseDuplicateMessages)
                {
                    var messageHash = message.Hash;
                    var same = lastMessageHash == messageHash;
                    if (same)
                    {
                        sameCount += 1;
                        message.IsVisible[tab.Identifier] = false;
                        if (i != messages.Count - 1)
                            continue;
                    }

                    if (sameCount > 0)
                    {
                        ImGui.SameLine();
                        DrawChunks(
                            [new TextChunk(ChunkSource.None, null, $" ({sameCount + 1}x)") { FallbackColour = ChatType.System, Italic = true, }],
                            true,
                            handler,
                            ImGui.GetContentRegionAvail().X
                        );
                        sameCount = 0;
                    }

                    lastMessageHash = messageHash;
                    if (same && i == messages.Count - 1)
                        continue;
                }

                // go to next row
                if (isTable)
                    ImGui.TableNextColumn();

                // Set the height of the previous message. `lastPosY` is set to
                // the top of the previous message, and the current cursor is at
                // the top of the current message.
                if (i > 0)
                {
                    var prevMessage = messages[i - 1];
                    prevMessage.Height.TryGetValue(tab.Identifier, out var prevHeight);
                    if (prevHeight == null || (prevMessage.IsVisible.TryGetValue(tab.Identifier, out var prevVisible) && prevVisible))
                    {
                        var newHeight = ImGui.GetCursorPosY() - lastPosY;

                        // Remove the padding from the bottom of the previous row and the top of the current row.
                        if (isTable && !moreCompact)
                            newHeight -= oldCellPaddingY * 2;

                        if (newHeight != 0)
                            prevMessage.Height[tab.Identifier] = newHeight;
                    }
                }
                lastPosY = ImGui.GetCursorPosY();

                // message has rendered once
                // message isn't visible, so render dummy
                message.Height.TryGetValue(tab.Identifier, out var height);
                message.IsVisible.TryGetValue(tab.Identifier, out var visible);
                if (height != null && !visible)
                {
                    var beforeDummy = ImGui.GetCursorPos();

                    // skip to the message column for vis test
                    if (isTable)
                        ImGui.TableNextColumn();

                    ImGui.Dummy(new Vector2(10f, height.Value));

                    var nowVisible = ImGui.IsItemVisible();
                    if (!nowVisible)
                        continue;

                    if (isTable)
                        ImGui.TableSetColumnIndex(0);

                    ImGui.SetCursorPos(beforeDummy);
                    message.IsVisible[tab.Identifier] = nowVisible;
                }

                if (tab.DisplayTimestamp)
                {
                    var localTime = message.Date.ToLocalTime();
                    var timestamp = localTime.ToString("t", !Plugin.Config.Use24HourClock ? null : CultureInfo.CreateSpecificCulture("de-DE"));
                    if (isTable)
                    {
                        if (!Plugin.Config.HideSameTimestamps || timestamp != lastTimestamp)
                        {
                            lastTimestamp = timestamp;
                            ImGui.TextUnformatted(timestamp);

                            // We use an IsItemHovered() check here instead of
                            // just calling Tooltip() to avoid computing the
                            // tooltip string for all visible items on every
                            // frame.
                            if (ImGui.IsItemHovered())
                                ImGuiUtil.Tooltip(localTime.ToString("F"));
                        }
                        else
                        {
                            // Avoids rendering issues caused by emojis in
                            // message content.
                            ImGui.TextUnformatted("");
                        }
                    }
                    else
                    {
                        DrawChunk(new TextChunk(ChunkSource.None, null, $"[{timestamp}] ") { Foreground = 0xFFFFFFFF, });
                        ImGui.SameLine();
                    }
                }

                if (isTable)
                    ImGui.TableNextColumn();

                var lineWidth = ImGui.GetContentRegionAvail().X;
                if (message.Sender.Count > 0)
                {
                    DrawChunks(message.Sender, true, handler, lineWidth);
                    ImGui.SameLine();
                }

                // We need to draw something otherwise the item visibility check below won't work.
                if (message.Content.Count == 0)
                    DrawChunks([new TextChunk(ChunkSource.Content, null, " ")], true, handler, lineWidth);
                else
                    DrawChunks(message.Content, true, handler, lineWidth);

                message.IsVisible[tab.Identifier] = ImGui.IsItemVisible();
            }
        }
        catch (ApplicationException)
        {
            // We couldn't get a reader lock on messages within 3ms, so
            // don't draw anything (and don't log a warning either).
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Error drawing chat log");
        }
    }

    private void DrawTabBar()
    {
        using var tabBar = ImRaii.TabBar("##chat2-tabs");
        if (!tabBar.Success)
            return;

        var previousTab = Plugin.CurrentTab;
        for (var tabI = 0; tabI < Plugin.Config.Tabs.Count; tabI++)
        {
            var tab = Plugin.Config.Tabs[tabI];
            if (tab.PopOut)
                continue;

            var unread = tabI == Plugin.LastTab || tab.UnreadMode == UnreadMode.None || tab.Unread == 0 ? "" : $" ({tab.Unread})";
            var flags = ImGuiTabItemFlags.None;
            if (Plugin.WantedTab == tabI)
                flags |= ImGuiTabItemFlags.SetSelected;

            using var tabItem = ImRaii.TabItem($"{tab.Name}{unread}###log-tab-{tabI}", flags);
            DrawTabContextMenu(tab, tabI);

            if (!tabItem.Success)
                continue;

            var hasTabSwitched = Plugin.LastTab != tabI;
            Plugin.LastTab = tabI;

            if (hasTabSwitched)
                TabSwitched(tab, previousTab);

            tab.Unread = 0;
            DrawMessageLog(tab, PayloadHandler, GetRemainingHeightForMessageLog(), hasTabSwitched);
        }

        Plugin.WantedTab = null;
    }

    private void DrawTabSidebar()
    {
        var currentTab = -1;
        using var tabTable = ImRaii.Table("tabs-table", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.Resizable);
        if (!tabTable.Success)
            return;

        ImGui.TableSetupColumn("tabs", ImGuiTableColumnFlags.WidthStretch, 1);
        ImGui.TableSetupColumn("chat", ImGuiTableColumnFlags.WidthStretch, 4);

        ImGui.TableNextColumn();

        var hasTabSwitched = false;
        var childHeight = GetRemainingHeightForMessageLog();
        using (var child = ImRaii.Child("##chat2-tab-sidebar", new Vector2(-1, childHeight)))
        {
            if (child)
            {
                var previousTab = Plugin.CurrentTab;
                for (var tabI = 0; tabI < Plugin.Config.Tabs.Count; tabI++)
                {
                    var tab = Plugin.Config.Tabs[tabI];
                    if (tab.PopOut)
                        continue;

                    var unread = tabI == Plugin.LastTab || tab.UnreadMode == UnreadMode.None || tab.Unread == 0 ? "" : $" ({tab.Unread})";
                    var clicked = ImGui.Selectable($"{tab.Name}{unread}###log-tab-{tabI}", Plugin.LastTab == tabI || Plugin.WantedTab == tabI);
                    DrawTabContextMenu(tab, tabI);

                    if (!clicked && Plugin.WantedTab != tabI)
                        continue;

                    currentTab = tabI;
                    hasTabSwitched = Plugin.LastTab != tabI;
                    Plugin.LastTab = tabI;
                    if (hasTabSwitched)
                        TabSwitched(tab, previousTab);
                }
            }
        }

        ImGui.TableNextColumn();

        if (currentTab == -1 && Plugin.LastTab < Plugin.Config.Tabs.Count)
        {
            currentTab = Plugin.LastTab;
            Plugin.Config.Tabs[currentTab].Unread = 0;
        }

        if (currentTab > -1)
            DrawMessageLog(Plugin.Config.Tabs[currentTab], PayloadHandler, childHeight, hasTabSwitched);

        Plugin.WantedTab = null;
    }

    private void DrawTabContextMenu(Tab tab, int i)
    {
        using var contextMenu = ImRaii.ContextPopupItem($"tab-context-menu-{i}");
        if (!contextMenu.Success)
            return;

        var anyChanged = false;
        var tabs = Plugin.Config.Tabs;

        ImGui.SetNextItemWidth(250f * ImGuiHelpers.GlobalScale);
        if (ImGui.InputText("##tab-name", ref tab.Name, 128))
            anyChanged = true;

        if (ImGuiUtil.IconButton(FontAwesomeIcon.TrashAlt, tooltip: Language.ChatLog_Tabs_Delete))
        {
            tabs.RemoveAt(i);
            Plugin.WantedTab = 0;

            anyChanged = true;
        }

        ImGui.SameLine();

        var (leftIcon, leftTooltip) = Plugin.Config.SidebarTabView
            ? (FontAwesomeIcon.ArrowUp, Language.ChatLog_Tabs_MoveUp)
            : (FontAwesomeIcon.ArrowLeft, Language.ChatLog_Tabs_MoveLeft);
        if (ImGuiUtil.IconButton(leftIcon, tooltip: leftTooltip) && i > 0)
        {
            (tabs[i - 1], tabs[i]) = (tabs[i], tabs[i - 1]);
            ImGui.CloseCurrentPopup();
            anyChanged = true;
        }

        ImGui.SameLine();

        var (rightIcon, rightTooltip) = Plugin.Config.SidebarTabView
            ? (FontAwesomeIcon.ArrowDown, Language.ChatLog_Tabs_MoveDown)
            : (FontAwesomeIcon.ArrowRight, Language.ChatLog_Tabs_MoveRight);
        if (ImGuiUtil.IconButton(rightIcon, tooltip: rightTooltip) && i < tabs.Count - 1)
        {
            (tabs[i + 1], tabs[i]) = (tabs[i], tabs[i + 1]);
            ImGui.CloseCurrentPopup();
            anyChanged = true;
        }

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.WindowRestore, tooltip: Language.ChatLog_Tabs_PopOut))
        {
            tab.PopOut = true;
            anyChanged = true;
        }

        if (anyChanged)
            Plugin.SaveConfig();
    }

    internal readonly List<bool> PopOutDocked = [];
    internal readonly HashSet<Guid> PopOutWindows = [];
    private void AddPopOutsToDraw()
    {
        HandlerLender.ResetCounter();

        if (PopOutDocked.Count != Plugin.Config.Tabs.Count)
        {
            PopOutDocked.Clear();
            PopOutDocked.AddRange(Enumerable.Repeat(false, Plugin.Config.Tabs.Count));
        }

        for (var i = 0; i < Plugin.Config.Tabs.Count; i++)
        {
            var tab = Plugin.Config.Tabs[i];
            if (!tab.PopOut)
                continue;

            if (PopOutWindows.Contains(tab.Identifier))
                continue;

            var window = new Popout(this, tab, i);

            Plugin.WindowSystem.AddWindow(window);
            PopOutWindows.Add(tab.Identifier);
        }
    }

    private unsafe void DrawAutoComplete()
    {
        if (AutoCompleteInfo == null)
            return;

        AutoCompleteList ??= AutoTranslate.Matching(AutoCompleteInfo.ToComplete, Plugin.Config.SortAutoTranslate);
        if (AutoCompleteOpen)
        {
            ImGui.OpenPopup(AutoCompleteId);
            AutoCompleteOpen = false;
        }

        ImGui.SetNextWindowSize(new Vector2(400, 300) * ImGuiHelpers.GlobalScale);
        using var popup = ImRaii.Popup(AutoCompleteId);
        if (!popup.Success)
        {
            if (ActivatePos == -1)
                ActivatePos = AutoCompleteInfo.EndPos;

            AutoCompleteInfo = null;
            AutoCompleteList = null;
            Activate = true;
            return;
        }

        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextWithHint("##auto-complete-filter", Language.AutoTranslate_Search_Hint, ref AutoCompleteInfo.ToComplete, 256, ImGuiInputTextFlags.CallbackAlways | ImGuiInputTextFlags.CallbackHistory, AutoCompleteCallback))
        {
            AutoCompleteList = AutoTranslate.Matching(AutoCompleteInfo.ToComplete, Plugin.Config.SortAutoTranslate);
            AutoCompleteSelection = 0;
            AutoCompleteShouldScroll = true;
        }

        var selected = -1;
        if (ImGui.IsItemActive() && ImGui.GetIO().KeyCtrl)
        {
            for (var i = 0; i < 10 && i < AutoCompleteList.Count; i++)
            {
                var num = (i + 1) % 10;
                var key = ImGuiKey.Key0 + num;
                var key2 = ImGuiKey.Keypad0 + num;
                if (ImGui.IsKeyDown(key) || ImGui.IsKeyDown(key2))
                    selected = i;
            }
        }

        if (ImGui.IsItemDeactivated())
        {
            if (ImGui.IsKeyDown(ImGuiKey.Escape))
            {
                ImGui.CloseCurrentPopup();
                return;
            }

            var enter = ImGui.IsKeyDown(ImGuiKey.Enter) || ImGui.IsKeyDown(ImGuiKey.KeypadEnter);
            if (AutoCompleteList.Count > 0 && enter)
                selected = AutoCompleteSelection;
        }

        if (ImGui.IsWindowAppearing())
        {
            FixCursor = true;
            ImGui.SetKeyboardFocusHere(-1);
        }

        using var child = ImRaii.Child("##auto-complete-list", Vector2.Zero, false, ImGuiWindowFlags.HorizontalScrollbar);
        if (!child.Success)
            return;

        var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper());

        clipper.Begin(AutoCompleteList.Count);
        while (clipper.Step())
        {
            for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
            {
                var entry = AutoCompleteList[i];

                var highlight = AutoCompleteSelection == i;
                var clicked = ImGui.Selectable($"{entry.String}##{entry.Group}/{entry.Row}", highlight) || selected == i;
                if (i < 10)
                {
                    var button = (i + 1) % 10;
                    var text = string.Format(Language.AutoTranslate_Completion_Key, button);
                    var size = ImGui.CalcTextSize(text);
                    ImGui.SameLine(ImGui.GetContentRegionAvail().X - size.X);
                    ImGui.PushStyleColor(ImGuiCol.Text, *ImGui.GetStyleColorVec4(ImGuiCol.TextDisabled));
                    ImGui.TextUnformatted(text);
                    ImGui.PopStyleColor();
                }

                if (!clicked)
                    continue;

                var before = Chat[..AutoCompleteInfo.StartPos];
                var after = Chat[AutoCompleteInfo.EndPos..];
                var replacement = $"<at:{entry.Group},{entry.Row}>";
                Chat = $"{before}{replacement}{after}";
                ImGui.CloseCurrentPopup();
                Activate = true;
                ActivatePos = AutoCompleteInfo.StartPos + replacement.Length;
            }
        }

        if (!AutoCompleteShouldScroll)
            return;

        AutoCompleteShouldScroll = false;
        var selectedPos = clipper.StartPosY + clipper.ItemsHeight * (AutoCompleteSelection * 1f);
        ImGui.SetScrollFromPosY(selectedPos - ImGui.GetWindowPos().Y);
    }

    private int AutoCompleteCallback(scoped ref ImGuiInputTextCallbackData data)
    {
        if (FixCursor && AutoCompleteInfo != null)
        {
            FixCursor = false;
            data.CursorPos = AutoCompleteInfo.ToComplete.Length;
            data.SelectionStart = data.SelectionEnd = data.CursorPos;
        }

        if (AutoCompleteList == null)
            return 0;

        switch (data.EventKey)
        {
            case ImGuiKey.UpArrow:
                if (AutoCompleteSelection == 0)
                    AutoCompleteSelection = AutoCompleteList.Count - 1;
                else
                    AutoCompleteSelection--;

                AutoCompleteShouldScroll = true;
                return 1;
            case ImGuiKey.DownArrow:
                if (AutoCompleteSelection == AutoCompleteList.Count - 1)
                    AutoCompleteSelection = 0;
                else
                    AutoCompleteSelection++;

                AutoCompleteShouldScroll = true;
                return 1;
            default:
                if(ImGui.IsKeyPressed(ImGuiKey.Tab))
                {
                    if (AutoCompleteSelection == AutoCompleteList.Count - 1)
                        AutoCompleteSelection = 0;
                    else
                        AutoCompleteSelection++;

                    AutoCompleteShouldScroll = true;
                    return 1;
                }
                break;
        }

        return 0;
    }

    private unsafe int Callback(scoped ref ImGuiInputTextCallbackData data)
    {
        // We play the opening sound here only if closing sound has been played before
        if (Plugin.Config.PlaySounds && PlayedClosingSound)
        {
            PlayedClosingSound = false;
            UIGlobals.PlaySoundEffect(ChatOpenSfx);
        }

        // Set the cursor pos to the user selected
        if (Plugin.InputPreview.SelectedCursorPos != -1)
            data.CursorPos = Plugin.InputPreview.SelectedCursorPos;
        Plugin.InputPreview.SelectedCursorPos = -1;

        CursorPos = data.CursorPos;
        if (data.EventFlag == ImGuiInputTextFlags.CallbackCompletion)
        {
            if (data.CursorPos == 0)
            {
                AutoCompleteInfo = new AutoCompleteInfo(
                    string.Empty,
                    data.CursorPos,
                    data.CursorPos
                );
                AutoCompleteOpen = true;
                AutoCompleteSelection = 0;

                return 0;
            }

            int white;
            for (white = data.CursorPos - 1; white >= 0; white--)
                if (data.Buf[white] == ' ')
                    break;

            var start = data.Buf + white + 1;
            var end = data.CursorPos - white - 1;
            var utf8Message = Marshal.PtrToStringUTF8((nint)start, end);
            var correctedCursor = data.CursorPos - (end - utf8Message.Length);
            AutoCompleteInfo = new AutoCompleteInfo(
                utf8Message,
                white + 1,
                correctedCursor
            );
            AutoCompleteOpen = true;
            AutoCompleteSelection = 0;
            return 0;
        }

        if (data.EventFlag == ImGuiInputTextFlags.CallbackCharFilter)
            if (!Plugin.Functions.Chat.IsCharValid((char) data.EventChar))
                return 1;

        if (Activate)
        {
            Activate = false;
            data.CursorPos = ActivatePos > -1 ? ActivatePos : Chat.Length;
            data.SelectionStart = data.SelectionEnd = data.CursorPos;
            ActivatePos = -1;
        }

        Plugin.CommandHelpWindow.IsOpen = false;
        var text = MemoryHelper.ReadString((nint) data.Buf, data.BufTextLen);
        if (text.StartsWith('/'))
        {
            var command = text.Split(' ')[0];
            var cmd = Sheets.TextCommandSheet.FirstOrNull(cmd =>
                cmd.Command.ExtractText() == command || cmd.Alias.ExtractText() == command ||
                cmd.ShortCommand.ExtractText() == command || cmd.ShortAlias.ExtractText() == command);

            if (cmd != null)
                Plugin.CommandHelpWindow.UpdateContent(cmd.Value);
        }

        if (data.EventFlag != ImGuiInputTextFlags.CallbackHistory)
            return 0;

        var prevPos = InputBacklogIdx;
        switch (data.EventKey)
        {
            case ImGuiKey.UpArrow:
                switch (InputBacklogIdx)
                {
                    case -1:
                        var offset = 0;

                        if (!string.IsNullOrWhiteSpace(Chat))
                        {
                            AddBacklog(Chat);
                            offset = 1;
                        }

                        InputBacklogIdx = InputBacklog.Count - 1 - offset;
                        break;
                    case > 0:
                        InputBacklogIdx--;
                        break;
                }
                break;
            case ImGuiKey.DownArrow:
                if (InputBacklogIdx != -1)
                    if (++InputBacklogIdx >= InputBacklog.Count)
                        InputBacklogIdx = -1;
                break;
        }

        if (prevPos == InputBacklogIdx)
            return 0;

        var historyStr = InputBacklogIdx >= 0 ? InputBacklog[InputBacklogIdx] : string.Empty;
        data.DeleteChars(0, data.BufTextLen);
        data.InsertChars(0, historyStr);

        return 0;
    }

    internal void DrawChunks(IReadOnlyList<Chunk> chunks, bool wrap = true, PayloadHandler? handler = null, float lineWidth = 0f)
    {
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero);

        for (var i = 0; i < chunks.Count; i++)
        {
            if (chunks[i] is TextChunk text && string.IsNullOrEmpty(text.Content))
                continue;

            DrawChunk(chunks[i], wrap, handler, lineWidth);

            if (i < chunks.Count - 1)
            {
                ImGui.SameLine();
            }
            else if (chunks[i].Link is EmotePayload && Plugin.Config.ShowEmotes)
            {
                // Emote payloads seem to not automatically put newlines, which
                // is an issue when modern mode is disabled.
                ImGui.SameLine();
                // Use default ImGui behavior for newlines.
                ImGui.TextUnformatted("");
            }
        }
    }

    private void DrawChunk(Chunk chunk, bool wrap = true, PayloadHandler? handler = null, float lineWidth = 0f)
    {
        if (chunk is IconChunk icon)
        {
            DrawIcon(chunk, icon, handler);
            return;
        }

        if (chunk is not TextChunk text)
            return;

        if (chunk.Link is EmotePayload emotePayload && Plugin.Config.ShowEmotes)
        {
            var emoteSize = ImGui.CalcTextSize("W");
            emoteSize = emoteSize with { Y = emoteSize.X } * 1.5f;

            // TextWrap doesn't work for emotes, so we have to wrap them manually
            if (ImGui.GetContentRegionAvail().X < emoteSize.X)
                ImGui.NewLine();

            // We only draw a dummy if it is still loading, in the case it failed we draw the actual name
            var image = EmoteCache.GetEmote(emotePayload.Code);
            if (image is { Failed: false })
            {
                if (image.IsLoaded)
                    image.Draw(emoteSize);
                else
                    ImGui.Dummy(emoteSize);

                if (ImGui.IsItemHovered())
                    ImGuiUtil.Tooltip(emotePayload.Code);

                return;
            }
        }

        var colour = text.Foreground;
        if (colour == null && text.FallbackColour != null)
        {
            var type = text.FallbackColour.Value;
            colour = Plugin.Config.ChatColours.TryGetValue(type, out var col) ? col : type.DefaultColor();
        }

        var push = colour != null;
        var uColor = push ? ColourUtil.RgbaToAbgr(colour!.Value) : 0;
        using var pushedColor = ImRaii.PushColor(ImGuiCol.Text, uColor, push);

        var useCustomItalicFont = Plugin.Config.FontsEnabled && Plugin.FontManager.ItalicFont != null;
        if (text.Italic)
            (useCustomItalicFont ? Plugin.FontManager.ItalicFont! : Plugin.FontManager.AxisItalic).Push();

        // Check for contains here as sometimes there are multiple
        // TextChunks with the same PlayerPayload but only one has the name.
        // E.g. party chat with cross world players adds extra chunks.
        //
        // Note: This has been null before, I'm guessing due to some issues with
        // other plugins. New TextChunks will now enforce empty string in ctor,
        // but old ones may still be null.
        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        var content = text.Content ?? "";
        if (ScreenshotMode)
        {
            if (chunk.Link is PlayerPayload playerPayload)
                content = HidePlayerInString(content, playerPayload.PlayerName, playerPayload.World.RowId);
            else if (Plugin.ClientState.LocalPlayer is { } player)
                content = HidePlayerInString(content, player.Name.TextValue, player.HomeWorld.RowId);
        }

        if (wrap)
        {
            ImGuiUtil.WrapText(content, chunk, handler, DefaultText, lineWidth);
        }
        else
        {
            ImGui.TextUnformatted(content);
            ImGuiUtil.PostPayload(chunk, handler);
        }

        if (text.Italic)
            (useCustomItalicFont ? Plugin.FontManager.ItalicFont! : Plugin.FontManager.AxisItalic).Pop();
    }

    internal void DrawIcon(Chunk chunk, IconChunk icon, PayloadHandler? handler)
    {
        if (!IconUtil.GfdFileView.TryGetEntry((uint) icon.Icon, out var entry))
            return;

        var iconTexture = Plugin.TextureProvider.GetFromGame("common/font/fonticon_ps5.tex").GetWrapOrDefault();
        if (iconTexture == null)
            return;

        var texSize = new Vector2(iconTexture.Width, iconTexture.Height);

        var sizeRatio = FontManager.GetFontSize() / entry.Height;
        var size = new Vector2(entry.Width, entry.Height) * sizeRatio * ImGuiHelpers.GlobalScale;

        var uv0 = new Vector2(entry.Left, entry.Top + 170) * 2 / texSize;
        var uv1 = new Vector2(entry.Left + entry.Width, entry.Top + entry.Height + 170) * 2 / texSize;

        ImGui.Image(iconTexture.Handle, size, uv0, uv1);
        ImGuiUtil.PostPayload(chunk, handler);

    }

    internal string HidePlayerInString(string str, string playerName, uint worldId)
    {
        var expected = Plugin.Functions.Chat.AbbreviatePlayerName(playerName);
        var hash = HashPlayer(playerName, worldId);
        return str.Replace(playerName, expected).Replace(expected, hash);
    }

    private string HashPlayer(string playerName, uint worldId)
    {
        var hashCode = $"{Salt}{playerName}{worldId}".GetHashCode();
        return $"Player {hashCode:X8}";
    }
}
