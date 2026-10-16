using System.IO;
using System.Text.RegularExpressions;
using InventorySystem;
using NUnit.Framework;
using ShotgunAndRun.Quest;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShotgunAndRun.Core.Tests
{
    public sealed class SaveSystemTests
    {
        private string _directory;
        private SaveSystem _saveSystem;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "ShotgunAndRunSaveTests", Path.GetRandomFileName());
            _saveSystem = new SaveSystem(Path.Combine(_directory, SaveSystem.DefaultFileName));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        [Test]
        public void Load_WithoutFile_ReturnsNewProgress()
        {
            ProgressData data = _saveSystem.Load();

            Assert.That(_saveSystem.HasSave, Is.False);
            Assert.That(data.PlayerLevel, Is.EqualTo(1));
            Assert.That(data.WeaponParts.BarrelLevel, Is.EqualTo(1));
        }

        [Test]
        public void SaveThenLoad_RestoresProgress()
        {
            ProgressData saved = new ProgressData { PlayerLevel = 3, PlayerXp = 2700 };
            saved.Quests.SetStatus("SQ_MAIN_002", QuestStatus.Accepted);
            saved.Quests.SetStatus("SQ_MAIN_001", QuestStatus.Completed);
            saved.Quests.AddFlag("RandomSpawnUnlocked");
            saved.WeaponParts.BarrelLevel = 3;
            saved.AddToStorage(MaterialType.IronIngot, 12);
            saved.AddToStorage(MaterialType.IronIngot, 7);

            _saveSystem.Save(saved);
            _saveSystem.Save(saved);
            ProgressData loaded = _saveSystem.Load();

            Assert.That(loaded.PlayerLevel, Is.EqualTo(3));
            Assert.That(loaded.PlayerXp, Is.EqualTo(2700));
            Assert.That(loaded.Quests.IsAccepted("SQ_MAIN_002"), Is.True);
            Assert.That(loaded.Quests.IsCompleted("SQ_MAIN_001"), Is.True);
            Assert.That(loaded.Quests.HasFlag("RandomSpawnUnlocked"), Is.True);
            Assert.That(loaded.WeaponParts.BarrelLevel, Is.EqualTo(3));
            Assert.That(loaded.GetStoredAmount(MaterialType.IronIngot), Is.EqualTo(19));
            Assert.That(loaded.Storage[0].ItemId, Is.EqualTo(MaterialItemIds.IronIngot));
            Assert.That(File.Exists(_saveSystem.FilePath + ".tmp"), Is.False);
        }

        [Test]
        public void Load_Version1Storage_MigratesMaterialTypeToItemId()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(
                _saveSystem.FilePath,
                "{\"SaveVersion\":1,\"Storage\":[{\"Type\":0,\"Amount\":7}]}");

            ProgressData loaded = _saveSystem.Load();

            Assert.That(loaded.SaveVersion, Is.EqualTo(ProgressData.CurrentSaveVersion));
            Assert.That(loaded.GetStoredAmount(MaterialItemIds.IronIngot), Is.EqualTo(7));
        }

        [Test]
        public void Load_DuplicateItemIds_MergesAmounts()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(
                _saveSystem.FilePath,
                "{\"SaveVersion\":2,\"Storage\":[" +
                "{\"ItemId\":\"material.iron_ingot\",\"Amount\":2}," +
                "{\"ItemId\":\"material.iron_ingot\",\"Amount\":5}]}");

            ProgressData loaded = _saveSystem.Load();

            Assert.That(loaded.Storage, Has.Count.EqualTo(1));
            Assert.That(loaded.GetStoredAmount(MaterialItemIds.IronIngot), Is.EqualTo(7));
        }

        [Test]
        public void Load_MissingFields_KeepDefaults()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_saveSystem.FilePath, "{\"PlayerXp\":300}");

            ProgressData loaded = _saveSystem.Load();

            Assert.That(loaded.PlayerXp, Is.EqualTo(300));
            Assert.That(loaded.PlayerLevel, Is.EqualTo(1));
            Assert.That(loaded.WeaponParts, Is.Not.Null);
            Assert.That(loaded.Quests, Is.Not.Null);
        }

        [Test]
        public void SaveThenLoad_RestoresQuestRecords()
        {
            ProgressData saved = new ProgressData();
            saved.Quests.SetStatus("EX_Z1", QuestStatus.ReadyToTurnIn);
            saved.Quests.SetStatus("SQ_MAIN_001", QuestStatus.Completed);
            saved.Quests.Records.Find(record => record.Id == "SQ_MAIN_001").StampPending = true;

            _saveSystem.Save(saved);
            ProgressData loaded = _saveSystem.Load();

            Assert.That(loaded.Quests.GetStatus("EX_Z1"), Is.EqualTo(QuestStatus.ReadyToTurnIn));
            Assert.That(loaded.Quests.GetPendingStampIds(), Is.EqualTo(new[] { "SQ_MAIN_001" }));
        }

        [Test]
        public void Save_WritesQuestStatusAsText()
        {
            ProgressData saved = new ProgressData();
            saved.Quests.SetStatus("EX_Z1", QuestStatus.Accepted);

            _saveSystem.Save(saved);

            Assert.That(File.ReadAllText(_saveSystem.FilePath), Does.Contain("\"Status\": \"Accepted\""));
        }

        [Test]
        public void Load_UnknownQuestStatusText_FallsBackToNotAccepted()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(
                _saveSystem.FilePath,
                "{\"SaveVersion\":4,\"Quests\":{\"Records\":[" +
                "{\"Id\":\"EX_Z1\",\"Status\":\"Acepted\"}," +
                "{\"Id\":\"EX_Z2\",\"Status\":\"Accepted\",\"StampPending\":true}]}}");

            LogAssert.Expect(LogType.Warning, new Regex("EX_Z1"));
            ProgressData loaded = _saveSystem.Load();

            Assert.That(loaded.Quests.GetStatus("EX_Z1"), Is.EqualTo(QuestStatus.NotAccepted));
            Assert.That(loaded.Quests.IsAccepted("EX_Z2"), Is.True);
            Assert.That(loaded.Quests.IsStampPending("EX_Z2"), Is.False);
        }

        [Test]
        public void Load_Version3QuestLists_MigratesToRecords()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(
                _saveSystem.FilePath,
                "{\"SaveVersion\":3,\"Quests\":{" +
                "\"AcceptedQuestIds\":[\"EX_B\",\"EX_A\"]," +
                "\"ReadyQuestIds\":[\"EX_A\",\"EX_NOT_ACCEPTED\"]," +
                "\"CompletedQuestIds\":[\"SQ_MAIN_001\"]," +
                "\"PendingStampQuestIds\":[\"SQ_MAIN_001\",\"EX_B\"]," +
                "\"ZoneVisitedQuestIds\":[\"PU_Z1\"]," +
                "\"Flags\":[\"FLAG_Q1_CLEARED\"]}}");

            ProgressData loaded = _saveSystem.Load();

            Assert.That(loaded.SaveVersion, Is.EqualTo(ProgressData.CurrentSaveVersion));
            Assert.That(loaded.Quests.GetAcceptedIds(), Is.EqualTo(new[] { "EX_B", "EX_A" }));
            Assert.That(loaded.Quests.GetStatus("EX_A"), Is.EqualTo(QuestStatus.ReadyToTurnIn));
            Assert.That(loaded.Quests.GetStatus("EX_B"), Is.EqualTo(QuestStatus.Accepted));
            Assert.That(loaded.Quests.GetStatus("EX_NOT_ACCEPTED"), Is.EqualTo(QuestStatus.NotAccepted));
            Assert.That(loaded.Quests.IsCompleted("SQ_MAIN_001"), Is.True);
            Assert.That(loaded.Quests.GetPendingStampIds(), Is.EqualTo(new[] { "SQ_MAIN_001" }));
            Assert.That(loaded.Quests.IsZoneVisited("PU_Z1"), Is.True);
            Assert.That(loaded.Quests.HasFlag("FLAG_Q1_CLEARED"), Is.True);
        }

        [Test]
        public void Load_Version2QuestChain_StartsWithEmptyQuests()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(
                _saveSystem.FilePath,
                "{\"SaveVersion\":2,\"PlayerXp\":300,\"QuestIndex\":4,\"QuestConditionMet\":true}");

            ProgressData loaded = _saveSystem.Load();

            Assert.That(loaded.SaveVersion, Is.EqualTo(ProgressData.CurrentSaveVersion));
            Assert.That(loaded.PlayerXp, Is.EqualTo(300));
            Assert.That(loaded.Quests.Records, Is.Empty);
        }

        [Test]
        public void Load_CompletedQuestStillAccepted_RemovesFromAccepted()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(
                _saveSystem.FilePath,
                "{\"SaveVersion\":3,\"Quests\":{" +
                "\"AcceptedQuestIds\":[\"SQ_MAIN_001\",\"SQ_MAIN_001\"]," +
                "\"CompletedQuestIds\":[\"SQ_MAIN_001\"]}}");

            ProgressData loaded = _saveSystem.Load();

            Assert.That(loaded.Quests.GetAcceptedIds(), Is.Empty);
            Assert.That(loaded.Quests.GetStatus("SQ_MAIN_001"), Is.EqualTo(QuestStatus.Completed));
        }

        [Test]
        public void Load_CorruptedFile_ReturnsNewProgress()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_saveSystem.FilePath, "not json");

            ProgressData loaded = _saveSystem.Load();

            Assert.That(loaded.PlayerXp, Is.EqualTo(0));
        }

        [Test]
        public void SaveThenLoad_RestoresConsumableStorage()
        {
            ProgressData saved = new ProgressData();
            saved.EnsureConsumableStorageCapacity();
            saved.ConsumableStorage[0] = new QuickSlotRecord("SmallKit", 2);
            saved.ConsumableStorage[3] = new QuickSlotRecord("Adrenaline", 1);

            _saveSystem.Save(saved);
            ProgressData loaded = _saveSystem.Load();

            Assert.That(loaded.ConsumableStorage, Has.Count.GreaterThanOrEqualTo(10));
            Assert.That(loaded.ConsumableStorage[0].ConsumableTypeName, Is.EqualTo("SmallKit"));
            Assert.That(loaded.ConsumableStorage[0].Amount, Is.EqualTo(2));
            Assert.That(loaded.ConsumableStorage[3].ConsumableTypeName, Is.EqualTo("Adrenaline"));
            Assert.That(loaded.ConsumableStorage[3].Amount, Is.EqualTo(1));
        }

        [Test]
        public void Save_WhileHeld_DoesNotWriteFile()
        {
            _saveSystem.IsHeld = true;

            _saveSystem.Save(new ProgressData { PlayerXp = 100 });

            Assert.That(_saveSystem.HasSave, Is.False);
        }

        [Test]
        public void Save_WhileHeld_KeepsPreviousSave()
        {
            _saveSystem.Save(new ProgressData { PlayerXp = 100 });
            _saveSystem.IsHeld = true;

            _saveSystem.Save(new ProgressData { PlayerXp = 900 });

            Assert.That(_saveSystem.Load().PlayerXp, Is.EqualTo(100));
        }

        [Test]
        public void Save_AfterRelease_WritesFile()
        {
            _saveSystem.IsHeld = true;
            _saveSystem.Save(new ProgressData { PlayerXp = 100 });
            _saveSystem.IsHeld = false;

            _saveSystem.Save(new ProgressData { PlayerXp = 900 });

            Assert.That(_saveSystem.Load().PlayerXp, Is.EqualTo(900));
        }
    }
}
