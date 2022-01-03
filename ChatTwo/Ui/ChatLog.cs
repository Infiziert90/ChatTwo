using System.Numerics;
using ChatTwo.Code;
using ChatTwo.Util;
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

        this._fontIcon = this.Ui.Plugin.DataManager.GetImGuiTexture("common/font/fonticon_ps5.tex");

        this.Ui.Plugin.Functions.ChatActivated += this.ChatActivated;
    }

    public void Dispose() {
        this.Ui.Plugin.Functions.ChatActivated -= this.ChatActivated;
        this._fontIcon?.Dispose();
    }

    private void ChatActivated(string? input) {
        this.Activate = true;
        if (input != null && !this.Chat.Contains(input)) {
            this.Chat += input;
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

    public unsafe void Draw() {
        if (!ImGui.Begin($"{this.Ui.Plugin.Name}##chat", ImGuiWindowFlags.NoTitleBar)) {
            ImGui.End();
            return;
        }

        var lineHeight = ImGui.CalcTextSize("A").Y;

        if (ImGui.BeginTabBar("##chat2-tabs")) {
            for (var tabI = 0; tabI < this.Ui.Plugin.Config.Tabs.Count; tabI++) {
                var tab = this.Ui.Plugin.Config.Tabs[tabI];

                var unread = tabI == this._lastTab || !tab.DisplayUnread || tab.Unread == 0 ? "" : $" ({tab.Unread})";
                if (ImGui.BeginTabItem($"{tab.Name}{unread}###log-tab-{tabI}")) {
                    var switchedTab = this._lastTab != tabI;
                    this._lastTab = tabI;
                    tab.Unread = 0;

                    // var drawnHeight = 0f;
                    // var numDrawn = 0;
                    // var lastPos = ImGui.GetCursorPosY();
                    var height = ImGui.GetContentRegionAvail().Y
                                 - lineHeight * 2
                                 - ImGui.GetStyle().ItemSpacing.Y * 4;
                    if (ImGui.BeginChild("##chat2-messages", new Vector2(-1, height))) {
                        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);

                        // var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
                        // int numMessages;
                        try {
                            tab.MessagesMutex.WaitOne();

                            for (var i = 0; i < tab.Messages.Count; i++) {
                                // numDrawn += 1;
                                var message = tab.Messages[i];

                                if (tab.DisplayTimestamp) {
                                    var timestamp = message.Date.ToLocalTime().ToString("t");
                                    this.DrawChunk(new TextChunk(null, null, $"[{timestamp}]") {
                                        Foreground = 0xFFFFFFFF,
                                    });
                                    ImGui.SameLine();
                                }

                                if (message.Sender.Count > 0) {
                                    this.DrawChunks(message.Sender, this.PayloadHandler);
                                    ImGui.SameLine();
                                }

                                this.DrawChunks(message.Content, this.PayloadHandler);

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
                            ImGui.PopStyleVar();
                        }

                        // PluginLog.Log($"numDrawn: {numDrawn}");

                        if (switchedTab || ImGui.GetScrollY() >= ImGui.GetScrollMaxY()) {
                            // PluginLog.Log($"drawnHeight: {drawnHeight}");
                            // var itemPosY = clipper.StartPosY + drawnHeight;
                            // PluginLog.Log($"itemPosY: {itemPosY}");
                            // ImGui.SetScrollFromPosY(itemPosY - ImGui.GetWindowPos().Y);
                            ImGui.SetScrollHereY(1f);
                        }
                    }

                    this.PayloadHandler.Draw();

                    ImGui.EndChild();

                    ImGui.EndTabItem();
                }
            }

            ImGui.EndTabBar();
        }

        if (this.Activate) {
            ImGui.SetKeyboardFocusHere();
        }

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        try {
            this.DrawChunks(this.Ui.Plugin.Functions.ChatChannel.name);
        } finally {
            ImGui.PopStyleVar();
        }

        var beforeIcon = ImGui.GetCursorPos();

        if (ImGuiUtil.IconButton(FontAwesomeIcon.Comment)) {
            ImGui.OpenPopup(ChatChannelPicker);
        }

        if (ImGui.BeginPopup(ChatChannelPicker)) {
            foreach (var channel in Enum.GetValues<InputChannel>()) {
                var name = this.Ui.Plugin.DataManager.GetExcelSheet<LogFilter>()!
                    .FirstOrDefault(row => row.LogKind == (byte) channel.ToChatType())
                    ?.Name
                    ?.RawString ?? channel.ToString();

                if (ImGui.Selectable(name)) {
                    this.Ui.Plugin.Functions.SetChatChannel(channel);
                }
            }

            ImGui.EndPopup();
        }

        ImGui.SameLine();
        var afterIcon = ImGui.GetCursorPos();

        var buttonWidth = afterIcon.X - beforeIcon.X;
        var inputWidth = ImGui.GetContentRegionAvail().X - buttonWidth;

        var inputType = this.Ui.Plugin.Functions.ChatChannel.channel.ToChatType();
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

    internal void DrawChunks(IReadOnlyList<Chunk> chunks, PayloadHandler? handler = null) {
        for (var i = 0; i < chunks.Count; i++) {
            this.DrawChunk(chunks[i], handler);

            if (i < chunks.Count - 1) {
                ImGui.SameLine();
            }
        }
    }

    private void DrawChunk(Chunk chunk, PayloadHandler? handler = null) {
        if (chunk is IconChunk icon && this._fontIcon != null) {
            var bounds = IconUtil.GetBounds((byte) icon.Icon);
            if (bounds != null) {
                var texSize = new Vector2(this._fontIcon.Width, this._fontIcon.Height);

                var sizeRatio = this.Ui.Plugin.Config.FontSize / bounds.Value.W;
                var size = new Vector2(bounds.Value.Z, bounds.Value.W) * sizeRatio;

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

        ImGuiUtil.WrapText(text.Content, chunk, handler);

        if (text.Italic && this.Ui.ItalicFont.HasValue) {
            ImGui.PopFont();
        }

        if (colour != null) {
            ImGui.PopStyleColor();
        }
    }
}
