using System;
using System.Collections.Generic;
using UnityEngine;

namespace Consumable
{
    [CreateAssetMenu(fileName = "ConsumableCraftingCosts",
        menuName = "ShotgunAndRun/Inventory/ConsumableCraftingCosts")]
    public class ConsumableCraftingCosts : ScriptableObject, IConsumableCraftingRecipeProvider
    {
        [SerializeField] private ConsumableCraftingCost[] _craftingCosts;

        public IReadOnlyList<ConsumableCraftingCost> CraftingCosts => _craftingCosts;

        public IReadOnlyList<ConsumableCraftingCost> GetAllRecipes()
        {
            return _craftingCosts ?? Array.Empty<ConsumableCraftingCost>();
        }

        public bool TryGetRecipe(ConsumableData data, out ConsumableCraftingCost recipe)
        {
            recipe = default;
            if (data == null || _craftingCosts == null)
            {
                return false;
            }

            for (int i = 0; i < _craftingCosts.Length; i++)
            {
                ConsumableData costData = _craftingCosts[i].ConsumableData;
                if (costData == data || (costData != null && costData.ConsumableType == data.ConsumableType))
                {
                    recipe = _craftingCosts[i];
                    return true;
                }
            }

            return false;
        }

        public bool TryGetRecipe(ConsumableType type, out ConsumableCraftingCost recipe)
        {
            recipe = default;
            if (_craftingCosts == null)
            {
                return false;
            }

            for (int i = 0; i < _craftingCosts.Length; i++)
            {
                ConsumableData costData = _craftingCosts[i].ConsumableData;
                if (costData != null && costData.ConsumableType == type)
                {
                    recipe = _craftingCosts[i];
                    return true;
                }
            }

            return false;
        }
    }
}
