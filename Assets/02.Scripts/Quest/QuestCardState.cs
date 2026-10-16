namespace ShotgunAndRun.Quest
{
    // 게시판 카드가 보여주는 상태. 퀘스트 상태 + 수락 가능 여부로 결정합니다.
    public enum QuestCardState
    {
        Available,   // 수락 버튼
        Full,        // 수락 불가 (수주 칸이 가득 참)
        Active,      // 진행 중인 의뢰 (퀘스트 취소)
        StoryActive, // 진행 중인 스토리
        Ready,       // 완료 가능, 게시판에서 보고 (퀘스트 완료 버튼)
        ReportToNpc, // 완료 가능, NPC 보고 컷씬에서 보고 (TurnInByNpc)
        Done         // 완료 도장
    }

    public static class QuestCardStateResolver
    {
        // 게시판에 보여줄 수 없는 상태(Locked)는 false를 반환합니다.
        public static bool TryResolve(QuestSlot slot, bool turnInByNpc, QuestState state, bool canAccept,
            out QuestCardState result)
        {
            bool isStory = slot == QuestSlot.Story;
            switch (state)
            {
                case QuestState.Available:
                    result = canAccept ? QuestCardState.Available : QuestCardState.Full;
                    return true;
                case QuestState.Accepted:
                    result = isStory ? QuestCardState.StoryActive : QuestCardState.Active;
                    return true;
                case QuestState.ReadyToTurnIn:
                    result = turnInByNpc ? QuestCardState.ReportToNpc : QuestCardState.Ready;
                    return true;
                case QuestState.Completed:
                    result = QuestCardState.Done;
                    return true;
                default:
                    result = QuestCardState.Full;
                    return false;
            }
        }
    }
}
