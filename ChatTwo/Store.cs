using ChatTwo.Code;
using ChatTwo.Util;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Lumina.Excel.GeneratedSheets;

namespace ChatTwo;

internal class Store : IDisposable {
    internal sealed class MessagesLock : IDisposable {
        private Mutex Mutex { get; }
        internal List<Message> Messages { get; }

        internal MessagesLock(List<Message> messages, Mutex mutex) {
            this.Messages = messages;
            this.Mutex = mutex;

            this.Mutex.WaitOne();
        }

        public void Dispose() {
            this.Mutex.ReleaseMutex();
        }
    }

    private Plugin Plugin { get; }

    private Mutex MessagesMutex { get; } = new();
    private List<Message> Messages { get; } = new();

    private Dictionary<ChatType, NameFormatting> Formats { get; } = new();

    internal Store(Plugin plugin) {
        this.Plugin = plugin;

        this.Plugin.ChatGui.ChatMessageUnhandled += this.ChatMessage;
    }

    public void Dispose() {
        this.Plugin.ChatGui.ChatMessageUnhandled -= this.ChatMessage;

        this.MessagesMutex.Dispose();
    }

    internal MessagesLock GetMessages() {
        return new MessagesLock(this.Messages, this.MessagesMutex);
    }

    internal void AddMessage(Message message) {
        using var messages = this.GetMessages();
        messages.Messages.Add(message);

        if (messages.Messages.Count > 1_000) {
            messages.Messages.RemoveAt(0);
        }

        foreach (var tab in this.Plugin.Config.Tabs) {
            if (tab.Matches(message)) {
                tab.AddMessage(message);
            }
        }
    }

    internal void FilterAllTabs() {
        foreach (var tab in this.Plugin.Config.Tabs) {
            this.FilterTab(tab);
        }
    }

    internal void FilterTab(Tab tab) {
        using var messages = this.GetMessages();
        foreach (var message in messages.Messages) {
            if (tab.Matches(message)) {
                tab.AddMessage(message);
            }
        }
    }

    private void ChatMessage(XivChatType type, uint senderId, SeString sender, SeString message) {
        var chatCode = new ChatCode((ushort) type);

        NameFormatting? formatting = null;
        if (sender.Payloads.Count > 0) {
            formatting = this.FormatFor(chatCode.Type);
        }

        var senderChunks = new List<Chunk>();
        if (formatting is { IsPresent: true }) {
            senderChunks.Add(new TextChunk(null, formatting.Before) {
                FallbackColour = chatCode.Type,
            });
            senderChunks.AddRange(ChunkUtil.ToChunks(sender, chatCode.Type));
            senderChunks.Add(new TextChunk(null, formatting.After) {
                FallbackColour = chatCode.Type,
            });
        }

        var messageChunks = ChunkUtil.ToChunks(message, chatCode.Type).ToList();

        this.AddMessage(new Message(chatCode, senderChunks, messageChunks));
    }

    internal class NameFormatting {
        internal string Before { get; private set; } = string.Empty;
        internal string After { get; private set; } = string.Empty;
        internal bool IsPresent { get; private set; } = true;

        internal static NameFormatting Empty() {
            return new() {
                IsPresent = false,
            };
        }

        internal static NameFormatting Of(string before, string after) {
            return new() {
                Before = before,
                After = after,
            };
        }
    }

    private NameFormatting? FormatFor(ChatType type) {
        if (this.Formats.TryGetValue(type, out var cached)) {
            return cached;
        }

        var logKind = this.Plugin.DataManager.GetExcelSheet<LogKind>()!.GetRow((ushort) type);

        if (logKind == null) {
            return null;
        }

        var format = (SeString) logKind.Format;

        static bool IsStringParam(Payload payload, byte num) {
            var data = payload.Encode();

            return data.Length >= 5 && data[1] == 0x29 && data[4] == num + 1;
        }

        var firstStringParam = format.Payloads.FindIndex(payload => IsStringParam(payload, 1));
        var secondStringParam = format.Payloads.FindIndex(payload => IsStringParam(payload, 2));

        if (firstStringParam == -1 || secondStringParam == -1) {
            return NameFormatting.Empty();
        }

        var before = format.Payloads
            .GetRange(0, firstStringParam)
            .Where(payload => payload is ITextProvider)
            .Cast<ITextProvider>()
            .Select(text => text.Text);
        var after = format.Payloads
            .GetRange(firstStringParam + 1, secondStringParam - firstStringParam)
            .Where(payload => payload is ITextProvider)
            .Cast<ITextProvider>()
            .Select(text => text.Text);

        var nameFormatting = NameFormatting.Of(
            string.Join("", before),
            string.Join("", after)
        );

        this.Formats[type] = nameFormatting;

        return nameFormatting;
    }
}
