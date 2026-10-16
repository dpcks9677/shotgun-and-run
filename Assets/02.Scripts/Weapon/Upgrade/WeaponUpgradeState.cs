using System;
using UnityEngine;

namespace ShotgunAndRun.Weapon
{
    // 현재 강화 상태를 나타내는 클래스
    [Serializable]
    public sealed class WeaponUpgradeState
    {
        [Range(1, 5)] public int BarrelLevel = 1;
        [Range(1, 5)] public int ChamberLevel = 1;
        [Range(1, 5)] public int BeltLevel = 1;
        [Range(1, 5)] public int StockLevel = 1;
        [Range(1, 5)] public int AmmoLevel = 1;

        public int GetLevel(WeaponPartsType parts)
        {
            return parts switch
            {
                WeaponPartsType.Barrel => BarrelLevel,
                WeaponPartsType.Chamber => ChamberLevel,
                WeaponPartsType.Belt => BeltLevel,
                WeaponPartsType.Stock => StockLevel,
                WeaponPartsType.Ammo => AmmoLevel,
                _ => 1
            };
        }
    }
}
