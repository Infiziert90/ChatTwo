using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using ChatTwo.Code;
using ChatTwo.GameFunctions.Types;
using ChatTwo.Resources;
using ChatTwo.Util;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Style;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.UI;
using ImGuiNET;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

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

    internal Tab? CurrentTab
    {
        get
        {
            var i = LastTab;
            return i > -1 && i < Plugin.Config.Tabs.Count ? Plugin.Config.Tabs[i] : null;
        }
    }

    internal bool Activate;
    private int ActivatePos = -1;
    internal string Chat = string.Empty;
    private readonly IDalamudTextureWrap? FontIcon;
    private readonly List<string> InputBacklog = [];
    private int InputBacklogIdx = -1;
    private int LastTab { get; set; }
    private InputChannel? TempChannel;
    private TellTarget? TellTarget;
    private readonly Stopwatch LastResize = new();
    private AutoCompleteInfo? AutoCompleteInfo;
    private bool AutoCompleteOpen;
    private List<AutoTranslateEntry>? AutoCompleteList;
    private bool FixCursor;
    private int AutoCompleteSelection;
    private bool AutoCompleteShouldScroll;

    public Vector2 LastWindowPos { get; private set; } = Vector2.Zero;
    public Vector2 LastWindowSize { get; private set; } = Vector2.Zero;

    public unsafe ImGuiViewport* LastViewport;
    private bool WasDocked;

    private PayloadHandler PayloadHandler { get; }
    internal Lender<PayloadHandler> HandlerLender { get; }
    private Dictionary<string, ChatType> TextCommandChannels { get; } = new();
    private HashSet<string> AllCommands { get; } = [];

    private const uint ChatOpenSfx = 35u;
    private const uint ChatCloseSfx = 3u;
    private bool PlayedClosingSound = true;

    private readonly ExcelSheet<World> WorldSheet;
    private readonly ExcelSheet<LogFilter> LogFilterSheet;
    private readonly ExcelSheet<TextCommand> TextCommandSheet;

    internal ChatLogWindow(Plugin plugin) : base($"{Plugin.PluginName}###chat2")
    {
        Plugin = plugin;
        Salt = new Random().Next().ToString();

        Size = new Vector2(500, 250);
        SizeCondition = ImGuiCond.FirstUseEver;

        IsOpen = true;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;

        PayloadHandler = new PayloadHandler(this);
        HandlerLender = new Lender<PayloadHandler>(() => new PayloadHandler(this));

        SetUpTextCommandChannels();
        SetUpAllCommands();

        Plugin.Commands.Register("/clearlog2", "Clear the Chat 2 chat log").Execute += ClearLog;
        Plugin.Commands.Register("/chat2").Execute += ToggleChat;

        WorldSheet = Plugin.DataManager.GetExcelSheet<World>()!;
        LogFilterSheet = Plugin.DataManager.GetExcelSheet<LogFilter>()!;
        TextCommandSheet = Plugin.DataManager.GetExcelSheet<TextCommand>()!;
        FontIcon = Plugin.TextureProvider.GetTextureFromGame("common/font/fonticon_ps5.tex");

        Plugin.Functions.Chat.Activated += Activated;
        Plugin.ClientState.Login += Login;
        Plugin.ClientState.Logout += Logout;

        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "ItemDetail", PayloadHandler.MoveTooltip);
    }

    public override void PreDraw()
    {
        if (Plugin.Config is { OverrideStyle: true, ChosenStyle: not null })
            StyleModel.GetConfiguredStyles()?.FirstOrDefault(style => style.Name == Plugin.Config.ChosenStyle)?.Push();
    }

    public override void PostDraw()
    {
        if (Plugin.Config is { OverrideStyle: true, ChosenStyle: not null })
            StyleModel.GetConfiguredStyles()?.FirstOrDefault(style => style.Name == Plugin.Config.ChosenStyle)?.Pop();
    }

    public void Dispose()
    {
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, "ItemDetail", PayloadHandler.MoveTooltip);
        Plugin.ClientState.Logout -= Logout;
        Plugin.ClientState.Login -= Login;
        Plugin.Functions.Chat.Activated -= Activated;
        FontIcon?.Dispose();
        Plugin.Commands.Register("/chat2").Execute -= ToggleChat;
        Plugin.Commands.Register("/clearlog2").Execute -= ClearLog;
    }

    private void Logout()
    {
        foreach (var tab in Plugin.Config.Tabs)
            tab.Clear();
    }

    private void Login()
    {
        Plugin.MessageManager.FilterAllTabs(false);
    }

    private void Activated(ChatActivatedArgs args)
    {
        Activate = true;
        if (args.AddIfNotPresent != null && !Chat.Contains(args.AddIfNotPresent))
            Chat += args.AddIfNotPresent;

        if (args.Input != null)
            Chat += args.Input;

        var (info, reason, target) = (args.ChannelSwitchInfo, args.TellReason, args.TellTarget);

        if (info.Channel != null)
        {
            var prevTemp = TempChannel;
            if (info.Permanent)
                SetChannel(info.Channel.Value);
            else
                TempChannel = info.Channel.Value;

            if (info.Channel is InputChannel.Tell)
            {
                if (info.Rotate != RotateMode.None)
                {
                    var idx = prevTemp != InputChannel.Tell
                        ? 0 : info.Rotate == RotateMode.Reverse
                            ? -1 : 1;

                    var tellInfo = Plugin.Functions.Chat.GetTellHistoryInfo(idx);
                    if (tellInfo != null && reason != null)
                        TellTarget = new TellTarget(tellInfo.Name, (ushort) tellInfo.World, tellInfo.ContentId, reason.Value);
                }
                else
                {
                    TellTarget = null;
                    if (target != null)
                        TellTarget = target;
                }
            }
            else
            {
                TellTarget = null;
            }

            var mode = prevTemp == null ? RotateMode.None : info.Rotate;

            if (info.Channel is InputChannel.Linkshell1 && info.Rotate != RotateMode.None)
            {
                var idx = Plugin.Functions.Chat.RotateLinkshellHistory(mode);
                TempChannel = info.Channel.Value + (uint) idx;
            }
            else if (info.Channel is InputChannel.CrossLinkshell1 && info.Rotate != RotateMode.None)
            {
                var idx = Plugin.Functions.Chat.RotateCrossLinkshellHistory(mode);
                TempChannel = info.Channel.Value + (uint) idx;
            }
        }

        if (info.Text != null && Chat.Length == 0)
            Chat = info.Text;

        PlayedClosingSound = false;
        if (Plugin.Config.PlaySounds)
            UIModule.PlaySound(ChatOpenSfx);
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
                foreach (var tab in Plugin.Config.Tabs)
                    tab.Clear();
                break;
            case "help":
                Plugin.ChatGui.Print("- /clearlog2: clears the active tab's log");
                Plugin.ChatGui.Print("- /clearlog2 all: clears all tabs' logs and the global history");
                Plugin.ChatGui.Print("- /clearlog2 help: shows this help");
                break;
            default:
                if (LastTab > -1 && LastTab < Plugin.Config.Tabs.Count)
                    Plugin.Config.Tabs[LastTab].Clear();
                break;
        }
    }

    private void ToggleChat(string command, string arguments)
    {
        var parts = arguments.Split(' ');
        if (parts.Length < 2 || parts[0] != "chat")
            return;

        switch (parts[1])
        {
            case "hide":
                _hideState = HideState.User;
                break;
            case "show":
                _hideState = HideState.None;
                break;
            case "toggle":
                _hideState = _hideState switch
                {
                    HideState.User or HideState.CutsceneOverride => HideState.None,
                    HideState.Cutscene => HideState.CutsceneOverride,
                    HideState.None => HideState.User,
                    _ => _hideState,
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

        var echo = Plugin.DataManager.GetExcelSheet<TextCommand>()?.GetRow(116);
        if (echo != null)
            AddTextCommandChannel(echo, ChatType.Echo);
    }

    private void AddTextCommandChannel(TextCommand command, ChatType type)
    {
        TextCommandChannels[command.Command] = type;
        TextCommandChannels[command.ShortCommand] = type;
        TextCommandChannels[command.Alias] = type;
        TextCommandChannels[command.ShortAlias] = type;
    }

    private void SetUpAllCommands()
    {
        if (Plugin.DataManager.GetExcelSheet<TextCommand>() is not { } commands)
            return;

        var commandNames = commands.SelectMany(cmd => new[]
        {
            cmd.Command.RawString,
            cmd.ShortCommand.RawString,
            cmd.Alias.RawString,
            cmd.ShortAlias.RawString,
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

    private static float GetRemainingHeightForMessageLog()
    {
        var lineHeight = ImGui.CalcTextSize("A").Y;
        return ImGui.GetContentRegionAvail().Y - lineHeight * 2 - ImGui.GetStyle().ItemSpacing.Y - ImGui.GetStyle().FramePadding.Y * 2;
    }

    private void HandleKeybinds(bool modifiersOnly = false)
    {
        var modifierState = (ModifierFlag) 0;
        if (ImGui.GetIO().KeyAlt)
            modifierState |= ModifierFlag.Alt;

        if (ImGui.GetIO().KeyCtrl)
            modifierState |= ModifierFlag.Ctrl;

        if (ImGui.GetIO().KeyShift)
            modifierState |= ModifierFlag.Shift;

        var turnedOff = new Dictionary<VirtualKey, (uint, string)>();
        foreach (var (toIntercept, keybind) in Plugin.Functions.Chat.Keybinds)
        {
            if (toIntercept is "CMD_CHAT" or "CMD_COMMAND")
                continue;

            void Intercept(VirtualKey vk, ModifierFlag modifier)
            {
                if (!vk.TryToImGui(out var key))
                    return;

                var modifierPressed = Plugin.Config.KeybindMode switch
                {
                    KeybindMode.Strict => modifier == modifierState,
                    KeybindMode.Flexible => modifierState.HasFlag(modifier),
                    _ => false,
                };

                if (!ImGui.IsKeyPressed(key) || !modifierPressed || modifier == 0 && modifiersOnly)
                    return;

                var bits = BitOperations.PopCount((uint) modifier);
                if (!turnedOff.TryGetValue(vk, out var previousBits) || previousBits.Item1 < bits)
                    turnedOff[vk] = ((uint) bits, toIntercept);
            }

            Intercept(keybind.Key1, keybind.Modifier1);
            Intercept(keybind.Key2, keybind.Modifier2);
        }

        foreach (var (_, (_, keybind)) in turnedOff)
        {
            if (!GameFunctions.Chat.KeybindsToIntercept.TryGetValue(keybind, out var info))
                continue;

            try
            {
                TellReason? reason = info.Channel == InputChannel.Tell ? TellReason.Reply : null;
                Activated(new ChatActivatedArgs(info) { TellReason = reason, });
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Error in chat Activated event");
            }
        }
    }

    private void TabChannelSwitch(Tab tab)
    {
        // Save the previous channel to restore it later
        var current = CurrentTab;
        if (current is { Channel: null })
            current.PreviousChannel = Plugin.Functions.Chat.Channel.Channel;

        // Channel will be null if PreviousChannel is used
        var channel = tab.Channel ?? tab.PreviousChannel;

        // If channel is null it doesn't have a default, and we never selected this channel before
        if (channel != null)
            SetChannel(tab.Channel ?? tab.PreviousChannel);
    }

    private static bool GposeActive => Plugin.Condition[ConditionFlag.WatchingCutscene];
    private static bool CutsceneActive => Plugin.Condition[ConditionFlag.OccupiedInCutSceneEvent] || Plugin.Condition[ConditionFlag.WatchingCutscene78];

    private enum HideState
    {
        None,
        Cutscene,
        CutsceneOverride,
        User,
    }

    private HideState _hideState = HideState.None;

    public bool IsHidden;
    public void HideStateCheck()
    {
        // if the chat has no hide state and in a cutscene, set the hide state to cutscene
        if (Plugin.Config.HideDuringCutscenes && _hideState == HideState.None && (CutsceneActive || GposeActive))
            _hideState = HideState.Cutscene;

        // if the chat is hidden because of a cutscene and no longer in a cutscene, set the hide state to none
        if (_hideState is HideState.Cutscene or HideState.CutsceneOverride && !CutsceneActive && !GposeActive)
            _hideState = HideState.None;

        // if the chat is hidden because of a cutscene and the chat has been activated, show chat
        if (_hideState == HideState.Cutscene && Activate)
            _hideState = HideState.CutsceneOverride;

        // if the user hid the chat and is now activating chat, reset the hide state
        if (_hideState == HideState.User && Activate)
            _hideState = HideState.None;

        if (_hideState is HideState.Cutscene or HideState.User || (Plugin.Config.HideWhenNotLoggedIn && !Plugin.ClientState.IsLoggedIn))
        {
            IsHidden = true;
            return;
        }

        IsHidden = false;
    }

    public override unsafe void PreOpenCheck()
    {
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        if (!Plugin.Config.CanMove)
            Flags |= ImGuiWindowFlags.NoMove;

        if (!Plugin.Config.CanResize)
            Flags |= ImGuiWindowFlags.NoResize;

        if (!Plugin.Config.ShowTitleBar)
            Flags |= ImGuiWindowFlags.NoTitleBar;

        if (LastViewport == ImGuiHelpers.MainViewport.NativePtr && !WasDocked)
            BgAlpha = Plugin.Config.WindowAlpha / 100f;

        LastViewport = ImGui.GetWindowViewport().NativePtr;
        WasDocked = ImGui.IsWindowDocked();
    }

    public override bool DrawConditions()
    {
        return !IsHidden;
    }

    public override void Draw()
    {
        DrawChatLog();

        AddPopOutsToDraw();
        DrawAutoComplete();
    }

    private unsafe void DrawChatLog()
    {
        var resized = LastWindowSize != ImGui.GetWindowSize();
        LastWindowSize = ImGui.GetWindowSize();
        LastWindowPos = ImGui.GetWindowPos();

        if (resized)
            LastResize.Restart();

        LastViewport = ImGui.GetWindowViewport().NativePtr;
        WasDocked = ImGui.IsWindowDocked();

        var currentTab = Plugin.Config.SidebarTabView ? DrawTabSidebar() : DrawTabBar();

        Tab? activeTab = null;
        if (currentTab > -1 && currentTab < Plugin.Config.Tabs.Count)
            activeTab = Plugin.Config.Tabs[currentTab];

        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero))
        {
            if (TellTarget != null)
            {
                var playerName = TellTarget.Name;
                if (ScreenshotMode)
                    playerName = HashPlayer(TellTarget.Name, TellTarget.World);
                var world = WorldSheet.GetRow(TellTarget.World)?.Name?.RawString ?? "???";

                DrawChunks(new Chunk[]
                {
                    new TextChunk(ChunkSource.None, null, "Tell "),
                    new TextChunk(ChunkSource.None, null, playerName),
                    new IconChunk(ChunkSource.None, null, BitmapFontIcon.CrossWorld),
                    new TextChunk(ChunkSource.None, null, world),
                });
            }
            else if (TempChannel != null)
            {
                if (TempChannel.Value.IsLinkshell())
                {
                    var idx = (uint) TempChannel.Value - (uint) InputChannel.Linkshell1;
                    var lsName = Plugin.Functions.Chat.GetLinkshellName(idx);
                    ImGui.TextUnformatted($"LS #{idx + 1}: {lsName}");
                }
                else if (TempChannel.Value.IsCrossLinkshell())
                {
                    var idx = (uint) TempChannel.Value - (uint) InputChannel.CrossLinkshell1;
                    var cwlsName = Plugin.Functions.Chat.GetCrossLinkshellName(idx);
                    ImGui.TextUnformatted($"CWLS [{idx + 1}]: {cwlsName}");
                }
                else
                {
                    ImGui.TextUnformatted(TempChannel.Value.ToChatType().Name());
                }
            }
            else if (activeTab is { Channel: { } channel })
            {
                // We cannot lookup ExtraChat channel names from index over
                // IPC so we just don't show the name if it's the tabs
                // channel.
                //
                // We don't call channel.ToChatType().Name() as it has the
                // long name as used in the settings window.
                ImGui.TextUnformatted(channel.IsExtraChatLinkshell() ? $"ECLS [{channel.LinkshellIndex() + 1}]" : channel.ToChatType().Name());
            }
            else if (Plugin.ExtraChat.ChannelOverride is var (overrideName, _))
            {
                ImGui.TextUnformatted(overrideName);
            }
            else if (ScreenshotMode && Plugin.Functions.Chat.Channel is (InputChannel.Tell, _, var tellPlayerName, var tellWorldId))
            {
                if (!string.IsNullOrWhiteSpace(tellPlayerName) && tellWorldId != 0)
                {
                    var playerName = HashPlayer(tellPlayerName, tellWorldId);
                    var world = WorldSheet.GetRow(tellWorldId)?.Name?.RawString ?? "???";

                    DrawChunks(new Chunk[]
                    {
                        new TextChunk(ChunkSource.None, null, "Tell "),
                        new TextChunk(ChunkSource.None, null, playerName),
                        new IconChunk(ChunkSource.None, null, BitmapFontIcon.CrossWorld),
                        new TextChunk(ChunkSource.None, null, world),
                    });
                }
                else
                {
                    // We still need to censor the name if we couldn't read
                    // valid data.
                    ImGui.TextUnformatted("Tell");
                }
            }
            else
            {
                DrawChunks(Plugin.Functions.Chat.Channel.Name);
            }
        }

        var beforeIcon = ImGui.GetCursorPos();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Comment) && activeTab is not { Channel: not null })
            ImGui.OpenPopup(ChatChannelPicker);

        if (activeTab is { Channel: not null } && ImGui.IsItemHovered())
            ImGui.SetTooltip(Language.ChatLog_SwitcherDisabled);

        using (var popup = ImRaii.Popup(ChatChannelPicker))
        {
            if (popup)
            {
                foreach (var channel in Enum.GetValues<InputChannel>())
                {
                    var name = LogFilterSheet.FirstOrDefault(row => row.LogKind == (byte) channel.ToChatType())?.Name?.RawString ?? channel.ToChatType().Name();
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

                    if (ImGui.Selectable(name))
                        SetChannel(channel);
                }
            }
        }

        ImGui.SameLine();
        var afterIcon = ImGui.GetCursorPos();

        var buttonWidth = afterIcon.X - beforeIcon.X;
        var showNovice = Plugin.Config.ShowNoviceNetwork && Plugin.Functions.IsMentor();
        var inputWidth = ImGui.GetContentRegionAvail().X - buttonWidth * (showNovice ? 2 : 1);

        var inputType = TempChannel?.ToChatType() ?? activeTab?.Channel?.ToChatType() ?? Plugin.Functions.Chat.Channel.Channel.ToChatType();
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
        var inputColour = Plugin.Config.ChatColours.TryGetValue(inputType, out var inputCol) ? inputCol : inputType.DefaultColour();

        if (!isCommand && Plugin.ExtraChat.ChannelOverride is var (_, overrideColour))
            inputColour = overrideColour;

        if (isCommand && Plugin.ExtraChat.ChannelCommandColours.TryGetValue(Chat.Split(' ')[0], out var ecColour))
            inputColour = ecColour;

        var push = inputColour != null;
        using (ImRaii.PushColor(ImGuiCol.Text, push ? ColourUtil.RgbaToAbgr(inputColour!.Value) : 0, push))
        {
            if (Activate)
                ImGui.SetKeyboardFocusHere();

            var chatCopy = Chat;
            ImGui.SetNextItemWidth(inputWidth);
            ImGui.InputText("##chat2-input", ref Chat, 500, InputFlags, Callback);

            if (ImGui.IsItemDeactivated())
            {
                if (ImGui.IsKeyDown(ImGuiKey.Escape))
                {
                    Chat = chatCopy;

                    if (Plugin.Functions.Chat.UsesTellTempChannel)
                    {
                        Plugin.Functions.Chat.UsesTellTempChannel = false;
                        SetChannel(Plugin.Functions.Chat.PreviousChannel);
                    }
                }

                if (ImGui.IsKeyDown(ImGuiKey.Enter) || ImGui.IsKeyDown(ImGuiKey.KeypadEnter))
                {
                    Plugin.CommandHelpWindow.IsOpen = false;
                    SendChatBox(activeTab);

                    if (Plugin.Functions.Chat.UsesTellTempChannel)
                    {
                        Plugin.Functions.Chat.UsesTellTempChannel = false;
                        SetChannel(Plugin.Functions.Chat.PreviousChannel);
                    }
                }
            }

            if (ImGui.IsItemActive())
                HandleKeybinds(true);

            // Only trigger unfocused if we are currently not calling the auto complete
            if (!Activate && !ImGui.IsItemActive() && AutoCompleteInfo == null)
            {
                if (Plugin.Config.PlaySounds && !PlayedClosingSound)
                {
                    PlayedClosingSound = true;
                    UIModule.PlaySound(ChatCloseSfx);
                }

                if (TempChannel is InputChannel.Tell)
                    TellTarget = null;

                TempChannel = null;
                if (Plugin.Functions.Chat.UsesTellTempChannel)
                {
                    Plugin.Functions.Chat.UsesTellTempChannel = false;
                    SetChannel(Plugin.Functions.Chat.PreviousChannel);
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

        if (ImGuiUtil.IconButton(FontAwesomeIcon.Cog))
            Plugin.SettingsWindow.Toggle();

        if (!showNovice)
            return;

        ImGui.SameLine();

        if (ImGuiUtil.IconButton(FontAwesomeIcon.Leaf))
            Plugin.Functions.ClickNoviceNetworkButton();
    }

    internal void SetChannel(InputChannel? channel)
    {
        channel ??= InputChannel.Say;
        TellTarget = null;

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
            Plugin.Common.Functions.Chat.SendMessageUnsafe(bytes);
            return;
        }

        Plugin.Functions.Chat.SetChannel(channel.Value);
    }

    private void SendChatBox(Tab? activeTab)
    {
        if (!string.IsNullOrWhiteSpace(Chat))
        {
            var trimmed = Chat.Trim();
            AddBacklog(trimmed);
            InputBacklogIdx = -1;

            if (!trimmed.StartsWith('/'))
            {
                if (TellTarget != null)
                {
                    var target = TellTarget;
                    var reason = target.Reason;
                    var world = WorldSheet.GetRow(target.World);
                    if (world is { IsPublic: true })
                    {
                        if (reason == TellReason.Reply && Plugin.Common.Functions.FriendList.List.Any(friend => friend.ContentId == target.ContentId))
                            reason = TellReason.Friend;

                        var tellBytes = Encoding.UTF8.GetBytes(trimmed);
                        AutoTranslate.ReplaceWithPayload(Plugin.DataManager, ref tellBytes);

                        Plugin.Functions.Chat.SendTell(reason, target.ContentId, target.Name, (ushort) world.RowId, tellBytes);
                    }

                    if (TempChannel is InputChannel.Tell)
                        TellTarget = null;

                    Chat = string.Empty;
                    return;
                }


                if (TempChannel != null)
                    trimmed = $"{TempChannel.Value.Prefix()} {trimmed}";
                else if (activeTab is { Channel: { } channel })
                    trimmed = $"{channel.Prefix()} {trimmed}";
            }

            var bytes = Encoding.UTF8.GetBytes(trimmed);
            AutoTranslate.ReplaceWithPayload(Plugin.DataManager, ref bytes);

            Plugin.Common.Functions.Chat.SendMessageUnsafe(bytes);
        }

        Chat = string.Empty;
    }

    internal void UserHide()
    {
        _hideState = HideState.User;
    }

    internal void DrawMessageLog(Tab tab, PayloadHandler handler, float childHeight, bool switchedTab)
    {
        using var child = ImRaii.Child("##chat2-messages", new Vector2(-1, childHeight));
        if (!child.Success)
            return;

        var useTable = tab.DisplayTimestamp && Plugin.Config.PrettierTimestamps;
        if (useTable)
            DrawLogTableStyle(tab, handler, switchedTab);
        else
            DrawLogNormalStyle(tab, handler, switchedTab);
    }

    private void DrawLogNormalStyle(Tab tab, PayloadHandler handler, bool switchedTab)
    {
        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero))
        {
            DrawMessages(tab, handler, false);
        }

        if (switchedTab || ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
            ImGui.SetScrollHereY(1f);

        handler.Draw();
    }

    private void DrawLogTableStyle(Tab tab, PayloadHandler handler, bool switchedTab)
    {
        var oldItemSpacing = ImGui.GetStyle().ItemSpacing;
        var oldCellPadding = ImGui.GetStyle().CellPadding;

        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero))
        using (ImRaii.PushStyle(ImGuiStyleVar.CellPadding, oldCellPadding with { Y = 0 }, Plugin.Config.MoreCompactPretty))
        {
            using var table = ImRaii.Table("timestamp-table", 2, ImGuiTableFlags.PreciseWidths);
            if (!table.Success)
                return;

            ImGui.TableSetupColumn("timestamps", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("messages", ImGuiTableColumnFlags.WidthStretch);

            DrawMessages(tab, handler, true, Plugin.Config.MoreCompactPretty, oldCellPadding.Y);

            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, oldItemSpacing))
            using (ImRaii.PushStyle(ImGuiStyleVar.CellPadding, oldCellPadding))
            {
                if (switchedTab || ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
                    ImGui.SetScrollHereY(1f);

                handler.Draw();
            }
        }
    }

    private void DrawMessages(Tab tab, PayloadHandler handler, bool isTable, bool moreCompact = false, float oldCellPaddingY = 0)
    {
        try
        {
            tab.MessagesMutex.Wait();

            var reset = false;
            if (LastResize is { IsRunning: true, Elapsed.TotalSeconds: > 0.25 })
            {
                LastResize.Stop();
                LastResize.Reset();
                reset = true;
            }

            var lastPos = ImGui.GetCursorPosY();
            var lastTimestamp = string.Empty;
            int? lastMessageHash = null;
            var sameCount = 0;

            var maxLines = Plugin.Config.MaxLinesToRender;
            var startLine = tab.Messages.Count > maxLines ? tab.Messages.Count - maxLines : 0;
            for (var i = startLine; i < tab.Messages.Count; i++)
            {
                var message = tab.Messages[i];
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
                        if (i != tab.Messages.Count - 1)
                            continue;
                    }

                    if (sameCount > 0)
                    {
                        ImGui.SameLine();
                        DrawChunks(
                            new[] { new TextChunk(ChunkSource.None, null, $" ({sameCount + 1}x)") { FallbackColour = ChatType.System, Italic = true, } },
                            true,
                            handler,
                            ImGui.GetContentRegionAvail().X
                        );
                        sameCount = 0;
                    }

                    lastMessageHash = messageHash;
                    if (same && i == tab.Messages.Count - 1)
                        continue;
                }

                // go to next row
                if (isTable)
                    ImGui.TableNextColumn();

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
                    message.IsVisible[tab.Identifier] = ImGui.IsItemVisible();

                    if (message.IsVisible[tab.Identifier])
                    {
                        if (isTable)
                            ImGui.TableSetColumnIndex(0);

                        ImGui.SetCursorPos(beforeDummy);
                    }
                    else
                    {
                        lastPos = ImGui.GetCursorPosY();
                        continue;
                    }
                }

                if (tab.DisplayTimestamp)
                {
                    var timestamp = message.Date.ToLocalTime().ToString("t");
                    if (isTable)
                    {
                        if (!Plugin.Config.HideSameTimestamps || timestamp != lastTimestamp)
                        {
                            ImGui.TextUnformatted(timestamp);
                            lastTimestamp = timestamp;
                        }
                    }
                    else
                    {
                        DrawChunk(new TextChunk(ChunkSource.None, null, $"[{timestamp}]") { Foreground = 0xFFFFFFFF, });
                        ImGui.SameLine();
                    }
                }

                if (isTable)
                    ImGui.TableNextColumn();

                var lineWidth = ImGui.GetContentRegionAvail().X;
                var beforeDraw = ImGui.GetCursorScreenPos();
                if (message.Sender.Count > 0)
                {
                    DrawChunks(message.Sender, true, handler, lineWidth);
                    ImGui.SameLine();
                }

                if (message.Content.Count == 0)
                    DrawChunks(new[] { new TextChunk(ChunkSource.Content, null, " ") }, true, handler, lineWidth);
                else
                    DrawChunks(message.Content, true, handler, lineWidth);

                var afterDraw = ImGui.GetCursorScreenPos();
                message.Height[tab.Identifier] = ImGui.GetCursorPosY() - lastPos;
                if (isTable && !moreCompact)
                {
                    message.Height[tab.Identifier] -= oldCellPaddingY * 2;
                    beforeDraw.Y += oldCellPaddingY;
                    afterDraw.Y -= oldCellPaddingY;
                }

                lastPos = ImGui.GetCursorPosY();
                message.IsVisible[tab.Identifier] = ImGui.IsRectVisible(beforeDraw, afterDraw);
            }
        }
        finally
        {
            tab.MessagesMutex.Release();
        }
    }

    private int DrawTabBar()
    {
        var currentTab = -1;

        using var tabBar = ImRaii.TabBar("##chat2-tabs");
        if (!tabBar.Success)
            return currentTab;

        for (var tabI = 0; tabI < Plugin.Config.Tabs.Count; tabI++)
        {
            var tab = Plugin.Config.Tabs[tabI];
            if (tab.PopOut)
                continue;

            var unread = tabI == LastTab || tab.UnreadMode == UnreadMode.None || tab.Unread == 0 ? "" : $" ({tab.Unread})";
            using var tabItem = ImRaii.TabItem($"{tab.Name}{unread}###log-tab-{tabI}");
            DrawTabContextMenu(tab, tabI);

            if (!tabItem.Success)
                continue;

            currentTab = tabI;
            var switchedTab = LastTab != tabI;
            if (switchedTab)
                TabChannelSwitch(tab);
            LastTab = tabI;
            tab.Unread = 0;

            DrawMessageLog(tab, PayloadHandler, GetRemainingHeightForMessageLog(), switchedTab);
        }

        return currentTab;
    }

    private int DrawTabSidebar()
    {
        var currentTab = -1;

        using var tabTable = ImRaii.Table("tabs-table", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.Resizable);
        if (!tabTable.Success)
            return currentTab;

        ImGui.TableSetupColumn("tabs", ImGuiTableColumnFlags.None, 1);
        ImGui.TableSetupColumn("chat", ImGuiTableColumnFlags.None, 4);

        ImGui.TableNextColumn();

        var switchedTab = false;
        var childHeight = GetRemainingHeightForMessageLog();
        using (var child = ImRaii.Child("##chat2-tab-sidebar", new Vector2(-1, childHeight)))
        {
            if (child)
            {
                for (var tabI = 0; tabI < Plugin.Config.Tabs.Count; tabI++)
                {
                    var tab = Plugin.Config.Tabs[tabI];
                    if (tab.PopOut)
                        continue;

                    var unread = tabI == LastTab || tab.UnreadMode == UnreadMode.None || tab.Unread == 0 ? "" : $" ({tab.Unread})";
                    var clicked = ImGui.Selectable($"{tab.Name}{unread}###log-tab-{tabI}", LastTab == tabI);
                    DrawTabContextMenu(tab, tabI);

                    if (!clicked)
                        continue;

                    currentTab = tabI;
                    switchedTab = LastTab != tabI;
                    if (switchedTab)
                        TabChannelSwitch(tab);
                    LastTab = tabI;
                }
            }
        }

        ImGui.TableNextColumn();

        if (currentTab == -1 && LastTab < Plugin.Config.Tabs.Count)
        {
            currentTab = LastTab;
            Plugin.Config.Tabs[currentTab].Unread = 0;
        }

        if (currentTab > -1)
            DrawMessageLog(Plugin.Config.Tabs[currentTab], PayloadHandler, childHeight, switchedTab);

        return currentTab;
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

        AutoCompleteList ??= AutoTranslate.Matching(Plugin.DataManager, AutoCompleteInfo.ToComplete, Plugin.Config.SortAutoTranslate);
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
            AutoCompleteList = AutoTranslate.Matching(Plugin.DataManager, AutoCompleteInfo.ToComplete, Plugin.Config.SortAutoTranslate);
            AutoCompleteSelection = 0;
            AutoCompleteShouldScroll = true;
        }

        var selected = -1;
        if (ImGui.IsItemActive() && ImGui.GetIO().KeyCtrl)
        {
            for (var i = 0; i < 10 && i < AutoCompleteList.Count; i++)
            {
                var num = (i + 1) % 10;
                var key = ImGuiKey._0 + num;
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

        var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());

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

    private unsafe int AutoCompleteCallback(ImGuiInputTextCallbackData* data)
    {
        if (FixCursor && AutoCompleteInfo != null)
        {
            FixCursor = false;
            data->CursorPos = AutoCompleteInfo.ToComplete.Length;
            data->SelectionStart = data->SelectionEnd = data->CursorPos;
        }

        if (AutoCompleteList == null)
            return 0;

        switch (data->EventKey)
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
        }

        return 0;
    }

    private unsafe int Callback(ImGuiInputTextCallbackData* data)
    {
        // We play the opening sound here only if closing sound has been played before
        if (Plugin.Config.PlaySounds && PlayedClosingSound)
        {
            PlayedClosingSound = false;
            UIModule.PlaySound(ChatOpenSfx);
        }

        var ptr = new ImGuiInputTextCallbackDataPtr(data);
        if (data->EventFlag == ImGuiInputTextFlags.CallbackCompletion)
        {
            if (ptr.CursorPos == 0)
            {
                AutoCompleteInfo = new AutoCompleteInfo(
                    string.Empty,
                    ptr.CursorPos,
                    ptr.CursorPos
                );
                AutoCompleteOpen = true;
                AutoCompleteSelection = 0;

                return 0;
            }

            int white;
            for (white = ptr.CursorPos - 1; white >= 0; white--)
                if (data->Buf[white] == ' ')
                    break;

            var start = ptr.Buf + white + 1;
            var end = ptr.CursorPos - white - 1;
            var utf8Message = Marshal.PtrToStringUTF8(start, end);
            var correctedCursor = ptr.CursorPos - (end - utf8Message.Length);
            AutoCompleteInfo = new AutoCompleteInfo(
                utf8Message,
                white + 1,
                correctedCursor
            );
            AutoCompleteOpen = true;
            AutoCompleteSelection = 0;
            return 0;
        }

        if (data->EventFlag == ImGuiInputTextFlags.CallbackCharFilter)
            if (!Plugin.Functions.Chat.IsCharValid((char) ptr.EventChar))
                return 1;

        if (Activate)
        {
            Activate = false;
            data->CursorPos = ActivatePos > -1 ? ActivatePos : Chat.Length;
            data->SelectionStart = data->SelectionEnd = data->CursorPos;
            ActivatePos = -1;
        }

        Plugin.CommandHelpWindow.IsOpen = false;
        var text = MemoryHelper.ReadString((IntPtr) data->Buf, data->BufTextLen);
        if (text.StartsWith('/'))
        {
            var command = text.Split(' ')[0];
            var cmd = TextCommandSheet.FirstOrDefault(cmd =>
                cmd.Command.RawString == command || cmd.Alias.RawString == command ||
                cmd.ShortCommand.RawString == command || cmd.ShortAlias.RawString == command);

            if (cmd != null)
                Plugin.CommandHelpWindow.UpdateContent(cmd);
        }

        if (data->EventFlag != ImGuiInputTextFlags.CallbackHistory)
            return 0;

        var prevPos = InputBacklogIdx;
        switch (data->EventKey)
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
        ptr.DeleteChars(0, ptr.BufTextLen);
        ptr.InsertChars(0, historyStr);

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
                ImGui.SameLine();
        }
    }

    private void DrawChunk(Chunk chunk, bool wrap = true, PayloadHandler? handler = null, float lineWidth = 0f)
    {
        if (chunk is IconChunk icon && FontIcon != null)
        {
            var bounds = IconUtil.GfdFileView.TryGetEntry((uint) icon.Icon, out var entry);
            if (!bounds)
                return;

            var texSize = new Vector2(FontIcon.Width, FontIcon.Height);

            var sizeRatio = Plugin.Config.FontSize / entry.Height;
            var size = new Vector2(entry.Width, entry.Height) * sizeRatio * ImGuiHelpers.GlobalScale;

            var uv0 = new Vector2(entry.Left, entry.Top + 170) * 2 / texSize;
            var uv1 = new Vector2(entry.Left + entry.Width, entry.Top + entry.Height + 170) * 2 / texSize;

            ImGui.Image(FontIcon.ImGuiHandle, size, uv0, uv1);
            ImGuiUtil.PostPayload(chunk, handler);

            return;
        }

        if (chunk is not TextChunk text)
            return;

        var colour = text.Foreground;
        if (colour == null && text.FallbackColour != null)
        {
            var type = text.FallbackColour.Value;
            colour = Plugin.Config.ChatColours.TryGetValue(type, out var col) ? col : type.DefaultColour();
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
        var content = text.Content;
        if (ScreenshotMode)
        {
            if (chunk.Link is PlayerPayload playerPayload && content.Contains(playerPayload.PlayerName))
                content = content.Replace(playerPayload.PlayerName, HashPlayer(playerPayload.PlayerName, playerPayload.World.RowId));
            else if (Plugin.ClientState.LocalPlayer is { } player && content.Contains(player.Name.TextValue))
                content = content.Replace(player.Name.TextValue, HashPlayer(player.Name.TextValue, player.HomeWorld.Id));
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

    private string HashPlayer(string playerName, uint worldId)
    {
        var hashCode = $"{Salt}{playerName}{worldId}".GetHashCode();
        return $"Player {hashCode:X8}";
    }
}
