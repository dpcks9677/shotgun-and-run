using UnityEngine;

namespace ShotgunAndRun.Weapon
{
    // 총기가 아닌 현재 발사되는 총알에 적용되는 스탯
    public readonly struct ShotContext
    {
        public readonly float PelletDamage;
        public readonly float CriticalChance;
        public readonly bool IsCritical;
        public readonly bool HasPenetration;
        public readonly float MaxRange;

        public ShotContext(
            float pelletDamage,
            float criticalChance,
            bool isCritical,
            bool hasPenetration,
            float maxRange)
        {
            PelletDamage = pelletDamage;
            CriticalChance = criticalChance;
            IsCritical = isCritical;
            HasPenetration = hasPenetration;
            MaxRange = maxRange;
        }
    }
}
