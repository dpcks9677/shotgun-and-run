using System;
using UnityEngine;

namespace ShotgunAndRun.Core
{
    // 런 진행 수치 (game-spec 3.4, 4.4, 9.2)
    [Serializable]
    public sealed class RunRules
    {
        [Header("Time (seconds)")]
        [Min(0f)] public float ExtractionUnlockTime = 180f;
        [Min(0f)] public float TimeLimit = 720f;
        [Min(0f)] public float ExtractionHoldTime = 10f;

        [Header("Over Time Penalty")]
        [Range(0f, 1f)] public float OverTimeMoveSpeedMultiplier = 0.8f;
        [Range(0.01f, 1f)] public float OverTimeMaxHealthDebuffMultiplier = 0.5f;

        [Header("XP")]
        [Min(0)] public int NormalKillXp = 5;
        [Min(0)] public int EliteKillXp = 20;
        [Min(0)] public int ZoneClearXp = 100;
        [Min(0f)] public float ExtractionXpMultiplier = 1.5f;
        [Min(0f)] public float DeathXpMultiplier = 0.3f;

        // 레이드 타이머 범위 설정
        public const float RaidMinDuration = 140f;
        public const float RaidMaxDuration = 180f;
    }
}
