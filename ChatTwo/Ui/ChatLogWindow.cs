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
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Memory;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace ChatTwo.Ui;

public sealed class ChatLogWindow : Window, IUiComponent {
    private const string ChatChannelPicker = "chat-channel-picker";
    private const string AutoCompleteId = "##chat2-autocomplete";

    internal Plugin Plugin { get; }

    internal bool ScreenshotMode;
    internal string Salt { get; }

    internal Vector4 DefaultText { get; set; }

    internal Tab? CurrentTab {
        get {
            var i = LastTab;
            if (i > -1 && i < Plugin.Config.Tabs.Count) {
                return Plugin.Config.Tabs[i];
            }

            return null;
        }
    }

    internal bool Activate;
    private int _activatePos = -1;
    internal string Chat = string.Empty;
    private readonly IDalamudTextureWrap? _fontIcon;
    private readonly List<string> _inputBacklog = new();
    private int _inputBacklogIdx = -1;
    internal int LastTab { get; private set; }
    private InputChannel? _tempChannel;
    private TellTarget? _tellTarget;
    private readonly Stopwatch _lastResize = new();
    private AutoCompleteInfo? _autoCompleteInfo;
    private bool _autoCompleteOpen;
    private List<AutoTranslateEntry>? _autoCompleteList;
    private bool _fixCursor;
    private int _autoCompleteSelection;
    private bool _autoCompleteShouldScroll;

    public Vector2 LastWindowPos { get; private set; } = Vector2.Zero;
    public Vector2 LastWindowSize { get; private set; } = Vector2.Zero;

    public unsafe ImGuiViewport* LastViewport;
    private bool _wasDocked;

    internal PayloadHandler PayloadHandler { get; }
    internal Lender<PayloadHandler> HandlerLender { get; }
    private Dictionary<string, ChatType> TextCommandChannels { get; } = new();
    private HashSet<string> AllCommands { get; } = new();

    internal ChatLogWindow(Plugin plugin) : base($"{Plugin.PluginName}###chat2") {
        Plugin = plugin;
        Salt = new Random().Next().ToString();

        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(500, 250),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        PayloadHandler = new PayloadHandler(this);
        HandlerLender = new Lender<PayloadHandler>(() => new PayloadHandler(this));

        SetUpTextCommandChannels();
        SetUpAllCommands();

        Plugin.Commands.Register("/clearlog2", "Clear the Chat 2 chat log").Execute += ClearLog;
        Plugin.Commands.Register("/chat2").Execute += ToggleChat;

        _fontIcon = Plugin.TextureProvider.GetTextureFromGame("common/font/fonticon_ps5.tex");

        Plugin.Functions.Chat.Activated += Activated;
        Plugin.ClientState.Login += Login;
        Plugin.ClientState.Logout += Logout;

        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "ItemDetail", PayloadHandler.MoveTooltip);
    }

    public void Dispose() {
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, "ItemDetail", PayloadHandler.MoveTooltip);
        Plugin.ClientState.Logout -= Logout;
        Plugin.ClientState.Login -= Login;
        Plugin.Functions.Chat.Activated -= Activated;
        _fontIcon?.Dispose();
        Plugin.Commands.Register("/chat2").Execute -= ToggleChat;
        Plugin.Commands.Register("/clearlog2").Execute -= ClearLog;
    }

    private void Logout() {
        foreach (var tab in Plugin.Config.Tabs) {
            tab.Clear();
        }
    }

    private void Login() {
        Plugin.Store.FilterAllTabs(false);
    }

    private void Activated(ChatActivatedArgs args) {
        Activate = true;
        if (args.AddIfNotPresent != null && !Chat.Contains(args.AddIfNotPresent)) {
            Chat += args.AddIfNotPresent;
        }

        if (args.Input != null) {
            Chat += args.Input;
        }

        var (info, reason, target) = (args.ChannelSwitchInfo, args.TellReason, args.TellTarget);

        if (info.Channel != null) {
            var prevTemp = _tempChannel;

            if (info.Permanent) {
                Plugin.Functions.Chat.SetChannel(info.Channel.Value);
            } else {
                _tempChannel = info.Channel.Value;
            }

            if (info.Channel is InputChannel.Tell) {
                if (info.Rotate != RotateMode.None) {
                    var idx = prevTemp != InputChannel.Tell
                        ? 0 : info.Rotate == RotateMode.Reverse
                            ? -1 : 1;

                    var tellInfo = Plugin.Functions.Chat.GetTellHistoryInfo(idx);
                    if (tellInfo != null && reason != null) {
                        _tellTarget = new TellTarget(tellInfo.Name, (ushort) tellInfo.World, tellInfo.ContentId, reason.Value);
                    }
                } else {
                    _tellTarget = null;

                    if (target != null) {
                        _tellTarget = target;
                    }
                }
            } else {
                _tellTarget = null;
            }

            var mode = prevTemp == null ? RotateMode.None : info.Rotate;

            if (info.Channel is InputChannel.Linkshell1 && info.Rotate != RotateMode.None) {
                var idx = Plugin.Functions.Chat.RotateLinkshellHistory(mode);
                _tempChannel = info.Channel.Value + (uint) idx;
            } else if (info.Channel is InputChannel.CrossLinkshell1 && info.Rotate != RotateMode.None) {
                var idx = Plugin.Functions.Chat.RotateCrossLinkshellHistory(mode);
                _tempChannel = info.Channel.Value + (uint) idx;
            }
        }

        if (info.Text != null && Chat.Length == 0) {
            Chat = info.Text;
        }
    }

    private bool IsValidCommand(string command) {
        return Plugin.CommandManager.Commands.ContainsKey(command)
               || AllCommands.Contains(command);
    }

    private void ClearLog(string command, string arguments) {
        switch (arguments) {
            case "all":
                foreach (var tab in Plugin.Config.Tabs) {
                    tab.Clear();
                }

                break;
            case "help":
                Plugin.ChatGui.Print("- /clearlog2: clears the active tab's log");
                Plugin.ChatGui.Print("- /clearlog2 all: clears all tabs' logs and the global history");
                Plugin.ChatGui.Print("- /clearlog2 help: shows this help");

                break;
            default:
                if (LastTab > -1 && LastTab < Plugin.Config.Tabs.Count) {
                    Plugin.Config.Tabs[LastTab].Clear();
                }

                break;
        }
    }

    private void ToggleChat(string command, string arguments) {
        var parts = arguments.Split(' ');
        if (parts.Length < 2 || parts[0] != "chat") {
            return;
        }

        switch (parts[1]) {
            case "hide":
                _hideState = HideState.User;
                break;
            case "show":
                _hideState = HideState.None;
                break;
            case "toggle":
                _hideState = _hideState switch {
                    HideState.User or HideState.CutsceneOverride => HideState.None,
                    HideState.Cutscene => HideState.CutsceneOverride,
                    HideState.None => HideState.User,
                    _ => _hideState,
                };

                break;
        }
    }

    private void SetUpTextCommandChannels() {
        TextCommandChannels.Clear();

        foreach (var input in Enum.GetValues<InputChannel>()) {
            var commands = input.TextCommands(Plugin.DataManager);
            if (commands == null) {
                continue;
            }

            var type = input.ToChatType();
            foreach (var command in commands) {
                AddTextCommandChannel(command, type);
            }
        }

        var echo = Plugin.DataManager.GetExcelSheet<TextCommand>()?.GetRow(116);
        if (echo != null) {
            AddTextCommandChannel(echo, ChatType.Echo);
        }
    }

    private void AddTextCommandChannel(TextCommand command, ChatType type) {
        TextCommandChannels[command.Command] = type;
        TextCommandChannels[command.ShortCommand] = type;
        TextCommandChannels[command.Alias] = type;
        TextCommandChannels[command.ShortAlias] = type;
    }

    private void SetUpAllCommands() {
        if (Plugin.DataManager.GetExcelSheet<TextCommand>() is not { } commands) {
            return;
        }

        var commandNames = commands.SelectMany(cmd => new[] {
            cmd.Command.RawString,
            cmd.ShortCommand.RawString,
            cmd.Alias.RawString,
            cmd.ShortAlias.RawString,
        });

        foreach (var command in commandNames) {
            AllCommands.Add(command);
        }
    }

    private void AddBacklog(string message) {
        for (var i = 0; i < _inputBacklog.Count; i++) {
            if (_inputBacklog[i] != message) {
                continue;
            }

            _inputBacklog.RemoveAt(i);
            break;
        }

        _inputBacklog.Add(message);
    }

    private static float GetRemainingHeightForMessageLog() {
        var lineHeight = ImGui.CalcTextSize("A").Y;
        return ImGui.GetContentRegionAvail().Y
               - lineHeight * 2
               - ImGui.GetStyle().ItemSpacing.Y
               - ImGui.GetStyle().FramePadding.Y * 2;
    }

    private void HandleKeybinds(bool modifiersOnly = false) {
        var modifierState = (ModifierFlag) 0;
        if (ImGui.GetIO().KeyAlt) {
            modifierState |= ModifierFlag.Alt;
        }

        if (ImGui.GetIO().KeyCtrl) {
            modifierState |= ModifierFlag.Ctrl;
        }

        if (ImGui.GetIO().KeyShift) {
            modifierState |= ModifierFlag.Shift;
        }

        var turnedOff = new Dictionary<VirtualKey, (uint, string)>();
        foreach (var (toIntercept, keybind) in Plugin.Functions.Chat.Keybinds) {
            if (toIntercept is "CMD_CHAT" or "CMD_COMMAND") {
                continue;
            }

            void Intercept(VirtualKey vk, ModifierFlag modifier) {
                if (!vk.TryToImGui(out var key)) {
                    return;
                }

                var modifierPressed = Plugin.Config.KeybindMode switch {
                    KeybindMode.Strict => modifier == modifierState,
                    KeybindMode.Flexible => modifierState.HasFlag(modifier),
                    _ => false,
                };
                if (!ImGui.IsKeyPressed(key) || !modifierPressed || modifier == 0 && modifiersOnly) {
                    return;
                }

                var bits = BitOperations.PopCount((uint) modifier);
                if (!turnedOff.TryGetValue(vk, out var previousBits) || previousBits.Item1 < bits) {
                    turnedOff[vk] = ((uint) bits, toIntercept);
                }
            }

            Intercept(keybind.Key1, keybind.Modifier1);
            Intercept(keybind.Key2, keybind.Modifier2);
        }

        foreach (var (_, (_, keybind)) in turnedOff) {
            if (!GameFunctions.Chat.KeybindsToIntercept.TryGetValue(keybind, out var info)) {
                continue;
            }

            try {
                TellReason? reason = info.Channel == InputChannel.Tell ? TellReason.Reply : null;
                Activated(new ChatActivatedArgs(info) {
                    TellReason = reason,
                });
            } catch (Exception ex) {
                Plugin.Log.Error(ex, "Error in chat Activated event");
            }
        }
    }

    private bool CutsceneActive {
        get {
            return Plugin.Condition[ConditionFlag.OccupiedInCutSceneEvent]
                   || Plugin.Condition[ConditionFlag.WatchingCutscene78];
        }
    }

    private bool GposeActive {
        get {
            return Plugin.Condition[ConditionFlag.WatchingCutscene];
        }
    }

    private enum HideState {
        None,
        Cutscene,
        CutsceneOverride,
        User,
    }

    private HideState _hideState = HideState.None;

    public override unsafe void PreOpenCheck()
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

        if (_hideState is HideState.Cutscene or HideState.User)
        {
            IsOpen = false;
            return;
        }

        if (Plugin.Config.HideWhenNotLoggedIn && !Plugin.ClientState.IsLoggedIn) {
            IsOpen = false;
            return;
        }

        IsOpen = true;

        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        if (!Plugin.Config.CanMove)
            Flags |= ImGuiWindowFlags.NoMove;

        if (!Plugin.Config.CanResize)
            Flags |= ImGuiWindowFlags.NoResize;

        if (!Plugin.Config.ShowTitleBar)
            Flags |= ImGuiWindowFlags.NoTitleBar;

        if (LastViewport == ImGuiHelpers.MainViewport.NativePtr && !_wasDocked)
            BgAlpha = Plugin.Config.WindowAlpha / 100f;

        LastViewport = ImGui.GetWindowViewport().NativePtr;
        _wasDocked = ImGui.IsWindowDocked();
    }

    public override void Draw()
    {
        DrawChatLog();

        DrawPopOuts();
        DrawAutoComplete();
    }

    private unsafe void DrawChatLog()
    {
        var resized = LastWindowSize != ImGui.GetWindowSize();
        LastWindowSize = ImGui.GetWindowSize();
        LastWindowPos = ImGui.GetWindowPos();

        if (resized)
            _lastResize.Restart();

        LastViewport = ImGui.GetWindowViewport().NativePtr;
        _wasDocked = ImGui.IsWindowDocked();

        var currentTab = Plugin.Config.SidebarTabView ? DrawTabSidebar() : DrawTabBar();

        Tab? activeTab = null;
        if (currentTab > -1 && currentTab < Plugin.Config.Tabs.Count) {
            activeTab = Plugin.Config.Tabs[currentTab];
        }

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        try {
            if (_tellTarget != null) {
                var world = Plugin.DataManager.GetExcelSheet<World>()
                    ?.GetRow(_tellTarget.World)
                    ?.Name
                    ?.RawString ?? "???";

                DrawChunks(new Chunk[] {
                    new TextChunk(ChunkSource.None, null, "Tell "),
                    new TextChunk(ChunkSource.None, null, _tellTarget.Name),
                    new IconChunk(ChunkSource.None, null, BitmapFontIcon.CrossWorld),
                    new TextChunk(ChunkSource.None, null, world),
                });
            } else if (_tempChannel != null) {
                if (_tempChannel.Value.IsLinkshell()) {
                    var idx = (uint) _tempChannel.Value - (uint) InputChannel.Linkshell1;
                    var lsName = Plugin.Functions.Chat.GetLinkshellName(idx);
                    ImGui.TextUnformatted($"LS #{idx + 1}: {lsName}");
                } else if (_tempChannel.Value.IsCrossLinkshell()) {
                    var idx = (uint) _tempChannel.Value - (uint) InputChannel.CrossLinkshell1;
                    var cwlsName = Plugin.Functions.Chat.GetCrossLinkshellName(idx);
                    ImGui.TextUnformatted($"CWLS [{idx + 1}]: {cwlsName}");
                } else {
                    ImGui.TextUnformatted(_tempChannel.Value.ToChatType().Name());
                }
            } else if (activeTab is { Channel: { } channel }) {
                ImGui.TextUnformatted(channel.ToChatType().Name());
            } else if (Plugin.ExtraChat.ChannelOverride is var (overrideName, _)) {
                ImGui.TextUnformatted(overrideName);
            } else {
                DrawChunks(Plugin.Functions.Chat.Channel.name);
            }
        }
        finally
        {
            ImGui.PopStyleVar();
        }

        var beforeIcon = ImGui.GetCursorPos();

        if (ImGuiUtil.IconButton(FontAwesomeIcon.Comment) && activeTab is not { Channel: { } }) {
            ImGui.OpenPopup(ChatChannelPicker);
        }

        if (activeTab is { Channel: { } } && ImGui.IsItemHovered()) {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted(Language.ChatLog_SwitcherDisabled);
            ImGui.EndTooltip();
        }

        if (ImGui.BeginPopup(ChatChannelPicker)) {
            foreach (var channel in Enum.GetValues<InputChannel>()) {
                var name = Plugin.DataManager.GetExcelSheet<LogFilter>()!
                    .FirstOrDefault(row => row.LogKind == (byte) channel.ToChatType())
                    ?.Name
                    ?.RawString ?? channel.ToString();

                if (channel.IsLinkshell()) {
                    var lsName = Plugin.Functions.Chat.GetLinkshellName(channel.LinkshellIndex());
                    if (string.IsNullOrWhiteSpace(lsName)) {
                        continue;
                    }

                    name += $": {lsName}";
                }

                if (channel.IsCrossLinkshell()) {
                    var lsName = Plugin.Functions.Chat.GetCrossLinkshellName(channel.LinkshellIndex());
                    if (string.IsNullOrWhiteSpace(lsName)) {
                        continue;
                    }

                    name += $": {lsName}";
                }

                if (ImGui.Selectable(name)) {
                    Plugin.Functions.Chat.SetChannel(channel);
                    _tellTarget = null;
                }
            }

            ImGui.EndPopup();
        }

        ImGui.SameLine();
        var afterIcon = ImGui.GetCursorPos();

        var buttonWidth = afterIcon.X - beforeIcon.X;
        var showNovice = Plugin.Config.ShowNoviceNetwork && Plugin.Functions.IsMentor();
        var inputWidth = ImGui.GetContentRegionAvail().X - buttonWidth * (showNovice ? 2 : 1);

        var inputType = _tempChannel?.ToChatType() ?? activeTab?.Channel?.ToChatType() ?? Plugin.Functions.Chat.Channel.channel.ToChatType();
        var isCommand = Chat.Trim().StartsWith('/');
        if (isCommand) {
            var command = Chat.Split(' ')[0];
            if (TextCommandChannels.TryGetValue(command, out var channel)) {
                inputType = channel;
            }

            if (!IsValidCommand(command)) {
                inputType = ChatType.Error;
            }
        }

        var normalColour = *ImGui.GetStyleColorVec4(ImGuiCol.Text);

        var inputColour = Plugin.Config.ChatColours.TryGetValue(inputType, out var inputCol)
            ? inputCol
            : inputType.DefaultColour();

        if (!isCommand && Plugin.ExtraChat.ChannelOverride is var (_, overrideColour)) {
            inputColour = overrideColour;
        }

        if (isCommand && Plugin.ExtraChat.ChannelCommandColours.TryGetValue(Chat.Split(' ')[0], out var ecColour)) {
            inputColour = ecColour;
        }

        if (inputColour != null) {
            ImGui.PushStyleColor(ImGuiCol.Text, ColourUtil.RgbaToAbgr(inputColour.Value));
        }

        if (Activate) {
            ImGui.SetKeyboardFocusHere();
        }

        var chatCopy = Chat;
        ImGui.SetNextItemWidth(inputWidth);
        const ImGuiInputTextFlags inputFlags = ImGuiInputTextFlags.CallbackAlways | ImGuiInputTextFlags.CallbackCharFilter |
                                               ImGuiInputTextFlags.CallbackCompletion | ImGuiInputTextFlags.CallbackHistory;
        ImGui.InputText("##chat2-input", ref Chat, 500, inputFlags, Callback);

        if (ImGui.IsItemDeactivated()) {
            if (ImGui.IsKeyDown(ImGuiKey.Escape)) {
                Chat = chatCopy;

                if (Plugin.Functions.Chat.UsesTellTempChannel)
                {
                    Plugin.Functions.Chat.UsesTellTempChannel = false;
                    Plugin.Functions.Chat.SetChannel(Plugin.Functions.Chat.PreviousChannel ?? InputChannel.Say);
                }
            }

            var enter = ImGui.IsKeyDown(ImGuiKey.Enter) || ImGui.IsKeyDown(ImGuiKey.KeypadEnter);
            if (enter) {
                Plugin.CommandHelpWindow.IsOpen = false;
                SendChatBox(activeTab);

                if (Plugin.Functions.Chat.UsesTellTempChannel)
                {
                    Plugin.Functions.Chat.UsesTellTempChannel = false;
                    Plugin.Functions.Chat.SetChannel(Plugin.Functions.Chat.PreviousChannel ?? InputChannel.Say);
                }
            }
        }

        if (ImGui.IsItemActive()) {
            HandleKeybinds(true);
        }

        if (!Activate && !ImGui.IsItemActive()) {
            if (_tempChannel is InputChannel.Tell) {
                _tellTarget = null;
            }

            _tempChannel = null;
            if (Plugin.Functions.Chat.UsesTellTempChannel)
            {
                Plugin.Functions.Chat.UsesTellTempChannel = false;
                Plugin.Functions.Chat.SetChannel(Plugin.Functions.Chat.PreviousChannel ?? InputChannel.Say);
            }
        }

        if (ImGui.BeginPopupContextItem()) {
            ImGui.PushStyleColor(ImGuiCol.Text, normalColour);

            try {
                if (ImGui.Selectable(Language.ChatLog_HideChat)) {
                    UserHide();
                }
            } finally {
                ImGui.PopStyleColor();
            }

            ImGui.EndPopup();
        }

        if (inputColour != null) {
            ImGui.PopStyleColor();
        }

        ImGui.SameLine();

        if (ImGuiUtil.IconButton(FontAwesomeIcon.Cog)) {
            Plugin.SettingsWindow.IsOpen ^= true;
        }

        if (showNovice) {
            ImGui.SameLine();

            if (ImGuiUtil.IconButton(FontAwesomeIcon.Leaf)) {
                Plugin.Functions.ClickNoviceNetworkButton();
            }
        }
    }

    private void SendChatBox(Tab? activeTab) {
        if (!string.IsNullOrWhiteSpace(Chat)) {
            var trimmed = Chat.Trim();
            AddBacklog(trimmed);
            _inputBacklogIdx = -1;

            if (!trimmed.StartsWith('/')) {
                if (_tellTarget != null) {
                    var target = _tellTarget;
                    var reason = target.Reason;
                    var world = Plugin.DataManager.GetExcelSheet<World>()?.GetRow(target.World);
                    if (world is { IsPublic: true }) {
                        if (reason == TellReason.Reply && Plugin.Common.Functions.FriendList.List.Any(friend => friend.ContentId == target.ContentId)) {
                            reason = TellReason.Friend;
                        }

                        Plugin.Functions.Chat.SendTell(reason, target.ContentId, target.Name, (ushort) world.RowId, trimmed);
                    }

                    if (_tempChannel is InputChannel.Tell) {
                        _tellTarget = null;
                    }

                    goto Skip;
                }


                if (_tempChannel != null)
                    trimmed = $"{_tempChannel.Value.Prefix()} {trimmed}";
                else if (activeTab is { Channel: { } channel })
                    trimmed = $"{channel.Prefix()} {trimmed}";
            }

            var bytes = Encoding.UTF8.GetBytes(trimmed);
            AutoTranslate.ReplaceWithPayload(Plugin.DataManager, ref bytes);

            Plugin.Common.Functions.Chat.SendMessageUnsafe(bytes);
        }

        Skip:
        Chat = string.Empty;
    }

    internal void UserHide() {
        _hideState = HideState.User;
    }

    internal void DrawMessageLog(Tab tab, PayloadHandler handler, float childHeight, bool switchedTab) {
        if (ImGui.BeginChild("##chat2-messages", new Vector2(-1, childHeight))) {
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
            var table = tab.DisplayTimestamp && Plugin.Config.PrettierTimestamps;

            var oldCellPaddingY = ImGui.GetStyle().CellPadding.Y;
            if (Plugin.Config.PrettierTimestamps && Plugin.Config.MoreCompactPretty) {
                var padding = ImGui.GetStyle().CellPadding;
                padding.Y = 0;

                ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, padding);
            }

            if (table) {
                if (!ImGui.BeginTable("timestamp-table", 2, ImGuiTableFlags.PreciseWidths)) {
                    goto EndChild;
                }

                ImGui.TableSetupColumn("timestamps", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("messages", ImGuiTableColumnFlags.WidthStretch);
            }

            try {
                tab.MessagesMutex.Wait();

                var reset = false;
                if (_lastResize.IsRunning && _lastResize.Elapsed.TotalSeconds > 0.25) {
                    _lastResize.Stop();
                    _lastResize.Reset();
                    reset = true;
                }

                var lastPos = ImGui.GetCursorPosY();
                var lastTimestamp = string.Empty;
                int? lastMessageHash = null;
                var sameCount = 0;
                for (var i = 0; i < tab.Messages.Count; i++) {
                    var message = tab.Messages[i];

                    if (reset) {
                        message.Height = null;
                        message.IsVisible = false;
                    }

                    if (Plugin.Config.CollapseDuplicateMessages) {
                        var messageHash = message.Hash;
                        var same = lastMessageHash == messageHash;
                        if (same) {
                            sameCount += 1;

                            if (i != tab.Messages.Count - 1) {
                                continue;
                            }
                        }

                        if (sameCount > 0) {
                            ImGui.SameLine();
                            DrawChunks(
                                new[] {
                                    new TextChunk(ChunkSource.None, null, $" ({sameCount + 1}x)") {
                                        FallbackColour = ChatType.System,
                                        Italic = true,
                                    },
                                },
                                true,
                                handler,
                                ImGui.GetContentRegionAvail().X
                            );
                            sameCount = 0;
                        }

                        lastMessageHash = messageHash;

                        if (same && i == tab.Messages.Count - 1) {
                            continue;
                        }
                    }

                    // go to next row
                    if (table) {
                        ImGui.TableNextColumn();
                    }

                    // message has rendered once
                    if (message.Height.HasValue) {
                        // message isn't visible, so render dummy
                        if (!message.IsVisible) {
                            var beforeDummy = ImGui.GetCursorPos();

                            if (table) {
                                // skip to the message column for vis test
                                ImGui.TableNextColumn();
                            }

                            ImGui.Dummy(new Vector2(10f, message.Height.Value));
                            message.IsVisible = ImGui.IsItemVisible();

                            if (message.IsVisible) {
                                if (table) {
                                    ImGui.TableSetColumnIndex(0);
                                }

                                ImGui.SetCursorPos(beforeDummy);
                            } else {
                                goto UpdateMessage;
                            }
                        }
                    }

                    if (tab.DisplayTimestamp) {
                        var timestamp = message.Date.ToLocalTime().ToString("t");
                        if (table) {
                            if (!Plugin.Config.HideSameTimestamps || timestamp != lastTimestamp) {
                                ImGui.TextUnformatted(timestamp);
                                lastTimestamp = timestamp;
                            }
                        } else {
                            DrawChunk(new TextChunk(ChunkSource.None, null, $"[{timestamp}]") {
                                Foreground = 0xFFFFFFFF,
                            });
                            ImGui.SameLine();
                        }
                    }

                    if (table) {
                        ImGui.TableNextColumn();
                    }

                    var lineWidth = ImGui.GetContentRegionAvail().X;

                    var beforeDraw = ImGui.GetCursorScreenPos();
                    if (message.Sender.Count > 0) {
                        DrawChunks(message.Sender, true, handler, lineWidth);
                        ImGui.SameLine();
                    }

                    if (message.Content.Count == 0) {
                        DrawChunks(new[] { new TextChunk(ChunkSource.Content, null, " ") }, true, handler, lineWidth);
                    } else {
                        DrawChunks(message.Content, true, handler, lineWidth);
                    }

                    var afterDraw = ImGui.GetCursorScreenPos();

                    message.Height = ImGui.GetCursorPosY() - lastPos;
                    if (Plugin.Config.PrettierTimestamps && !Plugin.Config.MoreCompactPretty) {
                        message.Height -= oldCellPaddingY * 2;
                        beforeDraw.Y += oldCellPaddingY;
                        afterDraw.Y -= oldCellPaddingY;
                    }

                    message.IsVisible = ImGui.IsRectVisible(beforeDraw, afterDraw);

                    UpdateMessage:
                    lastPos = ImGui.GetCursorPosY();
                }
            } finally {
                tab.MessagesMutex.Release();
                ImGui.PopStyleVar(Plugin.Config.PrettierTimestamps && Plugin.Config.MoreCompactPretty ? 2 : 1);
            }

            if (switchedTab || ImGui.GetScrollY() >= ImGui.GetScrollMaxY()) {
                ImGui.SetScrollHereY(1f);
            }

            handler.Draw();

            if (table) {
                ImGui.EndTable();
            }
        }

        EndChild:
        ImGui.EndChild();
    }

    private int DrawTabBar() {
        var currentTab = -1;

        if (!ImGui.BeginTabBar("##chat2-tabs")) {
            return currentTab;
        }

        for (var tabI = 0; tabI < Plugin.Config.Tabs.Count; tabI++) {
            var tab = Plugin.Config.Tabs[tabI];
            if (tab.PopOut) {
                continue;
            }

            var unread = tabI == LastTab || tab.UnreadMode == UnreadMode.None || tab.Unread == 0 ? "" : $" ({tab.Unread})";
            var draw = ImGui.BeginTabItem($"{tab.Name}{unread}###log-tab-{tabI}");
            DrawTabContextMenu(tab, tabI);

            if (!draw) {
                continue;
            }

            currentTab = tabI;
            var switchedTab = LastTab != tabI;
            LastTab = tabI;
            tab.Unread = 0;

            DrawMessageLog(tab, PayloadHandler, GetRemainingHeightForMessageLog(), switchedTab);

            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();

        return currentTab;
    }

    private int DrawTabSidebar() {
        var currentTab = -1;

        if (!ImGui.BeginTable("tabs-table", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.Resizable)) {
            return -1;
        }

        ImGui.TableSetupColumn("tabs", ImGuiTableColumnFlags.None, 1);
        ImGui.TableSetupColumn("chat", ImGuiTableColumnFlags.None, 4);

        ImGui.TableNextColumn();

        var switchedTab = false;
        var childHeight = GetRemainingHeightForMessageLog();
        if (ImGui.BeginChild("##chat2-tab-sidebar", new Vector2(-1, childHeight))) {
            for (var tabI = 0; tabI < Plugin.Config.Tabs.Count; tabI++) {
                var tab = Plugin.Config.Tabs[tabI];
                if (tab.PopOut) {
                    continue;
                }

                var unread = tabI == LastTab || tab.UnreadMode == UnreadMode.None || tab.Unread == 0 ? "" : $" ({tab.Unread})";
                var clicked = ImGui.Selectable($"{tab.Name}{unread}###log-tab-{tabI}", LastTab == tabI);
                DrawTabContextMenu(tab, tabI);

                if (!clicked) {
                    continue;
                }

                currentTab = tabI;
                switchedTab = LastTab != tabI;
                LastTab = tabI;
            }
        }

        ImGui.EndChild();

        ImGui.TableNextColumn();

        if (currentTab == -1 && LastTab < Plugin.Config.Tabs.Count) {
            currentTab = LastTab;
            Plugin.Config.Tabs[currentTab].Unread = 0;
        }

        if (currentTab > -1) {
            DrawMessageLog(Plugin.Config.Tabs[currentTab], PayloadHandler, childHeight, switchedTab);
        }

        ImGui.EndTable();

        return currentTab;
    }

    private void DrawTabContextMenu(Tab tab, int i) {
        if (!ImGui.BeginPopupContextItem()) {
            return;
        }

        var tabs = Plugin.Config.Tabs;
        var anyChanged = false;

        ImGui.PushID($"tab-context-menu-{i}");

        ImGui.SetNextItemWidth(250f * ImGuiHelpers.GlobalScale);
        if (ImGui.InputText("##tab-name", ref tab.Name, 128)) {
            anyChanged = true;
        }

        if (ImGuiUtil.IconButton(FontAwesomeIcon.TrashAlt, tooltip: Language.ChatLog_Tabs_Delete)) {
            tabs.RemoveAt(i);
            anyChanged = true;
        }

        ImGui.SameLine();

        var (leftIcon, leftTooltip) = Plugin.Config.SidebarTabView
            ? (FontAwesomeIcon.ArrowUp, Language.ChatLog_Tabs_MoveUp)
            : (FontAwesomeIcon.ArrowLeft, Language.ChatLog_Tabs_MoveLeft);
        if (ImGuiUtil.IconButton(leftIcon, tooltip: leftTooltip) && i > 0) {
            (tabs[i - 1], tabs[i]) = (tabs[i], tabs[i - 1]);
            ImGui.CloseCurrentPopup();
            anyChanged = true;
        }

        ImGui.SameLine();

        var (rightIcon, rightTooltip) = Plugin.Config.SidebarTabView
            ? (FontAwesomeIcon.ArrowDown, Language.ChatLog_Tabs_MoveDown)
            : (FontAwesomeIcon.ArrowRight, Language.ChatLog_Tabs_MoveRight);
        if (ImGuiUtil.IconButton(rightIcon, tooltip: rightTooltip) && i < tabs.Count - 1) {
            (tabs[i + 1], tabs[i]) = (tabs[i], tabs[i + 1]);
            ImGui.CloseCurrentPopup();
            anyChanged = true;
        }

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.WindowRestore, tooltip: Language.ChatLog_Tabs_PopOut)) {
            tab.PopOut = true;
            anyChanged = true;
        }

        if (anyChanged) {
            Plugin.SaveConfig();
        }

        ImGui.PopID();
        ImGui.EndPopup();
    }

    internal readonly List<bool> PopOutDocked = new();
    internal Dictionary<string, Window> PopOutWindows = new();
    private void DrawPopOuts() {
        HandlerLender.ResetCounter();

        if (PopOutDocked.Count != Plugin.Config.Tabs.Count) {
            PopOutDocked.Clear();
            PopOutDocked.AddRange(Enumerable.Repeat(false, Plugin.Config.Tabs.Count));
        }

        for (var i = 0; i < Plugin.Config.Tabs.Count; i++) {
            var tab = Plugin.Config.Tabs[i];
            if (!tab.PopOut)
                continue;

            if (PopOutWindows.ContainsKey($"{tab.Name}{i}"))
                continue;

            var window = new Popout(this, tab, i) { IsOpen = true };

            Plugin.WindowSystem.AddWindow(window);
            PopOutWindows.Add($"{tab.Name}{i}", window);
        }
    }

    private unsafe void DrawAutoComplete() {
        if (_autoCompleteInfo == null) {
            return;
        }

        _autoCompleteList ??= AutoTranslate.Matching(Plugin.DataManager, _autoCompleteInfo.ToComplete, Plugin.Config.SortAutoTranslate);

        if (_autoCompleteOpen) {
            ImGui.OpenPopup(AutoCompleteId);
            _autoCompleteOpen = false;
        }

        ImGui.SetNextWindowSize(new Vector2(400, 300) * ImGuiHelpers.GlobalScale);
        if (!ImGui.BeginPopup(AutoCompleteId)) {
            if (_activatePos == -1) {
                _activatePos = _autoCompleteInfo.EndPos;
            }

            _autoCompleteInfo = null;
            _autoCompleteList = null;
            Activate = true;
            return;
        }

        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextWithHint("##auto-complete-filter", Language.AutoTranslate_Search_Hint, ref _autoCompleteInfo.ToComplete, 256, ImGuiInputTextFlags.CallbackAlways | ImGuiInputTextFlags.CallbackHistory, AutoCompleteCallback)) {
            _autoCompleteList = AutoTranslate.Matching(Plugin.DataManager, _autoCompleteInfo.ToComplete, Plugin.Config.SortAutoTranslate);
            _autoCompleteSelection = 0;
            _autoCompleteShouldScroll = true;
        }

        var selected = -1;
        if (ImGui.IsItemActive() && ImGui.GetIO().KeyCtrl) {
            for (var i = 0; i < 10 && i < _autoCompleteList.Count; i++) {
                var num = (i + 1) % 10;
                var key = ImGuiKey._0 + num;
                var key2 = ImGuiKey.Keypad0 + num;
                if (ImGui.IsKeyDown(key) || ImGui.IsKeyDown(key2)) {
                    selected = i;
                }
            }
        }

        if (ImGui.IsItemDeactivated()) {
            if (ImGui.IsKeyDown(ImGuiKey.Escape)) {
                ImGui.CloseCurrentPopup();
                goto End;
            }

            var enter = ImGui.IsKeyDown(ImGuiKey.Enter)
                        || ImGui.IsKeyDown(ImGuiKey.KeypadEnter);
            if (_autoCompleteList.Count > 0 && enter) {
                selected = _autoCompleteSelection;
            }
        }

        if (ImGui.IsWindowAppearing()) {
            _fixCursor = true;
            ImGui.SetKeyboardFocusHere(-1);
        }

        if (ImGui.BeginChild("##auto-complete-list", Vector2.Zero, false, ImGuiWindowFlags.HorizontalScrollbar)) {
            var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());

            clipper.Begin(_autoCompleteList.Count);
            while (clipper.Step()) {
                for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++) {
                    var entry = _autoCompleteList[i];

                    var highlight = _autoCompleteSelection == i;
                    var clicked = ImGui.Selectable($"{entry.String}##{entry.Group}/{entry.Row}", highlight) || selected == i;

                    if (i < 10) {
                        var button = (i + 1) % 10;
                        var text = string.Format(Language.AutoTranslate_Completion_Key, button);
                        var size = ImGui.CalcTextSize(text);
                        ImGui.SameLine(ImGui.GetContentRegionAvail().X - size.X);
                        ImGui.PushStyleColor(ImGuiCol.Text, *ImGui.GetStyleColorVec4(ImGuiCol.TextDisabled));
                        ImGui.TextUnformatted(text);
                        ImGui.PopStyleColor();
                    }

                    if (!clicked) {
                        continue;
                    }

                    var before = Chat[.._autoCompleteInfo.StartPos];
                    var after = Chat[_autoCompleteInfo.EndPos..];
                    var replacement = $"<at:{entry.Group},{entry.Row}>";
                    Chat = $"{before}{replacement}{after}";
                    ImGui.CloseCurrentPopup();
                    Activate = true;
                    _activatePos = _autoCompleteInfo.StartPos + replacement.Length;
                }
            }

            if (_autoCompleteShouldScroll) {
                _autoCompleteShouldScroll = false;
                var selectedPos = clipper.StartPosY + clipper.ItemsHeight * (_autoCompleteSelection * 1f);
                ImGui.SetScrollFromPosY(selectedPos - ImGui.GetWindowPos().Y);
            }

            ImGui.EndChild();
        }

        End:
        ImGui.EndPopup();
    }

    private unsafe int AutoCompleteCallback(ImGuiInputTextCallbackData* data) {
        if (_fixCursor && _autoCompleteInfo != null) {
            _fixCursor = false;
            data->CursorPos = _autoCompleteInfo.ToComplete.Length;
            data->SelectionStart = data->SelectionEnd = data->CursorPos;
        }

        if (_autoCompleteList == null) {
            return 0;
        }

        switch (data->EventKey) {
            case ImGuiKey.UpArrow: {
                if (_autoCompleteSelection == 0) {
                    _autoCompleteSelection = _autoCompleteList.Count - 1;
                } else {
                    _autoCompleteSelection--;
                }

                _autoCompleteShouldScroll = true;

                return 1;
            }
            case ImGuiKey.DownArrow: {
                if (_autoCompleteSelection == _autoCompleteList.Count - 1) {
                    _autoCompleteSelection = 0;
                } else {
                    _autoCompleteSelection++;
                }

                _autoCompleteShouldScroll = true;

                return 1;
            }
        }

        return 0;
    }

    private unsafe int Callback(ImGuiInputTextCallbackData* data) {
        var ptr = new ImGuiInputTextCallbackDataPtr(data);

        if (data->EventFlag == ImGuiInputTextFlags.CallbackCompletion) {
            if (ptr.CursorPos == 0) {
                _autoCompleteInfo = new AutoCompleteInfo(
                    string.Empty,
                    ptr.CursorPos,
                    ptr.CursorPos
                );
                _autoCompleteOpen = true;
                _autoCompleteSelection = 0;

                return 0;
            }

            int white;
            for (white = ptr.CursorPos - 1; white >= 0; white--) {
                if (data->Buf[white] == ' ') {
                    break;
                }
            }

            _autoCompleteInfo = new AutoCompleteInfo(
                Marshal.PtrToStringUTF8(ptr.Buf + white + 1, ptr.CursorPos - white - 1),
                white + 1,
                ptr.CursorPos
            );
            _autoCompleteOpen = true;
            _autoCompleteSelection = 0;
            return 0;
        }

        if (data->EventFlag == ImGuiInputTextFlags.CallbackCharFilter) {
            var valid = Plugin.Functions.Chat.IsCharValid((char) ptr.EventChar);
            if (!valid) {
                return 1;
            }
        }

        if (Activate) {
            Activate = false;
            data->CursorPos = _activatePos > -1 ? _activatePos : Chat.Length;
            data->SelectionStart = data->SelectionEnd = data->CursorPos;
            _activatePos = -1;
        }

        Plugin.CommandHelpWindow.IsOpen = false;
        var text = MemoryHelper.ReadString((IntPtr) data->Buf, data->BufTextLen);
        if (text.StartsWith('/')) {
            var command = text.Split(' ')[0];
            var cmd = Plugin.DataManager.GetExcelSheet<TextCommand>()?.FirstOrDefault(cmd => cmd.Command.RawString == command
                                                                                                     || cmd.Alias.RawString == command
                                                                                                     || cmd.ShortCommand.RawString == command
                                                                                                     || cmd.ShortAlias.RawString == command);
            if (cmd != null) {
                Plugin.CommandHelpWindow.UpdateContent(cmd);
                Plugin.CommandHelpWindow.IsOpen = true;
            }
        }

        if (data->EventFlag != ImGuiInputTextFlags.CallbackHistory) {
            return 0;
        }

        var prevPos = _inputBacklogIdx;

        switch (data->EventKey) {
            case ImGuiKey.UpArrow:
                switch (_inputBacklogIdx) {
                    case -1:
                        var offset = 0;

                        if (!string.IsNullOrWhiteSpace(Chat)) {
                            AddBacklog(Chat);
                            offset = 1;
                        }

                        _inputBacklogIdx = _inputBacklog.Count - 1 - offset;
                        break;
                    case > 0:
                        _inputBacklogIdx--;
                        break;
                }

                break;
            case ImGuiKey.DownArrow: {
                if (_inputBacklogIdx != -1) {
                    if (++_inputBacklogIdx >= _inputBacklog.Count) {
                        _inputBacklogIdx = -1;
                    }
                }

                break;
            }
        }

        if (prevPos == _inputBacklogIdx) {
            return 0;
        }

        var historyStr = _inputBacklogIdx >= 0 ? _inputBacklog[_inputBacklogIdx] : string.Empty;

        ptr.DeleteChars(0, ptr.BufTextLen);
        ptr.InsertChars(0, historyStr);

        return 0;
    }

    internal void DrawChunks(IReadOnlyList<Chunk> chunks, bool wrap = true, PayloadHandler? handler = null, float lineWidth = 0f) {
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        try {
            for (var i = 0; i < chunks.Count; i++) {
                if (chunks[i] is TextChunk text && string.IsNullOrEmpty(text.Content)) {
                    continue;
                }

                DrawChunk(chunks[i], wrap, handler, lineWidth);

                if (i < chunks.Count - 1) {
                    ImGui.SameLine();
                }
            }
        } finally {
            ImGui.PopStyleVar();
        }
    }

    private void DrawChunk(Chunk chunk, bool wrap = true, PayloadHandler? handler = null, float lineWidth = 0f) {
        if (chunk is IconChunk icon && _fontIcon != null) {
            var bounds = IconUtil.GetBounds((byte) icon.Icon);
            if (bounds != null) {
                var texSize = new Vector2(_fontIcon.Width, _fontIcon.Height);

                var sizeRatio = Plugin.Config.FontSize / bounds.Value.W;
                var size = new Vector2(bounds.Value.Z, bounds.Value.W) * sizeRatio * ImGuiHelpers.GlobalScale;

                var uv0 = new Vector2(bounds.Value.X, bounds.Value.Y - 2) / texSize;
                var uv1 = new Vector2(bounds.Value.X + bounds.Value.Z, bounds.Value.Y - 2 + bounds.Value.W) / texSize;
                ImGui.Image(_fontIcon.ImGuiHandle, size, uv0, uv1);
                ImGuiUtil.PostPayload(chunk, handler);
            }

            return;
        }

        if (chunk is not TextChunk text) {
            return;
        }

        var colour = text.Foreground;
        if (colour == null && text.FallbackColour != null) {
            var type = text.FallbackColour.Value;
            colour = Plugin.Config.ChatColours.TryGetValue(type, out var col)
                ? col
                : type.DefaultColour();
        }

        if (colour != null) {
            colour = ColourUtil.RgbaToAbgr(colour.Value);
            ImGui.PushStyleColor(ImGuiCol.Text, colour.Value);
        }

        var pushed = false;
        if (text.Italic) {
            pushed = true;
            (Plugin.Config.FontsEnabled && Plugin.FontManager.ItalicFont != null ? Plugin.FontManager.ItalicFont : Plugin.FontManager.AxisItalic).Push();
        }

        var content = text.Content;
        if (ScreenshotMode) {
            if (chunk.Link is PlayerPayload playerPayload) {
                var hashCode = $"{Salt}{playerPayload.PlayerName}{playerPayload.World.RowId}".GetHashCode();
                content = $"Player {hashCode:X8}";
            } else if (Plugin.ClientState.LocalPlayer is { } player && content.Contains(player.Name.TextValue)) {
                var hashCode = $"{Salt}{player.Name.TextValue}{player.HomeWorld.Id}".GetHashCode();
                content = content.Replace(player.Name.TextValue, $"Player {hashCode:X8}");
            }
        }

        if (wrap) {
            ImGuiUtil.WrapText(content, chunk, handler, DefaultText, lineWidth);
        } else {
            ImGui.TextUnformatted(content);
            ImGuiUtil.PostPayload(chunk, handler);
        }

        if (pushed) {
            (Plugin.Config.FontsEnabled && Plugin.FontManager.ItalicFont != null ? Plugin.FontManager.ItalicFont : Plugin.FontManager.AxisItalic).Pop();
        }

        if (colour != null) {
            ImGui.PopStyleColor();
        }
    }
}
