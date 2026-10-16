using System.Collections.Generic;

namespace ShotgunAndRun.Quest
{
    // 탈출 시 퀘스트 판정 결과입니다. 이번 런에 완료 가능 상태가 된 퀘스트만 담습니다.
    // XP와 보상 아이템은 판정 때 지급하지 않고, 보고(QuestService.TryTurnIn)할 때 지급합니다.
    public sealed class QuestReadyReport
    {
        public static readonly QuestReadyReport Empty = new QuestReadyReport(
            new List<QuestDefinitionSO>(),
            new List<string>());

        public QuestReadyReport(
            IReadOnlyList<QuestDefinitionSO> readyQuests,
            IReadOnlyList<string> consumedItemIds)
        {
            ReadyQuests = readyQuests;
            ConsumedItemIds = consumedItemIds;
        }

        // 이번 런에 완료 가능(보고 대기)이 된 퀘스트
        public IReadOnlyList<QuestDefinitionSO> ReadyQuests
        {
            get;
        }

        // 완료 판정에 쓴 퀘스트 아이템. 1개씩 기록하며, 창고에 넣지 않고 소비합니다.
        public IReadOnlyList<string> ConsumedItemIds
        {
            get;
        }

        public bool HasReadyQuest => ReadyQuests.Count > 0;
    }
}
