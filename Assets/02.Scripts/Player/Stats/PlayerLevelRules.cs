using UnityEngine;

namespace ShotgunAndRun.Player
{
    /// <summary>
    /// 플레이어 레벨(1~10)에 따른 가방, 이동속도, 체력, 대시 충전 시간 성장 보너스 단일 규칙 (SSOT)
    /// </summary>
    public static class PlayerLevelRules
    {
        public const string LevelStatSourceId = "PlayerLevelProgression";

        public static int GetBagCapacity(int level)
        {
            if (level >= 9) return 8;
            if (level >= 6) return 7;
            if (level >= 3) return 6;
            return 5;
        }

        public static float GetMoveSpeedMultiplier(int level)
        {
            if (level >= 8) return 1.15f;
            if (level >= 5) return 1.10f;
            if (level >= 2) return 1.05f;
            return 1.0f;
        }

        public static int GetMaxHealthBonus(int level)
        {
            if (level >= 10) return 3;
            if (level >= 7) return 2;
            if (level >= 4) return 1;
            return 0;
        }

        public static float GetDashChargeReduction(int level)
        {
            if (level >= 7) return 1.0f;
            if (level >= 4) return 0.5f;
            return 0.0f;
        }
    }
}
