using System;
using System.Collections.Generic;
using ShotgunAndRun.Loot;

namespace ShotgunAndRun.Quest
{
    // 쉘터 퀘스트 규칙 (game-spec 9.3)
    // - 게시판에서 수주, 조건 달성 후 탈출하면 완료 가능(ReadyToTurnIn) 상태가 되고 보고(TryTurnIn)해야 완료
    // - 사망하면 이번 런의 진행은 무효, 수주는 유지
    // - 반복 퀘스트 없음, 수주 한도는 스토리 포함 합계 3개
    //   스토리는 컷씬이 강제로 수주하므로 합계 한도를 무시하고 스토리끼리 1개만 제한 (의뢰 3 + 스토리 1 = 4/3 가능)
    //   완료 가능 상태도 수주 칸을 차지하며, 완료 가능 퀘스트는 취소할 수 없음
    // 런 중 플래그는 이 객체에만 두고, 탈출 판정(CompleteRun) 때 QuestProgress에 반영합니다.
    // XP · 보상 아이템은 보고 때 반환만 하고, 지급은 호출하는 쪽(GameManager)이 담당합니다.
    // 쉘터에서만 수주 · 포기하도록 막는 것은 호출하는 쪽이 담당합니다.
    public sealed class QuestService
    {
        public const int MaxAcceptedCount = 3;

        private readonly QuestProgress _progress;
        private readonly QuestCatalogSO _catalog;
        private readonly HashSet<string> _runFlags = new HashSet<string>(StringComparer.Ordinal);

        public QuestService(QuestProgress progress, QuestCatalogSO catalog)
        {
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

            // 세이브 파일에서 모든 퀘스트가 보이도록 카탈로그 순서로 기록을 채운다
            List<string> questIds = new List<string>();
            foreach (QuestDefinitionSO quest in _catalog.Quests)
            {
                if (quest != null)
                {
                    questIds.Add(quest.Id);
                }
            }

            _progress.EnsureRecords(questIds);
        }

        // 수주 · 포기 · 완료 가능 · 완료 처리 · 게시판 표시 기록이 바뀜
        public event Action Changed;

        public bool IsRunActive
        {
            get;
            private set;
        }

        // ID로 퀘스트 정의를 찾습니다. (컷씬 등 ID만 아는 호출자용)
        public bool TryGetQuest(string questId, out QuestDefinitionSO quest)
        {
            return _catalog.TryGetById(questId, out quest);
        }

        public QuestState GetState(QuestDefinitionSO quest)
        {
            if (quest == null)
            {
                throw new ArgumentNullException(nameof(quest));
            }

            if (_progress.IsCompleted(quest.Id))
            {
                return QuestState.Completed;
            }

            if (_progress.IsAccepted(quest.Id))
            {
                return _progress.IsReady(quest.Id) ? QuestState.ReadyToTurnIn : QuestState.Accepted;
            }

            return IsUnlocked(quest) && IsZoneVisitSatisfied(quest)
                ? QuestState.Available
                : QuestState.Locked;
        }

        public List<QuestDefinitionSO> GetQuests(QuestState state)
        {
            List<QuestDefinitionSO> result = new List<QuestDefinitionSO>();
            foreach (QuestDefinitionSO quest in _catalog.Quests)
            {
                if (quest != null && GetState(quest) == state)
                {
                    result.Add(quest);
                }
            }

            return result;
        }

        // 수주 중인 퀘스트를 수주한 순서대로 반환합니다. (완료 가능 포함)
        public List<QuestDefinitionSO> GetAcceptedQuests()
        {
            List<QuestDefinitionSO> result = new List<QuestDefinitionSO>();
            foreach (string questId in _progress.GetAcceptedIds())
            {
                if (_catalog.TryGetById(questId, out QuestDefinitionSO quest))
                {
                    result.Add(quest);
                }
            }

            return result;
        }

        // 수주 중인 퀘스트 수 (스토리 · 완료 가능 포함)
        public int AcceptedCount => GetAcceptedQuests().Count;

        public bool CanAccept(QuestDefinitionSO quest)
        {
            if (quest == null || IsRunActive || GetState(quest) != QuestState.Available)
            {
                return false;
            }

            // 스토리는 합계 한도를 무시하고 스토리끼리만 1개로 제한합니다.
            if (quest.Slot == QuestSlot.Story)
            {
                foreach (QuestDefinitionSO accepted in GetAcceptedQuests())
                {
                    if (accepted.Slot == QuestSlot.Story)
                    {
                        return false;
                    }
                }

                return true;
            }

            return AcceptedCount < MaxAcceptedCount;
        }

        public bool TryAccept(QuestDefinitionSO quest)
        {
            if (!CanAccept(quest) || !_progress.Accept(quest.Id))
            {
                return false;
            }

            Changed?.Invoke();
            return true;
        }

        // 의뢰만 포기할 수 있습니다. 스토리는 항상 진행하고, 완료 가능 퀘스트는 취소할 수 없습니다.
        public bool TryAbandon(QuestDefinitionSO quest)
        {
            if (quest == null ||
                IsRunActive ||
                quest.Slot != QuestSlot.Request ||
                _progress.IsReady(quest.Id) ||
                !_progress.Abandon(quest.Id))
            {
                return false;
            }

            Changed?.Invoke();
            return true;
        }

        // 보고(완료 처리): 런 중이 아니고 완료 가능 상태일 때만 성공합니다.
        // 보상은 반환만 하고 지급은 호출하는 쪽이 합니다.
        public bool TryTurnIn(QuestDefinitionSO quest, out int rewardXp, out List<QuestRewardItem> rewardItems)
        {
            rewardXp = 0;
            rewardItems = new List<QuestRewardItem>();
            if (quest == null || IsRunActive || GetState(quest) != QuestState.ReadyToTurnIn)
            {
                return false;
            }

            _progress.Complete(quest.Id);
            rewardXp = quest.RewardXp;
            foreach (QuestRewardItem reward in quest.RewardItems)
            {
                if (reward.IsValid)
                {
                    rewardItems.Add(reward);
                }
            }

            Changed?.Invoke();
            return true;
        }

        // 게시판에 놓을 카드 순서: ① 스토리(완료 도장 대기 → 수주 중) ② 도장 대기 의뢰
        // ③ 수주 중인 의뢰(수주한 순서, 완료 가능 포함) ④ 수락 가능 의뢰(카탈로그 순서)
        // 수락 가능한 스토리는 컷씬 수주 전용이라 나오지 않습니다.
        public List<QuestDefinitionSO> GetBoardQuests()
        {
            List<QuestDefinitionSO> result = new List<QuestDefinitionSO>();
            List<QuestDefinitionSO> accepted = GetAcceptedQuests();

            AddPendingStamps(result, QuestSlot.Story);
            AddAccepted(result, accepted, QuestSlot.Story);
            AddPendingStamps(result, QuestSlot.Request);
            AddAccepted(result, accepted, QuestSlot.Request);
            foreach (QuestDefinitionSO quest in _catalog.Quests)
            {
                if (quest != null && quest.Slot == QuestSlot.Request && GetState(quest) == QuestState.Available)
                {
                    result.Add(quest);
                }
            }

            return result;
        }

        public bool IsStampPending(QuestDefinitionSO quest)
        {
            return quest != null && _progress.IsStampPending(quest.Id);
        }

        // 게시판을 닫을 때 완료 도장 대기를 비웁니다.
        public void AcknowledgeStamps()
        {
            if (_progress.ClearPendingStamps())
            {
                Changed?.Invoke();
            }
        }

        // 수주 중인 퀘스트 중 처음 나오는 고정 스폰 (스토리 우선, 완료 가능 퀘스트 제외)
        public bool TryGetFixedSpawnId(out string spawnId)
        {
            foreach (QuestSlot slot in new[] { QuestSlot.Story, QuestSlot.Request })
            {
                foreach (QuestDefinitionSO quest in GetAcceptedQuests())
                {
                    if (quest.Slot == slot &&
                        !_progress.IsReady(quest.Id) &&
                        !string.IsNullOrWhiteSpace(quest.FixedSpawnId))
                    {
                        spawnId = quest.FixedSpawnId;
                        return true;
                    }
                }
            }

            spawnId = null;
            return false;
        }

        // 이번 런에 상자에 넣을 퀘스트 아이템 (LootGenerator 입력). 완료 가능 퀘스트는 제외합니다.
        public List<QuestLootRequest> GetLootRequests()
        {
            List<QuestLootRequest> requests = new List<QuestLootRequest>();
            foreach (QuestDefinitionSO quest in GetAcceptedQuests())
            {
                if (quest.QuestItem == null || _progress.IsReady(quest.Id))
                {
                    continue;
                }

                switch (quest.ItemSource)
                {
                    case QuestItemSource.ZoneChest:
                        requests.Add(new QuestLootRequest(QuestLootKind.ZoneCertificate, quest.QuestItem.Id,
                            quest.ZoneA));
                        break;
                    case QuestItemSource.Treasure:
                        requests.Add(new QuestLootRequest(QuestLootKind.Treasure, quest.QuestItem.Id, quest.ZoneA,
                            quest.ZoneB));
                        break;
                    case QuestItemSource.RecoveryChest:
                        requests.Add(new QuestLootRequest(QuestLootKind.Recovery, quest.QuestItem.Id, quest.ZoneA));
                        break;
                }
            }

            return requests;
        }

        public void BeginRun()
        {
            _runFlags.Clear();
            IsRunActive = true;
        }

        // 런 중 이벤트(구출, 디펜스 클리어 등)를 기록합니다. 탈출해야 반영됩니다.
        public void RaiseRunFlag(string flag)
        {
            if (IsRunActive && !string.IsNullOrWhiteSpace(flag))
            {
                _runFlags.Add(flag);
            }
        }

        public bool HasRunFlag(string flag)
        {
            return !string.IsNullOrEmpty(flag) && _runFlags.Contains(flag);
        }

        // 사망 · 런 중단: 이번 런의 진행만 버리고 수주는 유지합니다.
        public void FailRun()
        {
            _runFlags.Clear();
            IsRunActive = false;
        }

        // 탈출 성공 시 판정합니다. 조건을 채운 수주 중 퀘스트는 완료 가능 상태가 됩니다.
        // 해금 플래그(UnlockFlags)는 이때 바로 켜고, XP · 보상은 보고(TryTurnIn)할 때 반환합니다.
        // carriedItemAmounts: 가방에 들고 나온 아이템 (ItemData.Id → 수량)
        // visitedZoneIds: 이번 런에 들어간 존
        public QuestReadyReport CompleteRun(
            IReadOnlyDictionary<string, int> carriedItemAmounts,
            IEnumerable<string> visitedZoneIds)
        {
            if (!IsRunActive)
            {
                return QuestReadyReport.Empty;
            }

            // 방문 확인은 이번 런의 완료 가능 판정을 반영하기 전에 합니다.
            // "선행 퀘스트를 완료한 뒤" 방문해야 하므로, 이번 런에 완료 가능이 된 퀘스트는 보고한 뒤의 런부터 인정됩니다.
            MarkVisitedZones(visitedZoneIds);

            Dictionary<string, int> remainingItems = carriedItemAmounts != null
                ? new Dictionary<string, int>(carriedItemAmounts, StringComparer.Ordinal)
                : new Dictionary<string, int>(StringComparer.Ordinal);

            List<QuestDefinitionSO> readyQuests = new List<QuestDefinitionSO>();
            List<string> consumedItemIds = new List<string>();

            foreach (QuestDefinitionSO quest in GetAcceptedQuests())
            {
                if (_progress.IsReady(quest.Id) || !IsCompletionMet(quest, remainingItems))
                {
                    continue;
                }

                if (quest.QuestItem != null)
                {
                    remainingItems[quest.QuestItem.Id]--;
                    consumedItemIds.Add(quest.QuestItem.Id);
                }

                _progress.MarkReady(quest.Id);
                foreach (string flag in quest.UnlockFlags)
                {
                    _progress.AddFlag(flag);
                }

                readyQuests.Add(quest);
            }

            _runFlags.Clear();
            IsRunActive = false;

            if (readyQuests.Count > 0)
            {
                Changed?.Invoke();
            }

            return new QuestReadyReport(readyQuests, consumedItemIds);
        }

        private bool IsCompletionMet(QuestDefinitionSO quest, Dictionary<string, int> remainingItems)
        {
            if (quest.QuestItem != null &&
                (!remainingItems.TryGetValue(quest.QuestItem.Id, out int amount) || amount <= 0))
            {
                return false;
            }

            foreach (string flag in quest.RequiredRunFlags)
            {
                if (!HasRunFlag(flag))
                {
                    return false;
                }
            }

            return true;
        }

        private void AddPendingStamps(List<QuestDefinitionSO> result, QuestSlot slot)
        {
            foreach (string questId in _progress.GetPendingStampIds())
            {
                if (_catalog.TryGetById(questId, out QuestDefinitionSO quest) && quest.Slot == slot)
                {
                    result.Add(quest);
                }
            }
        }

        private static void AddAccepted(List<QuestDefinitionSO> result, List<QuestDefinitionSO> accepted,
            QuestSlot slot)
        {
            foreach (QuestDefinitionSO quest in accepted)
            {
                if (quest.Slot == slot)
                {
                    result.Add(quest);
                }
            }
        }

        private void MarkVisitedZones(IEnumerable<string> visitedZoneIds)
        {
            if (visitedZoneIds == null)
            {
                return;
            }

            HashSet<string> visited = new HashSet<string>(visitedZoneIds, StringComparer.Ordinal);
            foreach (QuestDefinitionSO quest in _catalog.Quests)
            {
                if (quest != null &&
                    quest.RequiresZoneVisitAfterUnlock &&
                    !_progress.IsCompleted(quest.Id) &&
                    visited.Contains(quest.ZoneA) &&
                    IsUnlocked(quest))
                {
                    _progress.MarkZoneVisited(quest.Id);
                }
            }
        }

        // 선행 퀘스트 · 영구 플래그 조건
        private bool IsUnlocked(QuestDefinitionSO quest)
        {
            foreach (string flag in quest.RequiredFlags)
            {
                if (!_progress.HasFlag(flag))
                {
                    return false;
                }
            }

            IReadOnlyList<QuestDefinitionSO> prerequisites = quest.PrerequisiteQuests;
            int completedCount = 0;
            int validCount = 0;
            foreach (QuestDefinitionSO prerequisite in prerequisites)
            {
                if (prerequisite == null)
                {
                    continue;
                }

                validCount++;
                if (_progress.IsCompleted(prerequisite.Id))
                {
                    completedCount++;
                }
            }

            int required = quest.PrerequisiteMinCount > 0 ? quest.PrerequisiteMinCount : validCount;
            return completedCount >= required;
        }

        private bool IsZoneVisitSatisfied(QuestDefinitionSO quest)
        {
            return !quest.RequiresZoneVisitAfterUnlock || _progress.IsZoneVisited(quest.Id);
        }
    }
}
