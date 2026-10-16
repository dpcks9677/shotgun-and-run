using System;
using System.Collections.Generic;
using InventorySystem;
using UnityEngine;

namespace ShotgunAndRun.Weapon
{
    [CreateAssetMenu(
        fileName = "WeaponUpgradeCatalog",
        menuName = "ShotgunAndRun/Weapon/Weapon Upgrade Catalog")]
    public sealed class WeaponUpgradeCatalogSO : ScriptableObject
    {
        [Header("Shared Materials")]
        [SerializeField] private ItemData _coreMaterial;

        public ItemData CoreMaterial => _coreMaterial;

        [Header("Upgrade Costs")]
        [SerializeField] private UpgradeStepCost[] _stepCosts = new UpgradeStepCost[4];

        [Header("Part Recipes")]
        [SerializeField] private WeaponPartsRecipe[] _partRecipes = new WeaponPartsRecipe[5];

        [Header("Part Display Data")]
        [SerializeField] private PartsDataSO[] _partsData = new PartsDataSO[5];

        // 현재 레벨에서 다음 레벨로 강화할 때 필요한 비용을 반환합니다.
        // 배열 인덱스는 0부터 시작하므로 Lv.1 -> 2 비용은 _stepCosts[0]입니다.
        // out으로 강화 비용을 반환하고, return으로 강화 비용 존재 여부를 반환
        public bool TryGetUpgradeStepCost(
            int currentLevel,
            out UpgradeStepCost stepCost)
        {
            stepCost = default;

            if (_stepCosts == null || currentLevel < 1 || currentLevel >= 5)
            {
                return false;
            }

            int index = currentLevel - 1;
            if (index < 0 || index >= _stepCosts.Length)
            {
                return false;
            }

            stepCost = _stepCosts[index];
            return HasAnyCost(stepCost);
        }

        // 인벤토리 또는 UI에서 지정한 파츠의 재료 종류가 필요할 때 사용한다.
        public bool TryGetPartsData(
            WeaponPartsType parts,
            out PartsDataSO partsData)
        {
            if (_partsData != null)
            {
                foreach (PartsDataSO candidate in _partsData)
                {
                    if (candidate != null &&
                        candidate.PartsType == parts)
                    {
                        partsData = candidate;
                        return true;
                    }
                }
            }

            partsData = null;
            return false;
        }

        // 지정한 parts가 강화하는데 필요한 재료(ItemData)를 반환한다.
        public bool TryGetPartRecipe(
            WeaponPartsType parts,
            out WeaponPartsRecipe recipe)
        {
            if (_partRecipes != null)
            {
                foreach (WeaponPartsRecipe candidate in _partRecipes)
                {
                    if (candidate != null && candidate.Parts == parts)
                    {
                        recipe = candidate;
                        return true;
                    }
                }
            }

            recipe = null;
            return false;
        }

        // 지정한 파츠의 업그레이드시 필요한 재료와 양을 반환한다.
        public bool TryGetUpgradeCosts(
            WeaponPartsType parts,
            int currentLevel,
            out MaterialAmount[] costs)
        {
            costs = Array.Empty<MaterialAmount>();

            // 파츠의 강화 재료 목록을 가져오는데 실패하거나
            // 레벨별 업그레이드에 필요한 재료의 개수를 가져오는데 실패한 경우 false 반환
            // 이게 정상적으로 실행되면 recipe에 지정한 parts를 업그레이드 하는데 필요한 강화 재료와
            // stepCost에 현재 레벨에서 업그레이드 하는데 필요한 각 재료(Main, Sub1~3, Core)의 개수가 반환된다.
            if (!TryGetPartRecipe(parts, out WeaponPartsRecipe recipe) ||
                !TryGetUpgradeStepCost(currentLevel, out UpgradeStepCost stepCost))
            {
                return false;
            }


            List<MaterialAmount> resolvedCosts = new List<MaterialAmount>(5);

            // 리스트에 강화에 필요한 재료와 개수를 매칭하여 추가
            // 즉, 지정한 파츠의 강화 재료(ex. Main : Leather, Sub1 : IronIngot, ...)와
            // 모든 파츠들이 공유하는 레벨별 각 강화 재료의 개수(ex. Main : 3개, Sub1 : 1개, ...)를 매핑하여
            // 최종적으로 지정 파츠를 강화하는데 필요한 재료와 개수 (Leather 3개, IronIngot 1개, ...)
            // 와 같은 형태로 만들어 리스트에 추가하는 것이다
            if (!TryAddCost(resolvedCosts, recipe.Main, stepCost.MainAmount) ||
                !TryAddCost(resolvedCosts, recipe.Sub1, stepCost.Sub1Amount) ||
                !TryAddCost(resolvedCosts, recipe.Sub2, stepCost.Sub2Amount) ||
                !TryAddCost(resolvedCosts, recipe.Sub3, stepCost.Sub3Amount) ||
                !TryAddCost(resolvedCosts, _coreMaterial, stepCost.CoreAmount))
            {
                return false;
            }

            costs = resolvedCosts.ToArray();
            return costs.Length > 0;
        }

        // UpgradeStepCost에 비용이 하나라도 존재하는지 검사하는 함수
        private static bool HasAnyCost(UpgradeStepCost stepCost)
        {
            return stepCost.MainAmount > 0 ||
                   stepCost.Sub1Amount > 0 ||
                   stepCost.Sub2Amount > 0 ||
                   stepCost.Sub3Amount > 0 ||
                   stepCost.CoreAmount > 0;
        }

        // MaterialAmount를 초기화하여 리스트에 추가한다.
        // 즉, 강화 재료와 필요 개수를 나타내는 MaterialAmount를 초기화하여 리스트에 추가
        private static bool TryAddCost(
            IList<MaterialAmount> costs,
            ItemData item,
            int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            if (item == null)
            {
                return false;
            }

            for (int i = 0; i < costs.Count; i++)
            {
                if (costs[i].Item == item)
                {
                    costs[i] = new MaterialAmount(
                        item,
                        costs[i].Amount + amount);
                    return true;
                }
            }

            costs.Add(new MaterialAmount(item, amount));
            return true;
        }

        private void OnValidate()
        {
            HashSet<WeaponPartsType> types = new();

            foreach (WeaponPartsRecipe recipe in _partRecipes)
            {
                if (recipe?.Parts == null)
                {
                    Debug.LogWarning("PartsDataSO가 없는 파츠 레시피가 있습니다.", this);
                    continue;
                }

                if (!types.Add(recipe.Parts))
                {
                    Debug.LogWarning(
                        $"{recipe.Parts} 파츠가 중복 등록되었습니다.",
                        this);
                }
            }
        }
    }
}
