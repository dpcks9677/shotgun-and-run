using System;
using InventorySystem;
using UnityEngine;

namespace Consumable
{
    [Serializable]
    public struct ConsumableCraftingCost
    {
        public ConsumableData _consumableData;
        public MaterialCost[] _MaterialCosts;

        public ConsumableData ConsumableData => _consumableData;
        public MaterialCost[] MaterialCosts => _MaterialCosts;

        public ConsumableCraftingCost(ConsumableData consumableData, MaterialCost[] materialCosts)
        {
            _consumableData = consumableData;
            _MaterialCosts = materialCosts;
        }
    }

    [Serializable]
    public struct MaterialCost
    {
        public ItemData _itemData;
        public int amount;

        public ItemData ItemData => _itemData;
        public int Amount => amount;

        public MaterialCost(ItemData itemData, int amount)
        {
            _itemData = itemData;
            this.amount = amount;
        }
    }
}
