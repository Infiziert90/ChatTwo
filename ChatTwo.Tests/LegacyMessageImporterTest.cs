using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ChatTwo.Code;
using JetBrains.Annotations;
using LiteDB;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChatTwo.Tests;

[TestClass]
[TestSubject(typeof(LegacyMessageImporter))]
public class LegacyMessageImporterTest
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void ConvertId()
    {
        for (var i = 0; i < 1000; i++)
        {
            var originalObjectId = ObjectId.NewObjectId();
            var intermediateGuid = LegacyMessageImporter.ObjectIdToGuid(originalObjectId);
            var newObjectId = CustomGuidToObjectId(intermediateGuid);

            TestContext.WriteLine($"original:     {originalObjectId}");
            TestContext.WriteLine($"new:          {newObjectId}");
            TestContext.WriteLine($"intermediate: {intermediateGuid}");
            Assert.IsTrue(originalObjectId.Equals(newObjectId));
        }
    }

    [TestMethod]
    [Timeout(10_000)]
    public void Import()
    {
        const int count = 100;
        var tempDir = Directory.CreateTempSubdirectory("ChatTwo_test_");
        TestContext.WriteLine("Using temp path: " + tempDir);
        var liteDbPath = Path.Join(tempDir.FullName, "original.litedb");
        TestContext.WriteLine("Using original DB path: " + liteDbPath);
        var migrationDbPath = Path.Join(tempDir.FullName, "migration.litedb");
        TestContext.WriteLine("Using migration DB path: " + migrationDbPath);
        var newDbPath = Path.Join(tempDir.FullName, "new.sqlitedb");
        TestContext.WriteLine("Using new DB path: " + newDbPath);

        var expectedMessages = new List<Message>(count);
        using (var liteDatabase = LegacyMessageImporter.Connect(liteDbPath, readOnly: false))
        {
            var messagesCollection = liteDatabase.GetCollection(LegacyMessageImporter.MessagesCollection);
            var now = DateTimeOffset.UtcNow;
            for (var i = 0; i < count; i++)
            {
                var messageId = ObjectId.NewObjectId();
                var message = MessageStoreTest.BigMessage(dateTime: now.AddSeconds(-count + i));
                // Use reflection to set Id because we don't want to add a
                // setter to it and allow other code to use it.
                var guid = LegacyMessageImporter.ObjectIdToGuid(messageId);
                message.GetType().GetField("<Id>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(message, guid);
                expectedMessages.Add(message);

                messagesCollection.Insert(new BsonDocument {
                    ["_id"] = messageId,
                    ["Receiver"] = message.Receiver,
                    ["ContentId"] = message.ContentId,
                    ["Date"] = message.Date.ToUnixTimeMilliseconds(),
                    ["Code"] = BsonMapper.Global.Serialize(message.Code),
                    ["Sender"] = BsonMapper.Global.Serialize(message.Sender),
                    ["Content"] = BsonMapper.Global.Serialize(message.Content),
                    ["SenderSource"] = BsonMapper.Global.Serialize(message.SenderSource),
                    ["ContentSource"] = BsonMapper.Global.Serialize(message.ContentSource),
                    ["SortCode"] = BsonMapper.Global.Serialize(message.SortCode),
                    ["ExtraChatChannel"] = message.ExtraChatChannel,
                });
            }

            Assert.AreEqual(count, messagesCollection.Count());
        }

        var dbPath = Path.Join(tempDir.FullName, "test.db");
        using var store = new MessageStore(dbPath);

        var eligibility = LegacyMessageImporterEligibility.CheckEligibility(originalDbPath: liteDbPath, migrationDbPath: migrationDbPath);
        Assert.AreEqual(LegacyMessageImporterEligibilityStatus.Eligible, eligibility.Status);
        Assert.AreEqual("", eligibility.AdditionalIneligibilityInfo);
        Assert.AreEqual(liteDbPath, eligibility.OriginalDbPath);
        Assert.AreEqual(migrationDbPath, eligibility.MigrationDbPath);
        Assert.IsTrue(eligibility.DatabaseSizeBytes > 0);
        Assert.AreEqual(count, eligibility.MessageCount);

        var importer = eligibility.StartImport(store, noLog: true);
        while (importer.ImportComplete == null)
            System.Threading.Thread.Sleep(10);

        Assert.IsTrue(importer.ImportComplete > importer.ImportStart);
        Assert.AreEqual(count, importer.SuccessfulMessages);
        Assert.AreEqual(0, importer.FailedMessages);

        var messages = store.GetMostRecentMessages(count: count + 1).ToList();
        Assert.AreEqual(count, messages.Count);
        for (var i = 0; i < count; i++)
            MessageStoreTest.AssertMessagesEqual(expectedMessages[i], messages[i]);

        // No longer eligible.
        eligibility = LegacyMessageImporterEligibility.CheckEligibility(originalDbPath: liteDbPath, migrationDbPath: migrationDbPath);
        Assert.AreEqual(LegacyMessageImporterEligibilityStatus.IneligibleOriginalDbNotExists, eligibility.Status);
        Assert.IsTrue(eligibility.AdditionalIneligibilityInfo.Contains("Original database file"));
        Assert.AreEqual("", eligibility.OriginalDbPath);
        Assert.AreEqual("", eligibility.MigrationDbPath);
        Assert.AreEqual(0, eligibility.DatabaseSizeBytes);
        Assert.AreEqual(0, eligibility.MessageCount);
    }

    [TestMethod]
    [Timeout(10_000)]
    public void CorruptedImport()
    {
        const int count = 100;
        const int corruptedIndex = 69;
        var tempDir = Directory.CreateTempSubdirectory("ChatTwo_test_");
        TestContext.WriteLine("Using temp path: " + tempDir);
        var liteDbPath = Path.Join(tempDir.FullName, "original.litedb");
        TestContext.WriteLine("Using original DB path: " + liteDbPath);
        var migrationDbPath = Path.Join(tempDir.FullName, "migration.litedb");
        TestContext.WriteLine("Using migration DB path: " + migrationDbPath);
        var newDbPath = Path.Join(tempDir.FullName, "new.sqlitedb");
        TestContext.WriteLine("Using new DB path: " + newDbPath);

        var expectedMessages = new List<Message>(count);
        using (var liteDatabase = LegacyMessageImporter.Connect(liteDbPath, readOnly: false))
        {
            var messagesCollection = liteDatabase.GetCollection(LegacyMessageImporter.MessagesCollection);
            var now = DateTimeOffset.UtcNow;
            for (var i = 0; i < count; i++)
            {
                if (i == corruptedIndex)
                {
                    // This message will not be imported because it can't be
                    // parsed into a Message object.
                    messagesCollection.Insert(new BsonDocument
                    {
                        ["_id"] = ObjectId.NewObjectId(),
                        ["Receiver"] = 0L,
                        ["ContentId"] = 0L,
                        ["Date"] = 0L,
                        ["Code"] = BsonMapper.Global.Serialize(new ChatCode(0)),
                        ["Sender"] = BsonMapper.Global.Serialize(new List<Chunk>()),
                        ["Content"] = BsonMapper.Global.Serialize(new List<Chunk>()),
                        // These are meant to be arrays.
                        ["SenderSource"] = new BsonDocument(),
                        ["ContentSource"] = new BsonDocument(),
                        ["SortCode"] = BsonMapper.Global.Serialize(new SortCode(0)),
                        ["ExtraChatChannel"] = new Guid(),
                    });
                    continue;
                }

                var messageId = ObjectId.NewObjectId();
                var message = MessageStoreTest.BigMessage(dateTime: now.AddSeconds(-count + i));
                // Use reflection to set Id because we don't want to add a
                // setter to it and allow other code to use it.
                var guid = LegacyMessageImporter.ObjectIdToGuid(messageId);
                message.GetType().GetField("<Id>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(message, guid);
                expectedMessages.Add(message);

                messagesCollection.Insert(new BsonDocument {
                    ["_id"] = messageId,
                    ["Receiver"] = message.Receiver,
                    ["ContentId"] = message.ContentId,
                    ["Date"] = message.Date.ToUnixTimeMilliseconds(),
                    ["Code"] = BsonMapper.Global.Serialize(message.Code),
                    ["Sender"] = BsonMapper.Global.Serialize(message.Sender),
                    ["Content"] = BsonMapper.Global.Serialize(message.Content),
                    ["SenderSource"] = BsonMapper.Global.Serialize(message.SenderSource),
                    ["ContentSource"] = BsonMapper.Global.Serialize(message.ContentSource),
                    ["SortCode"] = BsonMapper.Global.Serialize(message.SortCode),
                    ["ExtraChatChannel"] = message.ExtraChatChannel,
                });
            }

            Assert.AreEqual(count, messagesCollection.Count());
        }

        var dbPath = Path.Join(tempDir.FullName, "test.db");
        using var store = new MessageStore(dbPath);

        var eligibility = LegacyMessageImporterEligibility.CheckEligibility(originalDbPath: liteDbPath, migrationDbPath: migrationDbPath);
        Assert.AreEqual(LegacyMessageImporterEligibilityStatus.Eligible, eligibility.Status);

        var importer = eligibility.StartImport(store, noLog: true);
        while (importer.ImportComplete == null)
            System.Threading.Thread.Sleep(10);

        Assert.IsTrue(importer.ImportComplete > importer.ImportStart);
        Assert.AreEqual(count - 1, importer.SuccessfulMessages);
        Assert.AreEqual(1, importer.FailedMessages);

        var messages = store.GetMostRecentMessages(count: count + 1).ToList();
        Assert.AreEqual(count - 1, messages.Count);
        for (var i = 0; i < count - 1; i++)
            MessageStoreTest.AssertMessagesEqual(expectedMessages[i], messages[i]);
    }

    /// <summary>
    /// Converts Guids created by LegacyMessageImporter.ObjectIdToGuid() back to
    /// their original ObjectId. If any other Guid is passed, the result is
    /// lossy.
    /// </summary>
    private ObjectId CustomGuidToObjectId(Guid guid)
    {
        var guidBytes = guid.ToByteArray();
        var newObjectIdBytes = new byte[12];
        Buffer.BlockCopy(guidBytes, 0, newObjectIdBytes, 0, 7);
        newObjectIdBytes[7] = guidBytes[8];
        Buffer.BlockCopy(guidBytes, 10, newObjectIdBytes, 8, 4);
        return new ObjectId(newObjectIdBytes);
    }
}
