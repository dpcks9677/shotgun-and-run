using System;
using UnityEngine;

namespace ShotgunAndRun.Weapon
{
    public sealed class ShotgunRuntimeStats
    {
        public float PelletDamage
        {
            get;
            private set;
        }
        public int PelletCount
        {
            get;
            private set;
        }
        public float SpreadAngle
        {
            get;
            private set;
        }
        public float FireInterval
        {
            get;
            private set;
        }
        public int MagazineSize
        {
            get;
            private set;
        }
        public float ReloadDuration
        {
            get;
            private set;
        }
        public float RecoilDistance
        {
            get;
            private set;
        }
        public float ProjectileSpeed
        {
            get;
            private set;
        }
        public float MaxRange
        {
            get;
            private set;
        }
        public float CriticalChance
        {
            get;
            private set;
        }

        // 거리 배율 경계에 적용할 총열 강화 배율
        public float DistanceBoundaryMultiplier
        {
            get;
            private set;
        }

        // 총알 관통 여부
        public bool HasPenetration
        {
            get;
            private set;
        }

        // 탄을 이 수만큼 장전할 때마다 그 탄은 쏠 때 크리티컬이 확정됩니다. (0이면 없음)
        public int CriticalShellInterval
        {
            get;
            private set;
        }

        private bool _initialized;

        public event Action<ShotgunRuntimeStats> StatsRecalculated;

        public ShotgunRuntimeStats(
            ShotgunDataSO data,
            WeaponUpgradeState upgrades)
        {
            Recalculate(data, upgrades);
        }

        // 총기 파츠 레벨에 따른 stat 계산
        public void Recalculate(
            ShotgunDataSO data,
            WeaponUpgradeState upgrades)
        {
            if (data == null)
            {
                Debug.LogError("ShotgunDataSO가 없습니다.");
                return;
            }

            if (upgrades == null)
            {
                Debug.LogError("WeaponUpgradeState가 없습니다.");
                return;
            }

            int barrelLevel = Mathf.Clamp(upgrades.BarrelLevel, 1, 5);
            int chamberLevel = Mathf.Clamp(upgrades.ChamberLevel, 1, 5);
            int beltLevel = Mathf.Clamp(upgrades.BeltLevel, 1, 5);
            int stockLevel = Mathf.Clamp(upgrades.StockLevel, 1, 5);
            int ammoLevel = Mathf.Clamp(upgrades.AmmoLevel, 1, 5);

            PelletDamage =
                data.PelletDamage * GetAmmoDamageMultiplier(ammoLevel);

            // 탄약 Lv3: 4발이 장전될 때마다 크리티컬 탄 1발
            CriticalShellInterval = ammoLevel >= 3 ? 4 : 0;

            PelletCount = data.PelletCount;

            // 총열: 레벨당 확산각 4도 감소
            SpreadAngle = Mathf.Max(
                0f,
                data.SpreadAngle - 4f * (barrelLevel - 1));

            //TODO : 거리 구간 경꼐 늘리는 부분 추가

            // 약실&노리쇠: 공격속도 증가
            FireInterval =
                data.FireInterval /
                (1f + 0.08f * (chamberLevel - 1));

            // 탄띠: Lv3에서 +1, Lv4에서 +2
            MagazineSize =
                data.MagazineSize + GetMagazineBonus(beltLevel);

            // 탄띠 Lv5: 장전 속도 30% 증가 (탄 1발당 장전 간격 -30%)
            ReloadDuration =
                data.ReloadDuration * (beltLevel >= 5 ? 0.7f : 1f);

            // 개머리판: 반동 감소
            RecoilDistance =
                data.RecoilDistance *
                (1f - 0.1f * (stockLevel - 1));

            ProjectileSpeed = data.ProjectileSpeed;
            MaxRange = data.MaxRange;

            // 개머리판: 레벨당 크리티컬 확률 3% 증가
            CriticalChance = Mathf.Clamp01(
                data.CriticalChance + 0.03f * (stockLevel - 1));

            // 총열: 거리 배율 경계가 레벨당 10% 증가
            DistanceBoundaryMultiplier =
                1f + 0.1f * (barrelLevel - 1);

            // 탄띠 특수 효과
            HasPenetration = beltLevel >= 2;

            // TODO : 관통 효과 추가

            StatsRecalculated?.Invoke(this);
        }

        private static float GetAmmoDamageMultiplier(int level)
        {
            return level switch
            {
                1 => 1f,
                2 => 1.09f,
                3 => 1.2f,
                4 => 1.33f,
                5 => 1.5f,
                _ => 1f
            };
        }

        private static int GetMagazineBonus(int level)
        {
            return level switch
            {
                3 => 1,
                >= 4 => 2,
                _ => 0
            };
        }

        public bool RollCritical()
        {
            // 0%와 100%는 확정 처리
            if (CriticalChance <= 0f)
            {
                return false;
            }

            if (CriticalChance >= 1f)
            {
                return true;
            }

            return UnityEngine.Random.value < CriticalChance;
        }
    }
}
