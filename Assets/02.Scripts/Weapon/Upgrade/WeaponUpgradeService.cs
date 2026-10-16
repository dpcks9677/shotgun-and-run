using System;
using InventorySystem;
using UnityEngine;

namespace ShotgunAndRun.Weapon
{
    public sealed class WeaponUpgradeService
    {
        private readonly WeaponUpgradeState _state;
        private readonly WeaponUpgradeCatalogSO _catalog;
        private readonly IItemInventory _storage;

        public WeaponUpgradeService(
            WeaponUpgradeState state,
            WeaponUpgradeCatalogSO catalog,
            IItemInventory storage)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public bool TryGetNextUpgradeCost(
            WeaponPartsType parts,
            out UpgradeStepCost stepCost)
        {
            int currentLevel = Mathf.Clamp(_state.GetLevel(parts), 1, 5);
            return _catalog.TryGetUpgradeStepCost(currentLevel, out stepCost);
        }

        // 지정한 파츠를 다음 레벨로 강화하는데 필요한 재료의 개수를 costs에 반환받음
        public bool TryGetNextUpgradeCosts(
            WeaponPartsType parts,
            out MaterialAmount[] costs)
        {
            // State에서 지정한 파츠의 현재 강화 레벨을 가져온다.
            int currentLevel = Mathf.Clamp(_state.GetLevel(parts), 1, 5);

            // 지정한 파츠를 다음 레벨로 강화하는데 필요한 재료의 개수를 costs에 반환받음
            return _catalog.TryGetUpgradeCosts(parts, currentLevel, out costs);
        }

        // 지정한 파츠의 강화를 수행하는 함수
        public bool TryUpgrade(WeaponPartsType parts, bool isShelter)
        {
            if (!isShelter)
            {
                return false;
            }

            int currentLevel = Mathf.Clamp(_state.GetLevel(parts), 1, 5);
            if (currentLevel >= 5)
            {
                return false;
            }

            // 지정한 파츠의 현재 레벨에 따른 다음 레벨로의 강화 비용을 costs에 반환받음
            if (!_catalog.TryGetUpgradeCosts(
                    parts,
                    currentLevel,
                    out MaterialAmount[] costs))
            {
                return false;
            }

            // 강화 재료와 필요 개수가 인벤토리에 존재하는지 검사
            foreach (MaterialAmount cost in costs)
            {
                if (!cost.IsValid || !_storage.HasItem(cost.Item, cost.Amount))
                {
                    return false;
                }
            }

            // 인벤토리에서 해당 재료를 강화 비용만큼 차감
            foreach (MaterialAmount cost in costs)
            {
                if (!_storage.TryRemoveItem(cost.Item, cost.Amount))
                {
                    return false;
                }
            }

            // 강화에 성공한 것이므로 파츠의 레벨을 증가시킴
            SetLevel(parts, currentLevel + 1);
            return true;
        }

        private void SetLevel(WeaponPartsType parts, int level)
        {
            switch (parts)
            {
                case WeaponPartsType.Barrel:
                    _state.BarrelLevel = level;
                    break;
                case WeaponPartsType.Chamber:
                    _state.ChamberLevel = level;
                    break;
                case WeaponPartsType.Belt:
                    _state.BeltLevel = level;
                    break;
                case WeaponPartsType.Stock:
                    _state.StockLevel = level;
                    break;
                case WeaponPartsType.Ammo:
                    _state.AmmoLevel = level;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(parts), parts, null);
            }
        }
    }
}
