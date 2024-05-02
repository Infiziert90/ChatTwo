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

internal class MessageManager : IDisposable
{
    internal const int MessageDisplayLimit = 10_000;

    private Plugin Plugin { get; }
    internal MessageStore Store { get; }

    private ConcurrentQueue<(uint, Message)> Pending { get; } = new();
    private Dictionary<ChatType, NameFormatting> Formats { get; } = new();
    private ulong LastContentId { get; set; }

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

        Plugin.ChatGui.ChatMessageUnhandled += ChatMessage;
        Plugin.Framework.Update += GetMessageInfo;
        Plugin.Framework.Update += UpdateReceiver;
        Plugin.ClientState.Logout += Logout;
    }

    public void Dispose()
    {
        Plugin.ClientState.Logout -= Logout;
        Plugin.Framework.Update -= UpdateReceiver;
        Plugin.Framework.Update -= GetMessageInfo;
        Plugin.ChatGui.ChatMessageUnhandled -= ChatMessage;

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

    private void GetMessageInfo(IFramework framework)
    {
        if (!Pending.TryDequeue(out var entry))
            return;

        var contentId = Plugin.Functions.Chat.GetContentIdForEntry(entry.Item1);
        entry.Item2.ContentId = contentId ?? 0;
        if (Plugin.Config.DatabaseBattleMessages || !entry.Item2.Code.IsBattle())
            Store.UpsertMessage(entry.Item2);
    }

    private void AddMessage(Message message, Tab? currentTab)
    {
        if (Plugin.Config.DatabaseBattleMessages || !message.Code.IsBattle())
            Store.UpsertMessage(message);

        var currentMatches = currentTab?.Matches(message) ?? false;
        foreach (var tab in Plugin.Config.Tabs)
        {
            var unread = !(tab.UnreadMode == UnreadMode.Unseen && currentTab != tab && currentMatches);

            if (tab.Matches(message))
                tab.AddMessage(message, unread);
        }
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

        if (messages.DidError)
            WrapperUtil.AddNotification(Language.LoadMessages_Error, NotificationType.Error);
    }

    public (SeString? Sender, SeString? Message) LastMessage = (null, null);
    private void ChatMessage(XivChatType type, uint senderId, SeString sender, SeString message)
    {
        var chatCode = new ChatCode((ushort)type);

        NameFormatting? formatting = null;
        if (sender.Payloads.Count > 0)
            formatting = FormatFor(chatCode.Type);

        LastMessage = (sender, message);
        var senderChunks = new List<Chunk>();
        if (formatting is { IsPresent: true })
        {
            senderChunks.Add(new TextChunk(ChunkSource.None, null, formatting.Before)
            {
                FallbackColour = chatCode.Type,
            });
            senderChunks.AddRange(ChunkUtil.ToChunks(sender, ChunkSource.Sender, chatCode.Type));
            senderChunks.Add(new TextChunk(ChunkSource.None, null, formatting.After)
            {
                FallbackColour = chatCode.Type,
            });
        }

        var messageChunks = ChunkUtil.ToChunks(message, ChunkSource.Content, chatCode.Type).ToList();

        var msg = new Message(CurrentContentId, chatCode, senderChunks, messageChunks, sender, message);
        AddMessage(msg, Plugin.ChatLogWindow.CurrentTab ?? null);

        var idx = Plugin.Functions.GetCurrentChatLogEntryIndex();
        if (idx != null)
            Pending.Enqueue((idx.Value - 1, msg));
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

        var nameFormatting = NameFormatting.Of(
            string.Join("", before),
            string.Join("", after)
        );

        Formats[type] = nameFormatting;

        return nameFormatting;
    }
}
