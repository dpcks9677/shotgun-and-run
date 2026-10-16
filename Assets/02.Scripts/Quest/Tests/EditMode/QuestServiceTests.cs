using System;
using System.Collections.Generic;
using InventorySystem;
using NUnit.Framework;
using ShotgunAndRun.Loot;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShotgunAndRun.Quest.Tests
{
    public sealed class QuestServiceTests
    {
        private const string WhetstoneId = "quest.whetstone";
        private const string ElenaFreedFlag = "H2_ELENA_FREED";
        private const string RandomSpawnFlag = "RandomSpawnUnlocked";

        private readonly List<Object> _created = new List<Object>();

        private ItemData _whetstone;
        private ItemData _ironIngot;

        [SetUp]
        public void SetUp()
        {
            _whetstone = CreateItem(WhetstoneId, ItemType.Quest);
            _ironIngot = CreateItem(MaterialItemIds.IronIngot, ItemType.Material);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        [Test]
        public void TryAccept_AvailableQuest_BecomesAccepted()
        {
            QuestDefinitionSO quest = CreateWhetstoneQuest("SQ_MAIN_001");
            QuestProgress progress = new QuestProgress();
            QuestService service = CreateService(progress, quest);

            Assert.That(service.GetState(quest), Is.EqualTo(QuestState.Available));
            Assert.That(service.TryAccept(quest), Is.True);
            Assert.That(service.GetState(quest), Is.EqualTo(QuestState.Accepted));
            Assert.That(progress.GetAcceptedIds(), Is.EqualTo(new[] { "SQ_MAIN_001" }));
        }

        [Test]
        public void Constructor_FillsRecordsInCatalogOrder()
        {
            QuestDefinitionSO first = CreateWhetstoneQuest("SQ_MAIN_001");
            QuestDefinitionSO second = CreateExplorationQuest("EX_Z1", "Z1");
            QuestProgress progress = new QuestProgress();
            progress.SetStatus("EX_Z1", QuestStatus.Accepted);

            CreateService(progress, first, second);

            Assert.That(progress.Records.ConvertAll(record => record.Id), Is.EqualTo(new[] { "SQ_MAIN_001", "EX_Z1" }));
            Assert.That(progress.GetStatus("SQ_MAIN_001"), Is.EqualTo(QuestStatus.NotAccepted));
            Assert.That(progress.GetStatus("EX_Z1"), Is.EqualTo(QuestStatus.Accepted));
        }

        [Test]
        public void TryAccept_PrerequisiteNotCompleted_IsLocked()
        {
            QuestDefinitionSO first = CreateWhetstoneQuest("SQ_MAIN_001");
            QuestDefinitionSO second = CreateFlagQuest("SQ_MAIN_002", QuestType.MainStory, ElenaFreedFlag,
                so => SetObjects(so, "_prerequisiteQuests", first));
            QuestProgress progress = new QuestProgress();
            QuestService service = CreateService(progress, first, second);

            Assert.That(service.GetState(second), Is.EqualTo(QuestState.Locked));
            Assert.That(service.TryAccept(second), Is.False);

            progress.SetStatus("SQ_MAIN_001", QuestStatus.Completed);

            Assert.That(service.GetState(second), Is.EqualTo(QuestState.Available));
        }

        [Test]
        public void TryAccept_PrerequisiteMinCount_NeedsOnlyThatMany()
        {
            QuestDefinitionSO a = CreateExplorationQuest("EX_Z1", "Z1");
            QuestDefinitionSO b = CreateExplorationQuest("EX_Z2", "Z2");
            QuestDefinitionSO c = CreateExplorationQuest("EX_Z3", "Z3");
            QuestDefinitionSO highRisk = CreateExplorationQuest("EX_Z5", "Z5", so =>
            {
                SetObjects(so, "_prerequisiteQuests", a, b, c);
                so.FindProperty("_prerequisiteMinCount").intValue = 2;
            });
            QuestProgress progress = new QuestProgress();
            QuestService service = CreateService(progress, a, b, c, highRisk);

            progress.SetStatus("EX_Z1", QuestStatus.Completed);
            Assert.That(service.GetState(highRisk), Is.EqualTo(QuestState.Locked));

            progress.SetStatus("EX_Z3", QuestStatus.Completed);
            Assert.That(service.GetState(highRisk), Is.EqualTo(QuestState.Available));
        }

        [Test]
        public void TryAccept_RequiredFlagMissing_IsLocked()
        {
            QuestDefinitionSO quest = CreateExplorationQuest("EX_Z2", "Z2",
                so => SetStrings(so, "_requiredFlags", RandomSpawnFlag));
            QuestProgress progress = new QuestProgress();
            QuestService service = CreateService(progress, quest);

            Assert.That(service.GetState(quest), Is.EqualTo(QuestState.Locked));

            progress.AddFlag(RandomSpawnFlag);

            Assert.That(service.GetState(quest), Is.EqualTo(QuestState.Available));
        }

        [Test]
        public void TryAccept_StorySlotHoldsOne()
        {
            QuestDefinitionSO first = CreateWhetstoneQuest("SQ_A");
            QuestDefinitionSO second = CreateWhetstoneQuest("SQ_B");
            QuestService service = CreateService(new QuestProgress(), first, second);

            Assert.That(service.TryAccept(first), Is.True);
            Assert.That(service.AcceptedCount, Is.EqualTo(1));
            Assert.That(service.TryAccept(second), Is.False);
        }

        [Test]
        public void TryAccept_TotalLimitIncludesStory()
        {
            QuestDefinitionSO story = CreateWhetstoneQuest("SQ_MAIN_001");
            QuestDefinitionSO[] requests =
            {
                CreateExplorationQuest("EX_Z1", "Z1"),
                CreateExplorationQuest("EX_Z2", "Z2"),
                CreateExplorationQuest("EX_Z3", "Z3")
            };
            QuestService service = CreateService(new QuestProgress(), story, requests[0], requests[1], requests[2]);

            Assert.That(service.TryAccept(story), Is.True);
            Assert.That(service.TryAccept(requests[0]), Is.True);
            Assert.That(service.TryAccept(requests[1]), Is.True);
            Assert.That(service.AcceptedCount, Is.EqualTo(QuestService.MaxAcceptedCount));
            Assert.That(service.TryAccept(requests[2]), Is.False);
        }

        [Test]
        public void TryAccept_StoryIgnoresTotalLimit()
        {
            QuestDefinitionSO story = CreateWhetstoneQuest("SQ_MAIN_001");
            QuestDefinitionSO[] requests =
            {
                CreateExplorationQuest("EX_Z1", "Z1"),
                CreateExplorationQuest("EX_Z2", "Z2"),
                CreateExplorationQuest("EX_Z3", "Z3"),
                CreateExplorationQuest("EX_Z4", "Z4")
            };
            QuestService service = CreateService(new QuestProgress(), story, requests[0], requests[1], requests[2],
                requests[3]);
            service.TryAccept(requests[0]);
            service.TryAccept(requests[1]);
            service.TryAccept(requests[2]);

            Assert.That(service.TryAccept(story), Is.True);
            Assert.That(service.AcceptedCount, Is.EqualTo(4));
            Assert.That(service.TryAccept(requests[3]), Is.False);
        }

        [Test]
        public void TryAccept_RequestSlotsHoldThree()
        {
            QuestDefinitionSO[] quests =
            {
                CreateExplorationQuest("EX_Z1", "Z1"),
                CreateExplorationQuest("EX_Z2", "Z2"),
                CreateExplorationQuest("EX_Z3", "Z3"),
                CreateExplorationQuest("EX_Z4", "Z4")
            };
            QuestService service = CreateService(new QuestProgress(), quests);

            Assert.That(service.TryAccept(quests[0]), Is.True);
            Assert.That(service.TryAccept(quests[1]), Is.True);
            Assert.That(service.TryAccept(quests[2]), Is.True);
            Assert.That(service.TryAccept(quests[3]), Is.False);
        }

        [Test]
        public void TryAccept_DuringRun_Fails()
        {
            QuestDefinitionSO quest = CreateExplorationQuest("EX_Z1", "Z1");
            QuestService service = CreateService(new QuestProgress(), quest);

            service.BeginRun();

            Assert.That(service.TryAccept(quest), Is.False);
        }

        [Test]
        public void TryAbandon_RequestOnly()
        {
            QuestDefinitionSO story = CreateWhetstoneQuest("SQ_MAIN_001");
            QuestDefinitionSO request = CreateExplorationQuest("EX_Z2", "Z2");
            QuestProgress progress = new QuestProgress();
            QuestService service = CreateService(progress, story, request);
            service.TryAccept(story);
            service.TryAccept(request);

            Assert.That(service.TryAbandon(story), Is.False);
            Assert.That(service.TryAbandon(request), Is.True);
            Assert.That(service.GetState(request), Is.EqualTo(QuestState.Available));
            Assert.That(progress.GetAcceptedIds(), Is.EqualTo(new[] { "SQ_MAIN_001" }));
        }

        [Test]
        public void CompleteRun_CarryingQuestItem_BecomesReadyWithoutRewards()
        {
            QuestDefinitionSO quest = CreateWhetstoneQuest("SQ_MAIN_001", so =>
            {
                so.FindProperty("_rewardXp").intValue = 300;
                SetRewards(so, (_ironIngot, 1));
                SetStrings(so, "_unlockFlags", "WeaponUpgradeUnlocked");
            });
            QuestProgress progress = new QuestProgress();
            QuestService service = CreateService(progress, quest);
            service.TryAccept(quest);
            int changedCount = 0;
            service.Changed += () => changedCount++;

            service.BeginRun();
            QuestReadyReport report = service.CompleteRun(Carry(WhetstoneId, 1), new[] { "Z1" });

            Assert.That(report.ReadyQuests, Is.EqualTo(new[] { quest }));
            Assert.That(report.ConsumedItemIds, Is.EqualTo(new[] { WhetstoneId }));
            Assert.That(service.GetState(quest), Is.EqualTo(QuestState.ReadyToTurnIn));
            Assert.That(progress.GetAcceptedIds(), Is.EqualTo(new[] { "SQ_MAIN_001" }));
            Assert.That(progress.GetStatus("SQ_MAIN_001"), Is.EqualTo(QuestStatus.ReadyToTurnIn));
            Assert.That(progress.HasFlag("WeaponUpgradeUnlocked"), Is.True);
            Assert.That(service.IsRunActive, Is.False);
            Assert.That(changedCount, Is.EqualTo(1));
        }

        [Test]
        public void CompleteRun_WithoutQuestItem_KeepsAccepted()
        {
            QuestDefinitionSO quest = CreateWhetstoneQuest("SQ_MAIN_001");
            QuestService service = CreateService(new QuestProgress(), quest);
            service.TryAccept(quest);

            service.BeginRun();
            QuestReadyReport report = service.CompleteRun(Carry(MaterialItemIds.IronIngot, 3), new[] { "Z1" });

            Assert.That(report.HasReadyQuest, Is.False);
            Assert.That(report.ConsumedItemIds, Is.Empty);
            Assert.That(service.GetState(quest), Is.EqualTo(QuestState.Accepted));
        }

        [Test]
        public void CompleteRun_RunFlagRaised_Completes()
        {
            QuestDefinitionSO quest = CreateFlagQuest("SQ_MAIN_002", QuestType.MainStory, ElenaFreedFlag,
                so => SetStrings(so, "_unlockFlags", RandomSpawnFlag));
            QuestProgress progress = new QuestProgress();
            QuestService service = CreateService(progress, quest);
            service.TryAccept(quest);

            service.BeginRun();
            service.RaiseRunFlag(ElenaFreedFlag);
            QuestReadyReport report = service.CompleteRun(null, null);

            Assert.That(report.ReadyQuests, Is.EqualTo(new[] { quest }));
            Assert.That(progress.HasFlag(RandomSpawnFlag), Is.True);
        }

        [Test]
        public void FailRun_DiscardsRunFlags_KeepsAccepted()
        {
            QuestDefinitionSO quest = CreateFlagQuest("SQ_MAIN_002", QuestType.MainStory, ElenaFreedFlag);
            QuestService service = CreateService(new QuestProgress(), quest);
            service.TryAccept(quest);

            service.BeginRun();
            service.RaiseRunFlag(ElenaFreedFlag);
            service.FailRun();

            Assert.That(service.GetState(quest), Is.EqualTo(QuestState.Accepted));

            service.BeginRun();
            QuestReadyReport report = service.CompleteRun(null, null);

            Assert.That(report.HasReadyQuest, Is.False);
            Assert.That(service.GetState(quest), Is.EqualTo(QuestState.Accepted));
        }

        [Test]
        public void CompleteRun_WithoutBeginRun_ReturnsEmpty()
        {
            QuestDefinitionSO quest = CreateWhetstoneQuest("SQ_MAIN_001");
            QuestService service = CreateService(new QuestProgress(), quest);
            service.TryAccept(quest);

            QuestReadyReport report = service.CompleteRun(Carry(WhetstoneId, 1), null);

            Assert.That(report.HasReadyQuest, Is.False);
            Assert.That(service.GetState(quest), Is.EqualTo(QuestState.Accepted));
        }

        [Test]
        public void CompleteRun_OneItemCompletesOneQuest()
        {
            QuestDefinitionSO first = CreateExplorationQuest("EX_A", "Z1");
            QuestDefinitionSO second = CreateExplorationQuest("EX_B", "Z1");
            QuestService service = CreateService(new QuestProgress(), first, second);
            service.TryAccept(first);
            service.TryAccept(second);

            service.BeginRun();
            QuestReadyReport report = service.CompleteRun(Carry(WhetstoneId, 1), null);

            Assert.That(report.ReadyQuests, Is.EqualTo(new[] { first }));
            Assert.That(service.GetState(second), Is.EqualTo(QuestState.Accepted));
        }

        [Test]
        public void ZonePurification_UnlocksAfterLaterVisitAndExtraction()
        {
            QuestDefinitionSO exploration = CreateExplorationQuest("EX_Z1", "Z1");
            QuestDefinitionSO purification = CreateFlagQuest("PU_Z1_1", QuestType.ZonePurification,
                "Z1_PURIFY_1_CLEARED",
                so =>
                {
                    so.FindProperty("_zoneA").stringValue = "Z1";
                    so.FindProperty("_requiresZoneVisitAfterUnlock").boolValue = true;
                    SetObjects(so, "_prerequisiteQuests", exploration);
                });
            QuestService service = CreateService(new QuestProgress(), exploration, purification);
            service.TryAccept(exploration);

            // 초반 탐사가 완료 가능이 된 런의 방문은 인정하지 않음. 보고해야 선행 완료로 인정
            service.BeginRun();
            service.CompleteRun(Carry(WhetstoneId, 1), new[] { "Z1" });
            Assert.That(service.GetState(exploration), Is.EqualTo(QuestState.ReadyToTurnIn));
            Assert.That(service.GetState(purification), Is.EqualTo(QuestState.Locked));
            Assert.That(service.TryTurnIn(exploration, out _, out _), Is.True);
            Assert.That(service.GetState(exploration), Is.EqualTo(QuestState.Completed));
            Assert.That(service.GetState(purification), Is.EqualTo(QuestState.Locked));

            // 다른 존만 방문
            service.BeginRun();
            service.CompleteRun(null, new[] { "Z2" });
            Assert.That(service.GetState(purification), Is.EqualTo(QuestState.Locked));

            // 방문했지만 사망
            service.BeginRun();
            service.FailRun();
            Assert.That(service.GetState(purification), Is.EqualTo(QuestState.Locked));

            service.BeginRun();
            service.CompleteRun(null, new[] { "Z1" });
            Assert.That(service.GetState(purification), Is.EqualTo(QuestState.Available));
        }

        [Test]
        public void CompleteRun_ReadyQuest_UnlockFlagsOnImmediately()
        {
            QuestDefinitionSO quest = CreateWhetstoneQuest("SQ_MAIN_001",
                so => SetStrings(so, "_unlockFlags", "FLAG_Q1_CLEARED"));
            QuestProgress progress = new QuestProgress();
            QuestService service = CreateService(progress, quest);
            service.TryAccept(quest);

            service.BeginRun();
            service.CompleteRun(Carry(WhetstoneId, 1), null);

            Assert.That(progress.HasFlag("FLAG_Q1_CLEARED"), Is.True);
            Assert.That(service.GetState(quest), Is.EqualTo(QuestState.ReadyToTurnIn));
        }

        [Test]
        public void TryAbandon_ReadyQuest_FailsAndKeepsSlot()
        {
            QuestDefinitionSO quest = CreateExplorationQuest("EX_Z1", "Z1");
            QuestService service = CreateService(new QuestProgress(), quest);
            service.TryAccept(quest);
            service.BeginRun();
            service.CompleteRun(Carry(WhetstoneId, 1), null);

            Assert.That(service.TryAbandon(quest), Is.False);
            Assert.That(service.GetState(quest), Is.EqualTo(QuestState.ReadyToTurnIn));
            Assert.That(service.AcceptedCount, Is.EqualTo(1));
        }

        [Test]
        public void ReadyQuest_ExcludedFromLootRequestsAndFixedSpawn()
        {
            QuestDefinitionSO quest = CreateWhetstoneQuest("SQ_MAIN_001",
                so => so.FindProperty("_fixedSpawnId").stringValue = "E1");
            QuestService service = CreateService(new QuestProgress(), quest);
            service.TryAccept(quest);
            Assert.That(service.GetLootRequests(), Has.Count.EqualTo(1));
            Assert.That(service.TryGetFixedSpawnId(out _), Is.True);

            service.BeginRun();
            service.CompleteRun(Carry(WhetstoneId, 1), null);

            Assert.That(service.GetLootRequests(), Is.Empty);
            Assert.That(service.TryGetFixedSpawnId(out _), Is.False);
        }

        [Test]
        public void TryTurnIn_ReadyQuest_CompletesAndReturnsRewards()
        {
            QuestDefinitionSO quest = CreateWhetstoneQuest("SQ_MAIN_001", so =>
            {
                so.FindProperty("_rewardXp").intValue = 300;
                SetRewards(so, (_ironIngot, 2));
            });
            QuestProgress progress = new QuestProgress();
            QuestService service = CreateService(progress, quest);
            service.TryAccept(quest);
            service.BeginRun();
            service.CompleteRun(Carry(WhetstoneId, 1), null);
            int changedCount = 0;
            service.Changed += () => changedCount++;

            bool result = service.TryTurnIn(quest, out int xp, out List<QuestRewardItem> items);

            Assert.That(result, Is.True);
            Assert.That(xp, Is.EqualTo(300));
            Assert.That(items, Has.Count.EqualTo(1));
            Assert.That(items[0].Item, Is.SameAs(_ironIngot));
            Assert.That(items[0].Amount, Is.EqualTo(2));
            Assert.That(service.GetState(quest), Is.EqualTo(QuestState.Completed));
            Assert.That(progress.GetAcceptedIds(), Is.Empty);
            Assert.That(progress.IsReady("SQ_MAIN_001"), Is.False);
            Assert.That(service.IsStampPending(quest), Is.True);
            Assert.That(changedCount, Is.EqualTo(1));
        }

        [Test]
        public void TryTurnIn_NotReady_Fails()
        {
            QuestDefinitionSO quest = CreateWhetstoneQuest("SQ_MAIN_001");
            QuestService service = CreateService(new QuestProgress(), quest);

            Assert.That(service.TryTurnIn(quest, out int xp, out List<QuestRewardItem> items), Is.False);
            Assert.That(xp, Is.Zero);
            Assert.That(items, Is.Empty);

            service.TryAccept(quest);
            Assert.That(service.TryTurnIn(quest, out _, out _), Is.False);
            Assert.That(service.GetState(quest), Is.EqualTo(QuestState.Accepted));
        }

        [Test]
        public void TryTurnIn_DuringRun_Fails()
        {
            QuestDefinitionSO quest = CreateWhetstoneQuest("SQ_MAIN_001");
            QuestService service = CreateService(new QuestProgress(), quest);
            service.TryAccept(quest);
            service.BeginRun();
            service.CompleteRun(Carry(WhetstoneId, 1), null);
            service.BeginRun();

            Assert.That(service.TryTurnIn(quest, out _, out _), Is.False);
            Assert.That(service.GetState(quest), Is.EqualTo(QuestState.ReadyToTurnIn));
        }

        [Test]
        public void GetBoardQuests_OrdersStoryStampRequestsAvailable()
        {
            QuestDefinitionSO story = CreateWhetstoneQuest("SQ_MAIN_001");
            QuestDefinitionSO lockedStory = CreateFlagQuest("SQ_MAIN_002", QuestType.MainStory, ElenaFreedFlag,
                so => SetObjects(so, "_prerequisiteQuests", story));
            QuestDefinitionSO stamped = CreateExplorationQuest("EX_STAMP", "Z1");
            QuestDefinitionSO acceptedFirst = CreateExplorationQuest("EX_ACC1", "Z2");
            QuestDefinitionSO acceptedSecond = CreateExplorationQuest("EX_ACC2", "Z3");
            QuestDefinitionSO availableA = CreateExplorationQuest("EX_AV_A", "Z4");
            QuestDefinitionSO availableB = CreateExplorationQuest("EX_AV_B", "Z5");
            QuestDefinitionSO lockedRequest = CreateExplorationQuest("EX_LOCKED", "Z6",
                so => SetStrings(so, "_requiredFlags", RandomSpawnFlag));
            QuestProgress progress = new QuestProgress();
            // 카탈로그 순서를 일부러 뒤섞어 정렬 규칙을 확인
            QuestService service = CreateService(progress, availableA, acceptedSecond, lockedRequest, stamped,
                availableB, lockedStory, acceptedFirst, story);
            progress.SetStatus("EX_STAMP", QuestStatus.Completed);
            progress.Records.Find(record => record.Id == "EX_STAMP").StampPending = true;
            service.TryAccept(acceptedFirst);
            service.TryAccept(acceptedSecond);
            service.TryAccept(story);

            List<QuestDefinitionSO> board = service.GetBoardQuests();

            Assert.That(board,
                Is.EqualTo(new[] { story, stamped, acceptedFirst, acceptedSecond, availableA, availableB }));
        }

        [Test]
        public void GetBoardQuests_StoryStampComesBeforeAcceptedStory()
        {
            QuestDefinitionSO done = CreateWhetstoneQuest("SQ_MAIN_001");
            QuestDefinitionSO next = CreateFlagQuest("SQ_MAIN_002", QuestType.MainStory, ElenaFreedFlag);
            QuestProgress progress = new QuestProgress();
            QuestService service = CreateService(progress, done, next);
            progress.SetStatus("SQ_MAIN_001", QuestStatus.Completed);
            progress.Records.Find(record => record.Id == "SQ_MAIN_001").StampPending = true;
            service.TryAccept(next);

            Assert.That(service.GetBoardQuests(), Is.EqualTo(new[] { done, next }));

            service.AcknowledgeStamps();

            Assert.That(service.GetBoardQuests(), Is.EqualTo(new[] { next }));
        }

        [Test]
        public void TryTurnIn_StoryWithoutNpcReport_CompletesAtBoard()
        {
            QuestDefinitionSO story = CreateFlagQuest("SQ_MAIN_002", QuestType.MainStory, ElenaFreedFlag);
            QuestService service = CreateService(new QuestProgress(), story);
            service.TryAccept(story);
            service.BeginRun();
            service.RaiseRunFlag(ElenaFreedFlag);
            service.CompleteRun(Carry(WhetstoneId, 0), null);

            Assert.That(service.GetState(story), Is.EqualTo(QuestState.ReadyToTurnIn));
            Assert.That(service.TryTurnIn(story, out _, out _), Is.True);
        }

        [Test]
        public void AcknowledgeStamps_ClearsPendingAndRaisesChanged()
        {
            QuestDefinitionSO quest = CreateWhetstoneQuest("SQ_MAIN_001");
            QuestProgress progress = new QuestProgress();
            QuestService service = CreateService(progress, quest);
            service.TryAccept(quest);
            service.BeginRun();
            service.CompleteRun(Carry(WhetstoneId, 1), null);
            service.TryTurnIn(quest, out _, out _);
            int changedCount = 0;
            service.Changed += () => changedCount++;

            service.AcknowledgeStamps();
            service.AcknowledgeStamps();

            Assert.That(progress.GetPendingStampIds(), Is.Empty);
            Assert.That(service.IsStampPending(quest), Is.False);
            Assert.That(changedCount, Is.EqualTo(1));
        }

        [Test]
        public void Normalize_MergesDuplicateRecordsAndClearsStampOnUnfinished()
        {
            QuestProgress progress = new QuestProgress();
            progress.Records.Add(new QuestRecord { Id = "A", Status = "Accepted", AcceptedOrder = 2 });
            progress.Records.Add(new QuestRecord { Id = "A", Status = "ReadyToTurnIn", StampPending = true });
            progress.Records.Add(new QuestRecord { Id = "B", Status = "Accepted", AcceptedOrder = 1 });
            progress.Records.Add(new QuestRecord { Id = "Y", Status = "Completed", StampPending = true, AcceptedOrder = 5 });

            progress.Normalize();

            Assert.That(progress.Records.ConvertAll(record => record.Id), Is.EqualTo(new[] { "A", "B", "Y" }));
            Assert.That(progress.GetStatus("A"), Is.EqualTo(QuestStatus.ReadyToTurnIn));
            Assert.That(progress.GetAcceptedIds(), Is.EqualTo(new[] { "B", "A" }));
            Assert.That(progress.GetPendingStampIds(), Is.EqualTo(new[] { "Y" }));
            Assert.That(progress.Records[2].AcceptedOrder, Is.EqualTo(0));
        }

        [Test]
        public void GetLootRequests_MapsItemSourceToLootKind()
        {
            QuestDefinitionSO certificate = CreateExplorationQuest("EX_Z1", "Z1");
            QuestDefinitionSO treasure = CreateQuest("TR_Z2_Z3", QuestType.TreasureHunt, so =>
            {
                SetItem(so, QuestItemSource.Treasure);
                so.FindProperty("_zoneA").stringValue = "Z2";
                so.FindProperty("_zoneB").stringValue = "Z3";
            });
            QuestDefinitionSO recovery = CreateQuest("RC_Z4", QuestType.Recovery, so =>
            {
                SetItem(so, QuestItemSource.RecoveryChest);
                so.FindProperty("_zoneA").stringValue = "Z4";
            });
            QuestDefinitionSO notAccepted = CreateExplorationQuest("EX_Z6", "Z6");
            QuestService service = CreateService(new QuestProgress(), certificate, treasure, recovery, notAccepted);
            service.TryAccept(certificate);
            service.TryAccept(treasure);
            service.TryAccept(recovery);

            List<QuestLootRequest> requests = service.GetLootRequests();

            Assert.That(requests, Has.Count.EqualTo(3));
            AssertRequest(requests[0], QuestLootKind.ZoneCertificate, "Z1", null);
            AssertRequest(requests[1], QuestLootKind.Treasure, "Z2", "Z3");
            AssertRequest(requests[2], QuestLootKind.Recovery, "Z4", null);
        }

        [Test]
        public void TryGetFixedSpawnId_PrefersStoryQuest()
        {
            QuestDefinitionSO request = CreateExplorationQuest("EX_Z9", "Z9",
                so => so.FindProperty("_fixedSpawnId").stringValue = "E3");
            QuestDefinitionSO story = CreateWhetstoneQuest("SQ_MAIN_001",
                so => so.FindProperty("_fixedSpawnId").stringValue = "E1");
            QuestService service = CreateService(new QuestProgress(), request, story);

            Assert.That(service.TryGetFixedSpawnId(out _), Is.False);

            service.TryAccept(request);
            service.TryAccept(story);

            Assert.That(service.TryGetFixedSpawnId(out string spawnId), Is.True);
            Assert.That(spawnId, Is.EqualTo("E1"));
        }

        [Test]
        public void FindProblems_ReportsMissingItemSource()
        {
            QuestDefinitionSO valid = CreateWhetstoneQuest("SQ_MAIN_001");
            QuestDefinitionSO invalid = CreateQuest("SQ_BAD", QuestType.MainStory,
                so => so.FindProperty("_questItem").objectReferenceValue = _whetstone);

            Assert.That(valid.FindProblems(), Is.Empty);
            Assert.That(invalid.FindProblems(), Has.Some.Contains("Item Source"));
        }

        private QuestService CreateService(QuestProgress progress, params QuestDefinitionSO[] quests)
        {
            QuestCatalogSO catalog = Create<QuestCatalogSO>("QuestCatalog");
            SerializedObject so = new SerializedObject(catalog);
            SetObjects(so, "_quests", quests);
            so.ApplyModifiedPropertiesWithoutUndo();
            return new QuestService(progress, catalog);
        }

        // Q1 숫돌처럼 Z1 존 상자의 퀘스트 아이템을 들고 탈출하는 스토리 퀘스트
        private QuestDefinitionSO CreateWhetstoneQuest(string id, Action<SerializedObject> configure = null)
        {
            return CreateQuest(id, QuestType.MainStory, so =>
            {
                SetItem(so, QuestItemSource.ZoneChest);
                so.FindProperty("_zoneA").stringValue = "Z1";
                configure?.Invoke(so);
            });
        }

        private QuestDefinitionSO CreateExplorationQuest(string id, string zone,
            Action<SerializedObject> configure = null)
        {
            return CreateQuest(id, QuestType.EarlyExploration, so =>
            {
                SetItem(so, QuestItemSource.ZoneChest);
                so.FindProperty("_zoneA").stringValue = zone;
                configure?.Invoke(so);
            });
        }

        private QuestDefinitionSO CreateFlagQuest(
            string id,
            QuestType type,
            string runFlag,
            Action<SerializedObject> configure = null)
        {
            return CreateQuest(id, type, so =>
            {
                SetStrings(so, "_requiredRunFlags", runFlag);
                configure?.Invoke(so);
            });
        }

        private QuestDefinitionSO CreateQuest(string id, QuestType type, Action<SerializedObject> configure)
        {
            QuestDefinitionSO quest = Create<QuestDefinitionSO>(id);
            SerializedObject so = new SerializedObject(quest);
            so.FindProperty("_id").stringValue = id;
            so.FindProperty("_type").intValue = (int)type;
            configure?.Invoke(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            return quest;
        }

        private ItemData CreateItem(string id, ItemType itemType)
        {
            ItemData item = Create<ItemData>(id);
            SerializedObject so = new SerializedObject(item);
            so.FindProperty("_id").stringValue = id;
            so.FindProperty("_itemType").intValue = (int)itemType;
            so.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        private T Create<T>(string objectName) where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            instance.name = objectName;
            _created.Add(instance);
            return instance;
        }

        private void SetItem(SerializedObject so, QuestItemSource source)
        {
            so.FindProperty("_questItem").objectReferenceValue = _whetstone;
            so.FindProperty("_itemSource").intValue = (int)source;
        }

        private static void SetRewards(SerializedObject so, params (ItemData item, int amount)[] rewards)
        {
            SerializedProperty list = so.FindProperty("_rewardItems");
            list.arraySize = rewards.Length;
            for (int i = 0; i < rewards.Length; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Item").objectReferenceValue = rewards[i].item;
                element.FindPropertyRelative("Amount").intValue = rewards[i].amount;
            }
        }

        private static void SetStrings(SerializedObject so, string field, params string[] values)
        {
            SerializedProperty list = so.FindProperty(field);
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                list.GetArrayElementAtIndex(i).stringValue = values[i];
            }
        }

        private static void SetObjects(SerializedObject so, string field, params Object[] values)
        {
            SerializedProperty list = so.FindProperty(field);
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static Dictionary<string, int> Carry(string itemId, int amount)
        {
            return new Dictionary<string, int> { { itemId, amount } };
        }

        private static void AssertRequest(QuestLootRequest request, QuestLootKind kind, string zoneA, string zoneB)
        {
            Assert.That(request.Kind, Is.EqualTo(kind));
            Assert.That(request.ItemId, Is.EqualTo(WhetstoneId));
            Assert.That(request.ZoneA, Is.EqualTo(zoneA));
            Assert.That(request.ZoneB, Is.EqualTo(zoneB));
        }
    }
}
