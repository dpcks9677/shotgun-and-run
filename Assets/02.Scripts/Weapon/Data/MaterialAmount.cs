using System;
using InventorySystem;
using UnityEngine;

namespace ShotgunAndRun.Weapon
{
    [Serializable]
    public struct MaterialAmount
    {
        [SerializeField] private ItemData _item;
        [SerializeField, Min(1)] private int _amount;

        public MaterialAmount(ItemData item, int amount)
        {
            _item = item;
            _amount = Mathf.Max(0, amount);
        }

        public ItemData Item => _item;
        public int Amount => _amount;
        public bool IsValid => _item != null && _amount > 0;
    }
}
