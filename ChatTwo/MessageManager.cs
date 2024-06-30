using System.Collections.Concurrent;
using System.Diagnostics;
using ChatTwo.Code;
using ChatTwo.Resources;
using ChatTwo.Util;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.GeneratedSheets;

namespace ChatTwo;

internal class MessageManager : IAsyncDisposable
{
    internal const int MessageDisplayLimit = 10_000;

    private Plugin Plugin { get; }
    internal MessageStore Store { get; }

    private Dictionary<ChatType, NameFormatting> Formats { get; } = new();
    private ulong LastContentId { get; set; }

    // Messages go into the PendingSync queue first, which will be consumed one
    // at a time in the main thread. This is to delay the async processing until
    // after we've received the content ID from the ContentIdResolver hook.
    //
    // After that, the message is enqueued in the PendingAsync queue, which will
    // be consumed in a separate thread and perform more processing (emotes,
    // URLs) as well as inserting the message into the database.
    private Queue<PendingMessage> PendingSync { get; } = new();
    private ConcurrentQueue<PendingMessage> PendingAsync { get; } = new();
    private readonly Thread PendingMessageThread;
    private readonly CancellationTokenSource PendingThreadCancellationToken = new();

    private Hook<RaptureLogModule.Delegates.AddMsgSourceEntry>? ContentIdResolverHook { get; init; }

    internal ulong CurrentContentId
    {
        get
        {
            var contentId = Plugin.ClientState.LocalContentId;
            return contentId == 0 ? LastContentId : contentId;
        }
    }

    internal unsafe MessageManager(Plugin plugin)
    {
        Plugin = plugin;

        Store = new MessageStore(DatabasePath());

        PendingMessageThread = new Thread(() => ProcessPendingMessages(PendingThreadCancellationToken.Token));
        PendingMessageThread.Start();

        ContentIdResolverHook = Plugin.GameInteropProvider.HookFromAddress<RaptureLogModule.Delegates.AddMsgSourceEntry>(RaptureLogModule.MemberFunctionPointers.AddMsgSourceEntry, ContentIdResolver);
        ContentIdResolverHook.Enable();

        Plugin.ChatGui.ChatMessageUnhandled += ChatMessage;
        Plugin.Framework.Update += OnFrameworkUpdate;
        Plugin.ClientState.Logout += Logout;
    }

    public async ValueTask DisposeAsync()
    {
        ContentIdResolverHook?.Dispose();
        Plugin.ClientState.Logout -= Logout;
        Plugin.Framework.Update -= OnFrameworkUpdate;
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

    private void OnFrameworkUpdate(IFramework framework)
    {
        var contentId = Plugin.ClientState.LocalContentId;
        if (contentId != 0)
            LastContentId = contentId;

        // Drain the PendingSync queue into the PendingAsync queue.
        while (true)
        {
            if (!PendingSync.TryDequeue(out var pending))
                return;
            PendingAsync.Enqueue(pending);
        }
    }

    private void ProcessPendingMessages(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (PendingAsync.TryDequeue(out var pendingMessage))
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

    internal void FilterAllTabs()
    {
        DateTimeOffset? since = null;
        if (!Plugin.Config.FilterIncludePreviousSessions)
            since = Plugin.GameStarted;

        var messages = Store.GetMostRecentMessages(CurrentContentId, since);

        // We store the pending messages to be added to the chat log in a
        // temporary list, and apply them all at once after filtering.
        var pendingTabs = Plugin.Config.Tabs.Select(tab => (tab, new List<Message>())).ToList();
        foreach (var message in messages)
            foreach (var (_, pendingMessages) in pendingTabs.Where(ptab => ptab.Item1.Matches(message)))
                pendingMessages.Add(message);

        // Apply the messages to the chat log in one go.
        foreach (var (tab, pendingMessages) in pendingTabs)
            tab.Messages.AddSortPrune(pendingMessages, MessageDisplayLimit);

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

    internal void FilterAllTabsAsync()
    {
        Task.Run(() =>
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                FilterAllTabs();
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Error in FilterAllTabs");
            }

            Plugin.Log.Debug($"FilterAllTabs took {stopwatch.ElapsedMilliseconds}ms");
        });
    }

    public (SeString? Sender, SeString? Message) LastMessage = (null, null);
    private void ChatMessage(XivChatType type, int timestamp, SeString sender, SeString message)
    {
        LastMessage = (sender, message);

        var pendingMessage = new PendingMessage
        {
            ReceiverId = CurrentContentId,
            ContentId = 0,
            Type = type,
            Timestamp = timestamp,
            Sender = sender,
            Content = message,
        };

        // Update colour codes.
        GlobalParametersCache.Refresh();

        // We delay messages to be handed off to the async processing thread
        // in the next tick, otherwise we can't get the content ID from the hook
        // below.
        PendingSync.Enqueue(pendingMessage);
    }

    // This hook is called immediately after receiving a message with the
    // message's content ID. If multiple messages are received in the same tick,
    // this will be called for each message immediately after ChatMessage is
    // called for each message.
    private unsafe void ContentIdResolver(RaptureLogModule* agent, ulong contentId, ulong accountId, int messageIndex, ushort worldId, ushort chatType)
    {
        ContentIdResolverHook?.Original(agent, contentId, accountId, messageIndex, worldId, chatType);
        if (PendingSync.Count == 0)
            return;

        PendingSync.Last().ContentId = contentId;
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
        internal int Timestamp { get; set; }
        internal SeString Sender { get; set; }
        internal SeString Content { get; set; }
    }
}
