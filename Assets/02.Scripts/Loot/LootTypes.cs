using System;
using InventorySystem;

namespace ShotgunAndRun.Loot
{
    // 상자 스폰 지점의 소속 (game-spec 4.2 · 4.3 · 9.3)
    public enum ChestPointKind
    {
        Zone,           // 존 상자 후보 지점 (존마다 5~6곳)
        Connector,      // 커넥터 상자 후보 지점 (구간마다 4~5곳)
        RecoveryFixed   // 회수 퀘스트 고정 상자 (수주한 런에만 생성)
    }

    // 상자 스폰 지점에 놓을 상자 크기 · 방향. 씬에 배치할 때 정하고 ChestPointValidator로 검사합니다.
    public enum ChestPointSize
    {
        Small,          // 1x1: 지점 칸
        Large,          // 2x1: 지점 칸 + 오른쪽 칸 (북 · 남쪽 벽 앞)
        TallEast,       // 1x2: 지점 칸 + 위 칸, 걸쇠 동쪽 (서쪽 벽 앞)
        TallWest        // 1x2: 지점 칸 + 위 칸, 걸쇠 서쪽 (동쪽 벽 앞)
    }

    // 존 세트 항목이 가리키는 재료. 공통 템플릿은 Main · Sub1 · Sub2로 존마다 다른 재료를 가리킵니다.
    public enum LootMaterialSlot
    {
        Fixed,  // Material 필드의 재료를 그대로 사용
        Main,   // 존의 주 재료
        Sub1,   // 존의 부 재료 1
        Sub2    // 존의 부 재료 2
    }

    public enum QuestLootKind
    {
        ZoneCertificate,  // 초반 탐사: 대상 존 상자 1개에 인증 아이템 (잠금 없음)
        Treasure,         // 보물찾기: 두 존의 존 상자 + 사이 커넥터 상자 중 생성된 상자 1개
        Recovery          // 회수: 존의 고정 상자에 회수 아이템 + 치유 약초
    }

    [Serializable]
    public struct MaterialStack
    {
        public MaterialType Material;
        public int Amount;

        public MaterialStack(MaterialType material, int amount)
        {
            Material = material;
            Amount = amount;
        }

        public override string ToString()
        {
            return $"{Material} {Amount}";
        }
    }
}
