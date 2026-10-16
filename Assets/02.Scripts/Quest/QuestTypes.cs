using System;
using InventorySystem;
using UnityEngine;

namespace ShotgunAndRun.Quest
{
    // 퀘스트 타입 (game-spec 9.3). 에셋에 정수로 저장되므로 값을 고정합니다.
    public enum QuestType
    {
        MainStory = 0,          // 메인 스토리 (스토리 슬롯)
        EarlyExploration = 1,   // 초반 탐사: 존 인증 아이템 회수
        ZonePurification = 2,   // 구역 정화: 퀘스트 디펜스
        TreasureHunt = 3,       // 보물찾기: 인접한 두 존 중 생성된 상자
        Recovery = 4            // 회수: 존의 고정 상자 (지정 엘리트 처치는 적 연결 후 추가)
    }

    // 스토리는 컷씬이 수주하며 동시에 1개, 수주 한도(스토리 포함 3개)는 의뢰 수락에만 적용 (game-spec 9.3)
    public enum QuestSlot
    {
        Story,
        Request
    }

    public enum QuestState
    {
        Locked,     // 해금 조건 미충족
        Available,  // 게시판에서 수주 가능
        Accepted,   // 수주 중
        ReadyToTurnIn, // 완료 가능: 탈출 판정은 통과했고 보고(완료 처리)만 남음
        Completed   // 완료 (반복 퀘스트 없음)
    }

    // 퀘스트 아이템이 등장하는 곳. 수주한 런에만 등장합니다 (game-spec 9.3).
    public enum QuestItemSource
    {
        None = 0,           // 퀘스트 아이템 없음 (플래그만으로 완료)
        ZoneChest = 1,      // 대상 존의 존 상자 1개에 확정 (초반 탐사 인증 아이템 등)
        Treasure = 2,       // 두 존의 존 상자 + 사이 커넥터 상자 중 1개
        RecoveryChest = 3   // 존의 회수 고정 상자
    }

    [Serializable]
    public struct QuestRewardItem
    {
        public ItemData Item;
        [Min(1)] public int Amount;

        public QuestRewardItem(ItemData item, int amount)
        {
            Item = item;
            Amount = amount;
        }

        public bool IsValid => Item != null && Amount > 0;
    }
}
