using System.Numerics;
using ChatTwo.Code;
using ChatTwo.Util;
using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using ImGuiNET;
using ImGuiScene;
using Lumina.Excel.GeneratedSheets;

namespace ChatTwo.Ui;

internal sealed class ChatLog : IUiComponent {
    private const string ChatChannelPicker = "chat-channel-picker";

    private PluginUi Ui { get; }

    internal bool Activate;
    internal string Chat = string.Empty;
    private readonly TextureWrap? _fontIcon;
    private readonly List<string> _inputBacklog = new();
    private int _inputBacklogIdx = -1;
    private int _lastTab;

    private PayloadHandler PayloadHandler { get; }

    internal ChatLog(PluginUi ui) {
        this.Ui = ui;
        this.PayloadHandler = new PayloadHandler(this.Ui, this);
        this.Ui.Plugin.CommandManager.AddHandler("/clearlog2", new CommandInfo(this.ClearLog) {
            HelpMessage = "Clears the Chat 2 chat log",
        });

        this._fontIcon = this.Ui.Plugin.DataManager.GetImGuiTexture("common/font/fonticon_ps5.tex");

        this.Ui.Plugin.Functions.Chat.Activated += this.Activated;
    }

    public void Dispose() {
        this.Ui.Plugin.Functions.Chat.Activated -= this.Activated;
        this._fontIcon?.Dispose();
        this.Ui.Plugin.CommandManager.RemoveHandler("/clearlog2");
    }

    private void Activated(string? input) {
        this.Activate = true;
        if (input != null && !this.Chat.Contains(input)) {
            this.Chat += input;
        }
    }

    private void ClearLog(string command, string arguments) {
        switch (arguments) {
            case "all":
                using (var messages = this.Ui.Plugin.Store.GetMessages()) {
                    messages.Messages.Clear();
                }

                foreach (var tab in this.Ui.Plugin.Config.Tabs) {
                    tab.Clear();
                }

                break;
            case "help":
                this.Ui.Plugin.ChatGui.Print("- /clearlog2: clears the active tab's log");
                this.Ui.Plugin.ChatGui.Print("- /clearlog2 all: clears all tabs' logs and the global history");
                this.Ui.Plugin.ChatGui.Print("- /clearlog2 help: shows this help");

                break;
            default:
                if (this._lastTab > -1 && this._lastTab < this.Ui.Plugin.Config.Tabs.Count) {
                    this.Ui.Plugin.Config.Tabs[this._lastTab].Clear();
                }

                break;
        }
    }

    private void AddBacklog(string message) {
        for (var i = 0; i < this._inputBacklog.Count; i++) {
            if (this._inputBacklog[i] != message) {
                continue;
            }

            this._inputBacklog.RemoveAt(i);
            break;
        }

        this._inputBacklog.Add(message);
    }

    private static float GetRemainingHeightForMessageLog() {
        var lineHeight = ImGui.CalcTextSize("A").Y;
        return ImGui.GetContentRegionAvail().Y
               - lineHeight * 2
               - ImGui.GetStyle().ItemSpacing.Y * 4;
    }

    private unsafe ImGuiViewport* _lastViewport;

    public unsafe void Draw() {
        var flags = ImGuiWindowFlags.None;
        if (!this.Ui.Plugin.Config.CanMove) {
            flags |= ImGuiWindowFlags.NoMove;
        }

        if (!this.Ui.Plugin.Config.CanResize) {
            flags |= ImGuiWindowFlags.NoResize;
        }

        if (!this.Ui.Plugin.Config.ShowTitleBar) {
            flags |= ImGuiWindowFlags.NoTitleBar;
        }

        if (this._lastViewport == ImGuiHelpers.MainViewport.NativePtr) {
            ImGui.SetNextWindowBgAlpha(this.Ui.Plugin.Config.WindowAlpha);
        }

        if (!ImGui.Begin($"{this.Ui.Plugin.Name}##chat", flags)) {
            this._lastViewport = ImGui.GetWindowViewport().NativePtr;
            ImGui.End();
            return;
        }

        this._lastViewport = ImGui.GetWindowViewport().NativePtr;

        var currentTab = this.Ui.Plugin.Config.SidebarTabView
            ? this.DrawTabSidebar()
            : this.DrawTabBar();

        if (this.Activate) {
            ImGui.SetKeyboardFocusHere();
        }

        Tab? activeTab = null;
        if (currentTab > -1 && currentTab < this.Ui.Plugin.Config.Tabs.Count) {
            activeTab = this.Ui.Plugin.Config.Tabs[currentTab];
        }

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        try {
            if (activeTab is { Channel: { } channel }) {
                ImGui.TextUnformatted(channel.ToChatType().Name());
            } else {
                this.DrawChunks(this.Ui.Plugin.Functions.Chat.Channel.name);
            }
        } finally {
            ImGui.PopStyleVar();
        }

        var beforeIcon = ImGui.GetCursorPos();

        if (ImGuiUtil.IconButton(FontAwesomeIcon.Comment) && activeTab is not { Channel: { } }) {
            ImGui.OpenPopup(ChatChannelPicker);
        }

        if (activeTab is { Channel: { } } && ImGui.IsItemHovered()) {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted("Disabled for this tab.");
            ImGui.EndTooltip();
        }

        if (ImGui.BeginPopup(ChatChannelPicker)) {
            foreach (var channel in Enum.GetValues<InputChannel>()) {
                var name = this.Ui.Plugin.DataManager.GetExcelSheet<LogFilter>()!
                    .FirstOrDefault(row => row.LogKind == (byte) channel.ToChatType())
                    ?.Name
                    ?.RawString ?? channel.ToString();

                if (ImGui.Selectable(name)) {
                    this.Ui.Plugin.Functions.Chat.SetChannel(channel);
                }
            }

            ImGui.EndPopup();
        }

        ImGui.SameLine();
        var afterIcon = ImGui.GetCursorPos();

        var buttonWidth = afterIcon.X - beforeIcon.X;
        var inputWidth = ImGui.GetContentRegionAvail().X - buttonWidth;

        var inputType = this.Ui.Plugin.Functions.Chat.Channel.channel.ToChatType();
        var inputColour = this.Ui.Plugin.Config.ChatColours.TryGetValue(inputType, out var inputCol)
            ? inputCol
            : inputType.DefaultColour();

        if (inputColour != null) {
            ImGui.PushStyleColor(ImGuiCol.Text, ColourUtil.RgbaToAbgr(inputColour.Value));
        }

        ImGui.SetNextItemWidth(inputWidth);
        const ImGuiInputTextFlags inputFlags = ImGuiInputTextFlags.EnterReturnsTrue
                                               | ImGuiInputTextFlags.CallbackAlways
                                               | ImGuiInputTextFlags.CallbackHistory;
        if (ImGui.InputText("##chat2-input", ref this.Chat, 500, inputFlags, this.Callback)) {
            if (!string.IsNullOrWhiteSpace(this.Chat)) {
                var trimmed = this.Chat.Trim();
                this.AddBacklog(trimmed);
                this._inputBacklogIdx = -1;

                if (activeTab is { Channel: { } channel } && !trimmed.StartsWith('/')) {
                    trimmed = $"{channel.Prefix()} {trimmed}";
                }

                this.Ui.Plugin.Common.Functions.Chat.SendMessage(trimmed);
            }

            this.Chat = string.Empty;
        }

        if (inputColour != null) {
            ImGui.PopStyleColor();
        }

        ImGui.SameLine();

        if (ImGuiUtil.IconButton(FontAwesomeIcon.Cog)) {
            this.Ui.SettingsVisible ^= true;
        }

        ImGui.End();
    }

    private void DrawMessageLog(Tab tab, float childHeight, bool switchedTab) {
        if (ImGui.BeginChild("##chat2-messages", new Vector2(-1, childHeight))) {
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
            var table = tab.DisplayTimestamp && this.Ui.Plugin.Config.PrettierTimestamps;

            if (this.Ui.Plugin.Config.MoreCompactPretty) {
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

            // var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
            // int numMessages;
            try {
                tab.MessagesMutex.WaitOne();

                for (var i = 0; i < tab.Messages.Count; i++) {
                    // numDrawn += 1;
                    var message = tab.Messages[i];

                    if (tab.DisplayTimestamp) {
                        var timestamp = message.Date.ToLocalTime().ToString("t");
                        if (table) {
                            ImGui.TableNextColumn();
                            ImGui.TextUnformatted(timestamp);
                        } else {
                            this.DrawChunk(new TextChunk(null, null, $"[{timestamp}]") {
                                Foreground = 0xFFFFFFFF,
                            });
                            ImGui.SameLine();
                        }
                    }

                    if (table) {
                        ImGui.TableNextColumn();
                    }

                    if (message.Sender.Count > 0) {
                        this.DrawChunks(message.Sender, true, this.PayloadHandler);
                        ImGui.SameLine();
                    }

                    this.DrawChunks(message.Content, true, this.PayloadHandler);

                    // drawnHeight += ImGui.GetCursorPosY() - lastPos;
                    // lastPos = ImGui.GetCursorPosY();
                }

                // numMessages = tab.Messages.Count;
                // may render too many items, but this is easier
                // clipper.Begin(numMessages, lineHeight + ImGui.GetStyle().ItemSpacing.Y);
                // while (clipper.Step()) {
                // }
            } finally {
                tab.MessagesMutex.ReleaseMutex();
                ImGui.PopStyleVar(this.Ui.Plugin.Config.MoreCompactPretty ? 2 : 1);
            }

            // PluginLog.Log($"numDrawn: {numDrawn}");

            if (switchedTab || ImGui.GetScrollY() >= ImGui.GetScrollMaxY()) {
                // PluginLog.Log($"drawnHeight: {drawnHeight}");
                // var itemPosY = clipper.StartPosY + drawnHeight;
                // PluginLog.Log($"itemPosY: {itemPosY}");
                // ImGui.SetScrollFromPosY(itemPosY - ImGui.GetWindowPos().Y);
                ImGui.SetScrollHereY(1f);
            }

            this.PayloadHandler.Draw();

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

        for (var tabI = 0; tabI < this.Ui.Plugin.Config.Tabs.Count; tabI++) {
            var tab = this.Ui.Plugin.Config.Tabs[tabI];

            var unread = tabI == this._lastTab || !tab.DisplayUnread || tab.Unread == 0 ? "" : $" ({tab.Unread})";
            var draw = ImGui.BeginTabItem($"{tab.Name}{unread}###log-tab-{tabI}");
            this.DrawTabContextMenu(tab, tabI);

            if (!draw) {
                continue;
            }

            currentTab = tabI;
            var switchedTab = this._lastTab != tabI;
            this._lastTab = tabI;
            tab.Unread = 0;

            this.DrawMessageLog(tab, GetRemainingHeightForMessageLog(), switchedTab);

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
            for (var tabI = 0; tabI < this.Ui.Plugin.Config.Tabs.Count; tabI++) {
                var tab = this.Ui.Plugin.Config.Tabs[tabI];

                var unread = tabI == this._lastTab || !tab.DisplayUnread || tab.Unread == 0 ? "" : $" ({tab.Unread})";
                var clicked = ImGui.Selectable($"{tab.Name}{unread}###log-tab-{tabI}", this._lastTab == tabI);
                this.DrawTabContextMenu(tab, tabI);

                if (!clicked) {
                    continue;
                }

                currentTab = tabI;
                switchedTab = this._lastTab != tabI;
                this._lastTab = tabI;
            }
        }

        ImGui.EndChild();

        ImGui.TableNextColumn();

        if (currentTab == -1 && this._lastTab < this.Ui.Plugin.Config.Tabs.Count) {
            currentTab = this._lastTab;
            this.Ui.Plugin.Config.Tabs[currentTab].Unread = 0;
        }

        if (currentTab > -1) {
            this.DrawMessageLog(this.Ui.Plugin.Config.Tabs[currentTab], childHeight, switchedTab);
        }

        ImGui.EndTable();

        return currentTab;
    }

    private void DrawTabContextMenu(Tab tab, int i) {
        if (!ImGui.BeginPopupContextItem()) {
            return;
        }

        var tabs = this.Ui.Plugin.Config.Tabs;
        var anyChanged = false;

        ImGui.PushID($"tab-context-menu-{i}");

        ImGui.SetNextItemWidth(250f * ImGuiHelpers.GlobalScale);
        if (ImGui.InputText("##tab-name", ref tab.Name, 128)) {
            anyChanged = true;
        }

        if (ImGuiUtil.IconButton(FontAwesomeIcon.TrashAlt, tooltip: "Delete tab")) {
            tabs.RemoveAt(i);
            anyChanged = true;
        }

        ImGui.SameLine();

        var (leftIcon, leftTooltip) = this.Ui.Plugin.Config.SidebarTabView
            ? (FontAwesomeIcon.ArrowUp, "Move up")
            : ((FontAwesomeIcon) 61536, "Move left");
        if (ImGuiUtil.IconButton(leftIcon, tooltip: leftTooltip) && i > 0) {
            (tabs[i - 1], tabs[i]) = (tabs[i], tabs[i - 1]);
            ImGui.CloseCurrentPopup();
            anyChanged = true;
        }

        ImGui.SameLine();

        var (rightIcon, rightTooltip) = this.Ui.Plugin.Config.SidebarTabView
            ? (FontAwesomeIcon.ArrowDown, "Move down")
            : (FontAwesomeIcon.ArrowRight, "Move right");
        if (ImGuiUtil.IconButton(rightIcon, tooltip: rightTooltip) && i < tabs.Count - 1) {
            (tabs[i + 1], tabs[i]) = (tabs[i], tabs[i + 1]);
            ImGui.CloseCurrentPopup();
            anyChanged = true;
        }

        if (anyChanged) {
            this.Ui.Plugin.SaveConfig();
        }

        ImGui.PopID();
        ImGui.EndPopup();
    }

    private unsafe int Callback(ImGuiInputTextCallbackData* data) {
        var ptr = new ImGuiInputTextCallbackDataPtr(data);

        if (this.Activate) {
            this.Activate = false;
            data->CursorPos = this.Chat.Length;
            data->SelectionStart = data->SelectionEnd = data->CursorPos;
        }

        if (data->EventFlag != ImGuiInputTextFlags.CallbackHistory) {
            return 0;
        }

        var prevPos = this._inputBacklogIdx;

        switch (data->EventKey) {
            case ImGuiKey.UpArrow:
                switch (this._inputBacklogIdx) {
                    case -1:
                        var offset = 0;

                        if (!string.IsNullOrWhiteSpace(this.Chat)) {
                            this.AddBacklog(this.Chat);
                            offset = 1;
                        }

                        this._inputBacklogIdx = this._inputBacklog.Count - 1 - offset;
                        break;
                    case > 0:
                        this._inputBacklogIdx--;
                        break;
                }

                break;
            case ImGuiKey.DownArrow: {
                if (this._inputBacklogIdx != -1) {
                    if (++this._inputBacklogIdx >= this._inputBacklog.Count) {
                        this._inputBacklogIdx = -1;
                    }
                }

                break;
            }
        }

        if (prevPos == this._inputBacklogIdx) {
            return 0;
        }

        var historyStr = this._inputBacklogIdx >= 0 ? this._inputBacklog[this._inputBacklogIdx] : string.Empty;

        ptr.DeleteChars(0, ptr.BufTextLen);
        ptr.InsertChars(0, historyStr);

        return 0;
    }

    internal void DrawChunks(IReadOnlyList<Chunk> chunks, bool wrap = true, PayloadHandler? handler = null) {
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        try {
            for (var i = 0; i < chunks.Count; i++) {
                if (chunks[i] is TextChunk text && string.IsNullOrEmpty(text.Content)) {
                    continue;
                }

                this.DrawChunk(chunks[i], wrap, handler);

                if (i < chunks.Count - 1) {
                    ImGui.SameLine();
                }
            }
        } finally {
            ImGui.PopStyleVar();
        }
    }

    private void DrawChunk(Chunk chunk, bool wrap = true, PayloadHandler? handler = null) {
        if (chunk is IconChunk icon && this._fontIcon != null) {
            var bounds = IconUtil.GetBounds((byte) icon.Icon);
            if (bounds != null) {
                var texSize = new Vector2(this._fontIcon.Width, this._fontIcon.Height);

                var sizeRatio = this.Ui.Plugin.Config.FontSize / bounds.Value.W;
                var size = new Vector2(bounds.Value.Z, bounds.Value.W) * sizeRatio * ImGuiHelpers.GlobalScale;

                var uv0 = new Vector2(bounds.Value.X, bounds.Value.Y - 2) / texSize;
                var uv1 = new Vector2(bounds.Value.X + bounds.Value.Z, bounds.Value.Y - 2 + bounds.Value.W) / texSize;
                ImGui.Image(this._fontIcon.ImGuiHandle, size, uv0, uv1);
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
            colour = this.Ui.Plugin.Config.ChatColours.TryGetValue(type, out var col)
                ? col
                : type.DefaultColour();
        }

        if (colour != null) {
            colour = ColourUtil.RgbaToAbgr(colour.Value);
            ImGui.PushStyleColor(ImGuiCol.Text, colour.Value);
        }

        if (text.Italic && this.Ui.ItalicFont.HasValue) {
            ImGui.PushFont(this.Ui.ItalicFont.Value);
        }

        var content = text.Content;
        if (this.Ui.ScreenshotMode) {
            if (chunk.Link is PlayerPayload playerPayload) {
                var hashCode = $"{this.Ui.Salt}{playerPayload.PlayerName}{playerPayload.World.RowId}".GetHashCode();
                content = $"Player {hashCode:X8}";
            } else if (this.Ui.Plugin.ClientState.LocalPlayer is { } player && content.Contains(player.Name.TextValue)) {
                var hashCode = $"{this.Ui.Salt}{player.Name.TextValue}{player.HomeWorld.Id}".GetHashCode();
                content = content.Replace(player.Name.TextValue, $"Player {hashCode:X8}");
            }
        }

        if (wrap) {
            ImGuiUtil.WrapText(content, chunk, handler);
        } else {
            ImGui.TextUnformatted(content);
            ImGuiUtil.PostPayload(chunk, handler);
        }

        if (text.Italic && this.Ui.ItalicFont.HasValue) {
            ImGui.PopFont();
        }

        if (colour != null) {
            ImGui.PopStyleColor();
        }
    }
}
