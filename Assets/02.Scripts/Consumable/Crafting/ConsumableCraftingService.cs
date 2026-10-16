using System;
using InventorySystem;

namespace Consumable
{
    public enum CraftFailureReason
    {
        None,
        RecipeNotFound,
        InsufficientMaterials,
        InventoryFull,
        InvalidTarget
    }

    /// <summary>
    /// 소모품 제작 비즈니스 로직을 담당하는 도메인 서비스입니다.
    /// 레시피 확인, 재료 재고 확인 및 차감, 소모품 퀵슬롯/인벤토리 지급을 순수 C# 로직으로 수행합니다.
    /// </summary>
    public class ConsumableCraftingService
    {
        private readonly IConsumableCraftingRecipeProvider _recipeProvider;
        private readonly IItemInventory _materialStorage;
        private readonly IConsumableInventory _consumableInventory;

        public ConsumableCraftingService(
            IConsumableCraftingRecipeProvider recipeProvider,
            IItemInventory materialStorage,
            IConsumableInventory consumableInventory)
        {
            _recipeProvider = recipeProvider ?? throw new ArgumentNullException(nameof(recipeProvider));
            _materialStorage = materialStorage ?? throw new ArgumentNullException(nameof(materialStorage));
            _consumableInventory = consumableInventory;
        }

        /// <summary>
        /// 해당 소모품을 현재 보유 재료 및 인벤토리 슬롯 상태로 제작 가능한지 검사합니다.
        /// </summary>
        public bool CanCraft(ConsumableData target, out CraftFailureReason reason)
        {
            if (target == null)
            {
                reason = CraftFailureReason.InvalidTarget;
                return false;
            }

            if (!_recipeProvider.TryGetRecipe(target, out ConsumableCraftingCost recipe))
            {
                reason = CraftFailureReason.RecipeNotFound;
                return false;
            }

            // 인벤토리(퀵슬롯) 수납 가능 여부 확인
            if (_consumableInventory != null && !_consumableInventory.CanAddConsumable(target, 1))
            {
                reason = CraftFailureReason.InventoryFull;
                return false;
            }

            // 필요 재료 충분 여부 확인
            if (recipe.MaterialCosts != null)
            {
                for (int i = 0; i < recipe.MaterialCosts.Length; i++)
                {
                    MaterialCost cost = recipe.MaterialCosts[i];
                    if (cost.ItemData != null && cost.Amount > 0)
                    {
                        if (!_materialStorage.HasItem(cost.ItemData, cost.Amount))
                        {
                            reason = CraftFailureReason.InsufficientMaterials;
                            return false;
                        }
                    }
                }
            }

            reason = CraftFailureReason.None;
            return true;
        }

        /// <summary>
        /// 소모품 제작을 시도합니다.
        /// 재료 차감 및 소모품 지급을 원자적(Atomic)으로 수행합니다.
        /// </summary>
        public bool TryCraft(ConsumableData target, out CraftFailureReason reason)
        {
            if (!CanCraft(target, out reason))
            {
                return false;
            }

            if (!_recipeProvider.TryGetRecipe(target, out ConsumableCraftingCost recipe))
            {
                reason = CraftFailureReason.RecipeNotFound;
                return false;
            }

            // 1. 재료 차감
            if (recipe.MaterialCosts != null)
            {
                for (int i = 0; i < recipe.MaterialCosts.Length; i++)
                {
                    MaterialCost cost = recipe.MaterialCosts[i];
                    if (cost.ItemData != null && cost.Amount > 0)
                    {
                        if (!_materialStorage.TryRemoveItem(cost.ItemData, cost.Amount))
                        {
                            reason = CraftFailureReason.InsufficientMaterials;
                            return false;
                        }
                    }
                }
            }

            // 2. 소모품 인벤토리 지급
            if (_consumableInventory != null)
            {
                if (!_consumableInventory.TryAddConsumable(target, 1))
                {
                    reason = CraftFailureReason.InventoryFull;
                    return false;
                }
            }

            reason = CraftFailureReason.None;
            return true;
        }
    }
}
