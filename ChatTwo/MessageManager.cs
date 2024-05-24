using System.Collections.Concurrent;
using System.Diagnostics;
using ChatTwo.Code;
using ChatTwo.Resources;
using ChatTwo.Util;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Plugin.Services;
using Lumina.Excel.GeneratedSheets;

namespace ChatTwo;

internal class MessageManager : IAsyncDisposable
{
    internal const int MessageDisplayLimit = 10_000;

    private Plugin Plugin { get; }
    internal MessageStore Store { get; }

    private Dictionary<ChatType, NameFormatting> Formats { get; } = new();
    private ulong LastContentId { get; set; }

    private ConcurrentQueue<PendingMessage> Pending { get; } = new();
    private int LastMessageIndex { get; set; }

    private readonly Thread PendingMessageThread;
    private readonly CancellationTokenSource PendingThreadCancellationToken = new();

    internal ulong CurrentContentId
    {
        get
        {
            var contentId = Plugin.ClientState.LocalContentId;
            return contentId == 0 ? LastContentId : contentId;
        }
    }

    internal MessageManager(Plugin plugin)
    {
        Plugin = plugin;
        Store = new MessageStore(DatabasePath());

        PendingMessageThread = new Thread(() => ProcessPendingMessages(PendingThreadCancellationToken.Token));
        PendingMessageThread.Start();

        Plugin.ChatGui.ChatMessageUnhandled += ChatMessage;
        Plugin.Framework.Update += UpdateReceiver;
        Plugin.ClientState.Logout += Logout;
    }

    public async ValueTask DisposeAsync()
    {
        Plugin.ClientState.Logout -= Logout;
        Plugin.Framework.Update -= UpdateReceiver;
        Plugin.ChatGui.ChatMessageUnhandled -= ChatMessage;

        await PendingThreadCancellationToken.CancelAsync();
        var timeout = 10_000; // 10s
        while (timeout > 0)
        {
            if (!PendingMessageThread.IsAlive)
                break;

            timeout -= 100;
            await Task.Delay(100);
            Plugin.Log.Debug("Sleeping because PendingMessageThread thread still alive");
        }

        Store.Dispose();
    }

    internal static string DatabasePath()
    {
        return Path.Join(Plugin.Interface.ConfigDirectory.FullName, "chat-sqlite.db");
    }

    private void Logout()
    {
        LastContentId = 0;
    }

    private void UpdateReceiver(IFramework framework)
    {
        var contentId = Plugin.ClientState.LocalContentId;
        if (contentId != 0)
            LastContentId = contentId;
    }

    private void ProcessPendingMessages(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (Pending.TryDequeue(out var pendingMessage))
            {
                try
                {
                    ProcessMessage(pendingMessage);
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error(ex, "Error processing pending message");
                }
            }
            else
            {
                Thread.Sleep(1);
            }
        }
    }

    internal void ClearAllTabs()
    {
        foreach (var tab in Plugin.Config.Tabs)
            tab.Clear();
    }

    internal void FilterAllTabs(bool unread = true)
    {
        DateTimeOffset? since = null;
        if (!Plugin.Config.FilterIncludePreviousSessions)
            since = Plugin.GameStarted;

        var messages = Store.GetMostRecentMessages(CurrentContentId, since);
        foreach (var message in messages)
            foreach (var tab in Plugin.Config.Tabs.Where(tab => tab.Matches(message)))
                tab.AddMessage(message, unread);

        if (!messages.DidError) return;

        WrapperUtil.AddNotification(Language.LoadMessages_Error, NotificationType.Error);

        // Mark the failed messages as deleted so we don't try to load them
        // again.
        var failedIds = messages.FailedMessageIds();
        Plugin.Log.Info($"Marking {failedIds.Count} messages as deleted due to parse failures");
        foreach (var msgId in messages.FailedMessageIds())
        {
            Plugin.Log.Debug($"Marking message '{msgId}' as deleted due to parse failure");
            Store.DeleteMessage(msgId);
        }
    }

    internal void FilterAllTabsAsync(bool unread = true)
    {
        Task.Run(() =>
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                FilterAllTabs(unread);
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Error in FilterAllTabs");
            }

            Plugin.Log.Debug($"FilterAllTabs took {stopwatch.ElapsedMilliseconds}ms");
        });
    }

    public (SeString? Sender, SeString? Message) LastMessage = (null, null);
    private void ChatMessage(XivChatType type, uint senderId, SeString sender, SeString message)
    {
        LastMessage = (sender, message);

        var pendingMessage = new PendingMessage
        {
            ReceiverId = CurrentContentId,
            ContentId = 0,
            Type = type,
            SenderId = senderId,
            Sender = sender,
            Content = message,
        };

        // Update colour codes.
        GlobalParametersCache.Refresh();

        // If the message was rendered in the vanilla chat log window it has an
        // index, and we can use that to get the sender's content ID. The
        // content ID is used to show "invite to party" buttons in the context
        // menu.
        var idx = Plugin.Functions.GetCurrentChatLogEntryIndex();
        var shouldGetContentId = false;
        if (idx > LastMessageIndex)
        {
            LastMessageIndex = idx;
            shouldGetContentId = true;
        }

        // You can't call GetContentIdForEntry in the same framework tick
        // that you received the message, or you just get null.
        //
        // We delay all messages to be enqueued in the next framework tick
        // because of this. We used to only delay messages that we wanted to
        // fetch a content ID for, but this results in out-of-order messages
        // occasionally.
        Plugin.Framework.RunOnTick(() =>
        {
            if (shouldGetContentId)
                pendingMessage.ContentId = Plugin.Functions.Chat.GetContentIdForEntry(idx - 1);
            Pending.Enqueue(pendingMessage);
        });
    }

    private void ProcessMessage(PendingMessage pendingMessage)
    {
        var chatCode = new ChatCode((ushort)pendingMessage.Type);

        NameFormatting? formatting = null;
        if (pendingMessage.Sender.Payloads.Count > 0)
            formatting = FormatFor(chatCode.Type);

        var senderChunks = new List<Chunk>();
        if (formatting is { IsPresent: true })
        {
            senderChunks.Add(new TextChunk(ChunkSource.None, null, formatting.Before)
            {
                FallbackColour = chatCode.Type,
            });
            senderChunks.AddRange(ChunkUtil.ToChunks(pendingMessage.Sender, ChunkSource.Sender, chatCode.Type));
            senderChunks.Add(new TextChunk(ChunkSource.None, null, formatting.After)
            {
                FallbackColour = chatCode.Type,
            });
        }

        var contentChunks = ChunkUtil.ToChunks(pendingMessage.Content, ChunkSource.Content, chatCode.Type).ToList();
        var message = new Message(CurrentContentId, pendingMessage.ContentId, chatCode, senderChunks, contentChunks, pendingMessage.Sender, pendingMessage.Content);

        if (Plugin.Config.DatabaseBattleMessages || !message.Code.IsBattle())
            Store.UpsertMessage(message);

        var currentMatches = Plugin.ChatLogWindow.CurrentTab?.Matches(message) ?? false;
        foreach (var tab in Plugin.Config.Tabs)
        {
            var unread = !(tab.UnreadMode == UnreadMode.Unseen && Plugin.ChatLogWindow.CurrentTab != tab && currentMatches);

            if (tab.Matches(message))
                tab.AddMessage(message, unread);
        }
    }

    internal class NameFormatting
    {
        internal string Before { get; private set; } = string.Empty;
        internal string After { get; private set; } = string.Empty;
        internal bool IsPresent { get; private set; } = true;

        internal static NameFormatting Empty()
        {
            return new NameFormatting { IsPresent = false, };
        }

        internal static NameFormatting Of(string before, string after)
        {
            return new NameFormatting
            {
                Before = before,
                After = after,
            };
        }
    }

    private NameFormatting? FormatFor(ChatType type)
    {
        if (Formats.TryGetValue(type, out var cached))
            return cached;

        var logKind = Plugin.DataManager.GetExcelSheet<LogKind>()!.GetRow((ushort)type);
        if (logKind == null)
            return null;

        var format = (SeString)logKind.Format;

        static bool IsStringParam(Payload payload, byte num)
        {
            var data = payload.Encode();
            return data.Length >= 5 && data[1] == 0x29 && data[4] == num + 1;
        }

        var firstStringParam = format.Payloads.FindIndex(payload => IsStringParam(payload, 1));
        var secondStringParam = format.Payloads.FindIndex(payload => IsStringParam(payload, 2));

        if (firstStringParam == -1 || secondStringParam == -1)
            return NameFormatting.Empty();

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

        var nameFormatting = NameFormatting.Of(string.Join("", before), string.Join("", after));
        Formats[type] = nameFormatting;

        return nameFormatting;
    }

    private class PendingMessage
    {
        internal ulong ReceiverId { get; set; }
        internal ulong ContentId { get; set; } // 0 if unknown
        internal XivChatType Type { get; set; }
        internal uint SenderId { get; set; }
        internal SeString Sender { get; set; }
        internal SeString Content { get; set; }
    }
}
