using System;
using UnityEngine;

namespace InventorySystem
{
    /// <summary>
    /// 개별 슬롯의 런타임 데이터 (아이템, 수량, 잠금 상태)
    /// </summary>
    [Serializable]
    public class InventorySlot
    {
        [SerializeField] private ItemData _itemData;
        [SerializeField] private int _quantity;
        [SerializeField] private bool _isLocked;

        public ItemData ItemData => _itemData;
        public int Quantity => _quantity;
        public bool IsLocked => _isLocked;

        /// <summary>
        /// 슬롯이 해금되어 있고, 비어있는지 여부
        /// </summary>
        public bool IsEmpty => !_isLocked && (_itemData == null || _quantity <= 0);

        public InventorySlot(bool isLocked = false)
        {
            _isLocked = isLocked;
            _itemData = null;
            _quantity = 0;
        }

        public void Unlock()
        {
            _isLocked = false;
        }

        public void Lock()
        {
            _isLocked = true;
            _itemData = null;
            _quantity = 0;
        }

        public void SetItem(ItemData itemData, int quantity)
        {
            if (_isLocked) return;

            _itemData = itemData;
            _quantity = Mathf.Clamp(quantity, 0, itemData != null ? itemData.MaxStack : 0);

            if (_quantity <= 0)
            {
                Clear();
            }
        }

        public int AddQuantity(int amount)
        {
            if (_isLocked || _itemData == null || amount <= 0) return amount;

            int spaceLeft = _itemData.MaxStack - _quantity;
            int added = Mathf.Min(spaceLeft, amount);
            _quantity += added;

            return amount - added;
        }

        public int RemoveQuantity(int amount)
        {
            if (_isLocked || _itemData == null || amount <= 0) return 0;

            int removed = Mathf.Min(_quantity, amount);
            _quantity -= removed;

            if (_quantity <= 0)
            {
                Clear();
            }

            return removed;
        }

        public void Clear()
        {
            _itemData = null;
            _quantity = 0;
        }
    }
}
