using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChatTwo.Code;
using ChatTwo.Util;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Chat2PartyFinderPayload = ChatTwo.Util.PartyFinderPayload;

namespace ChatTwo.Tests;

[TestClass]
[TestSubject(typeof(MessageStore))]
public class MessageStoreTest {
    // From Message.cs
    private static readonly byte[] ExtraChatChannelPayloadBytes = [0, 0x27, 18, 0x20];

    public TestContext TestContext { get; set; }

    public static string GetImportPath() {
        string[] importPaths = [
            @".\TestData",
            @"..\TestData",
            @"..\..\TestData",
            @"..\..\..\TestData",
        ];
        var importPath = importPaths.FirstOrDefault(Directory.Exists);
        if (string.IsNullOrEmpty(importPath)) {
            throw new DirectoryNotFoundException("Could not find the import path");
        }
        return importPath;
    }

    [TestMethod]
    [Timeout(5000)]
    public void StoreAndRetrieve() {
        var tempDir = Directory.CreateTempSubdirectory("ChatTwo_test_");
        var dbPath = Path.Join(tempDir.FullName, "test.db");
        TestContext.WriteLine("Using database path: " + dbPath);
        using var store = new MessageStore(dbPath);

        // Write the message.
        var input = BigMessage();
        store.UpsertMessage(input);

        // Read the message back.
        var messages = store.GetMostRecentMessages().ToList();
        Assert.AreEqual(1, messages.Count);
        AssertMessagesEqual(input, messages.First());
    }

    [TestMethod]
    [Timeout(5000)]
    public void RetrieveMultiple() {
        var tempDir = Directory.CreateTempSubdirectory("ChatTwo_test_");
        var dbPath = Path.Join(tempDir.FullName, "test.db");
        TestContext.WriteLine("Using database path: " + dbPath);
        using var store = new MessageStore(dbPath);

        // Insert 10 messages in the wrong order of date.
        var messages = new List<Message>();
        const uint receiver = 12345;
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 10; i++) {
            var message = BigMessage(true, receiver, now.AddSeconds(-i));
            TestContext.WriteLine($"Inserting message {i}: {message.Id}");
            store.UpsertMessage(message);
            messages.Add(message);
        }

        // Insert a message for a different receiver. This shouldn't be returned
        // because of the receiver filtering.
        var otherReceiverMsg = BigMessage(receiver: receiver + 1, dateTime: now.AddSeconds(1));
        TestContext.WriteLine($"Inserting other receiver message: {otherReceiverMsg.Id}");
        store.UpsertMessage(otherReceiverMsg);

        // Query the most recent 5 messages. Should return the 4 newest messages
        // from the list, as well as the different receiver message because we
        // aren't filtering.
        var outputMessages = store.GetMostRecentMessages(count: 5).ToList();
        var gotIds = outputMessages.Select(m => m.Id).ToList();
        TestContext.WriteLine($"Query 1 got IDs: {string.Join(", ", gotIds)}");
        AssertGuidsEqual(new List<Guid> {
            messages[3].Id,
            messages[2].Id,
            messages[1].Id,
            messages[0].Id,
            otherReceiverMsg.Id
        }, gotIds);

        // Query the most recent 5 messages but filter by receiver ID.
        outputMessages = store.GetMostRecentMessages(receiver: receiver, count: 5).ToList();
        gotIds = outputMessages.Select(m => m.Id).ToList();
        TestContext.WriteLine($"Query 2 got IDs: {string.Join(", ", gotIds)}");
        AssertGuidsEqual(new List<Guid> {
            messages[4].Id,
            messages[3].Id,
            messages[2].Id,
            messages[1].Id,
            messages[0].Id,
        }, gotIds);

        // Query the most recent 5 messages but only since a specific date.
        outputMessages = store.GetMostRecentMessages(receiver, since: messages[1].Date, count: 5).ToList();
        gotIds = outputMessages.Select(m => m.Id).ToList();
        TestContext.WriteLine($"Query 3 got IDs: {string.Join(", ", gotIds)}");
        AssertGuidsEqual(new List<Guid> {
            messages[1].Id,
            messages[0].Id,
        }, gotIds);
    }

    [TestMethod]
    [Timeout(5000)]
    // This test guards against the data format changing in an incompatible way.
    public void RetrieveExisting() {
        var input = BigMessage(uniqId: false);

        var dbPath = Path.Join(GetImportPath(), "existing.db");
        TestContext.WriteLine($"Using existing database: {dbPath}");
        Assert.IsTrue(File.Exists(dbPath));

        // Uncomment this section to regenerate the existing database.
        /*
        File.Delete(dbPath);
        using (var newStore = new MessageStore(dbPath)) {
            newStore.UpsertMessage(input);
        }
        */

        using var store = new MessageStore(dbPath);
        var output = store.GetMostRecentMessages().ToList();
        Assert.AreEqual(1, output.Count);
        AssertMessagesEqual(input, output[0]);
    }

    [TestMethod]
    [Timeout(30_000)]
    public void ProfileMany() {
        const int count = 20_000;

        var tempDir = Directory.CreateTempSubdirectory("ChatTwo_test_");
        var dbPath = Path.Join(tempDir.FullName, "test.db");
        TestContext.WriteLine("Using database path: " + dbPath);
        using var store = new MessageStore(dbPath);

        for (var i = 0; i < count; i++) {
            var message = BigMessage(uniqId: true);
            store.UpsertMessage(message);
        }

        var messages = store.GetMostRecentMessages(count: count).ToList();
        Assert.AreEqual(count, messages.Count);
        foreach (var message in messages) {
            // Load the message because they are lazily parsed.
            Assert.IsTrue(message.Id != Guid.Empty);
        }
    }

    internal static Message BigMessage(bool uniqId = true, uint receiver = 12345, DateTimeOffset? dateTime = null) {
        // NOTE: These values aren't valid in the game.
        // NOTE: we can't test UiForeground, UiGlow, or AutoTranslatePayload
        // because they load data from the game.
        var senderSeString = new SeStringBuilder()
            .AddText("<")
            .Add(new PlayerPayload("Player Name", 12345))
            .AddItalics("Player Name")
            .Add(RawPayload.LinkTerminator)
            .AddText(">: ")
            .Build();
        var extraChatId = Guid.Parse("03d9e6d4-dc1a-4005-bbe7-66b8c3529277");
        var contentSeString = new SeStringBuilder()
            .Add(new RawPayload(ExtraChatChannelPayloadBytes.Concat(extraChatId.ToByteArray()).ToArray()))
            .AddIcon(BitmapFontIcon.IslandSanctuary)
            .AddMapLink(1, 2, 3, 4)
            .AddText("map")
            .Add(RawPayload.LinkTerminator)
            .AddQuestLink(12345)
            .AddText("quest")
            .Add(RawPayload.LinkTerminator)
            .Add(new DalamudLinkPayload())
            .AddText("dalamud")
            .Add(RawPayload.LinkTerminator)
            .AddStatusLink(12345)
            .AddText("status")
            .Add(RawPayload.LinkTerminator)
            .AddPartyFinderLink(12345)
            .AddText("party finder")
            .Add(RawPayload.LinkTerminator)
            .Build();

        // Add Chat 2 specific payloads (that can't be serialized into the
        // SeString).
        var contentChunks = ChunkUtil.ToChunks(contentSeString, ChunkSource.Content, ChatType.Say).ToList();
        contentChunks = contentChunks.Concat([
            new TextChunk(ChunkSource.Content, new Chat2PartyFinderPayload(12345), "chat 2 party finder"),
            new TextChunk(ChunkSource.Content, new AchievementPayload(12345), "chat 2 achievement"),
            new TextChunk(ChunkSource.Content, new UriPayload(new Uri("https://dalamud.dev")), "chat 2 uri"),
        ]).ToList();

        return new Message(
            uniqId ? Guid.NewGuid() : Guid.Parse("f011343e-6a21-49e5-a6f9-238f0f1f8c2c"),
            receiver,
            54321,
            dateTime ?? DateTimeOffset.FromUnixTimeMilliseconds(1713520182440),
            new ChatCode(12345),
            ChunkUtil.ToChunks(senderSeString, ChunkSource.Sender, ChatType.Debug).ToList(),
            contentChunks,
            senderSeString,
            contentSeString,
            new SortCode(ChatType.Crafting, ChatSource.AlliancePet),
            extraChatId
        );
    }

    internal static void AssertMessagesEqual(Message input, Message output) {
        // Check basic fields.
        Assert.AreEqual(input.Id, output.Id);
        Assert.AreEqual(input.Receiver, output.Receiver);
        Assert.AreEqual(input.ContentId, output.ContentId);
        // Assert time is within 1 second
        var timeDifference = Math.Abs(input.Date.ToUniversalTime().Subtract(output.Date.ToUniversalTime()).TotalSeconds);
        Assert.IsTrue(timeDifference < 1);
        Assert.AreEqual(input.Code.Raw, output.Code.Raw);
        Assert.AreEqual($"{input.SenderSource.Encode():X}", $"{output.SenderSource.Encode():X}");
        Assert.AreEqual($"{input.ContentSource.Encode():X}", $"{output.ContentSource.Encode():X}");
        Assert.AreEqual(input.SortCode, output.SortCode);
        Assert.AreEqual(input.ExtraChatChannel, output.ExtraChatChannel);

        // Check chunks.
        AssertChunksEqual(input.Sender, output.Sender);
        AssertChunksEqual(input.Content, output.Content);
    }

    private static void AssertChunksEqual(IReadOnlyList<Chunk> inputChunks, IReadOnlyList<Chunk> outputChunks) {
        Assert.AreEqual(inputChunks.Count, outputChunks.Count);
        for (var i = 0; i < inputChunks.Count; i++) {
            var inputChunk = inputChunks[i];
            var outputChunk = outputChunks[i];
            Assert.AreEqual(inputChunk.Source, outputChunk.Source);
            switch (inputChunk.Link) {
                case AchievementPayload inputAchievementPayload:
                    Assert.AreEqual(inputAchievementPayload.Id, ((AchievementPayload) outputChunk.Link)!.Id);
                    break;
                case Chat2PartyFinderPayload inputPartyFinderPayload:
                    Assert.AreEqual(inputPartyFinderPayload.Id, ((Chat2PartyFinderPayload) outputChunk.Link)!.Id);
                    break;
                case UriPayload inputUriPayload:
                    Assert.AreEqual(inputUriPayload.Uri, ((UriPayload) outputChunk.Link)!.Uri);
                    break;
                case null:
                    Assert.IsTrue(outputChunk.Link == null);
                    break;
                default:
                    Assert.AreEqual($"{inputChunk.Link.Encode():X}", $"{outputChunk.Link!.Encode():X}");
                    break;
            }

            switch (inputChunk) {
                case TextChunk inputTextChunk:
                    var outputTextChunk = (TextChunk)outputChunk;
                    Assert.AreEqual(inputTextChunk.FallbackColour, outputTextChunk.FallbackColour);
                    Assert.AreEqual(inputTextChunk.Foreground, outputTextChunk.Foreground);
                    Assert.AreEqual(inputTextChunk.Glow, outputTextChunk.Glow);
                    Assert.AreEqual(inputTextChunk.Italic, outputTextChunk.Italic);
                    Assert.AreEqual(inputTextChunk.Content, outputTextChunk.Content);
                    break;
                case IconChunk inputIconChunk:
                    Assert.AreEqual(inputIconChunk.Icon, ((IconChunk) outputChunk).Icon);
                    break;
                default:
                    throw new Exception("Unknown chunk type");
            }
        }
    }

    private static void AssertGuidsEqual(IReadOnlyList<Guid> expected, IReadOnlyList<Guid> got) {
        Assert.AreEqual(expected.Count, got.Count);
        for (var i = 0; i < expected.Count; i++) {
            Assert.AreEqual(expected[i].ToString(), got[i].ToString());
        }
    }
}
