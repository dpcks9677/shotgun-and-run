using System;
using System.Collections.Generic;
using UnityEngine;

namespace InventorySystem
{
    /// <summary>
    /// 인벤토리 데이터 및 핵심 비즈니스 로직을 관리하는 컴포넌트
    /// </summary>
    public class Inventory : MonoBehaviour, IItemInventory, IInventorySnapshotProvider, IInventoryCapacity
    {
        [Header("Capacity Settings")]
        [SerializeField, Min(1)] private int _maxCapacity = 20;

        [SerializeField, Min(0)] private int _initialUnlockedCapacity = 10;

        [Header("Slots (Runtime View)")]
        [SerializeField] private List<InventorySlot> _slots = new();

        public int MaxCapacity => _maxCapacity;
        public int UnlockedCapacity
        {
            get;
            private set;
        }
        public IReadOnlyList<InventorySlot> Slots => _slots;

        public event Action<int> OnSlotChanged;
        public event Action<int> OnCapacityChanged;

        private void Awake()
        {
            InitializeSlots();
        }

        public void InitializeSlots()
        {
            if (_slots == null)
            {
                _slots = new List<InventorySlot>();
            }

            _slots.Clear();
            UnlockedCapacity = Mathf.Clamp(_initialUnlockedCapacity, 0, _maxCapacity);

            for (int i = 0; i < _maxCapacity; i++)
            {
                bool isLocked = i >= UnlockedCapacity;
                _slots.Add(new InventorySlot(isLocked));
            }
        }

        #region Slot Unlock API (외부 시스템 호출용)

        public bool UnlockNextSlot()
        {
            return UnlockSlots(1) > 0;
        }

        public int UnlockSlots(int count)
        {
            if (count <= 0 || UnlockedCapacity >= _maxCapacity) return 0;

            int targetCapacity = Mathf.Min(_maxCapacity, UnlockedCapacity + count);
            int actualUnlockedCount = targetCapacity - UnlockedCapacity;

            for (int i = UnlockedCapacity; i < targetCapacity; i++)
            {
                _slots[i].Unlock();
                OnSlotChanged?.Invoke(i);
            }

            UnlockedCapacity = targetCapacity;
            OnCapacityChanged?.Invoke(UnlockedCapacity);

            return actualUnlockedCount;
        }

        public void SetUnlockedCapacity(int targetCapacity)
        {
            int clamped = Mathf.Clamp(targetCapacity, 0, _maxCapacity);
            if (clamped > UnlockedCapacity)
            {
                UnlockSlots(clamped - UnlockedCapacity);
            }
        }

        #endregion

        #region Item Management API

        public bool AddItem(ItemData itemData, int amount = 1)
        {
            if (itemData == null || amount <= 0) return false;

            int remainingAmount = amount;

            // 1. 기존 중첩 가능 슬롯 우선 분배
            if (itemData.MaxStack > 1)
            {
                for (int i = 0; i < UnlockedCapacity; i++)
                {
                    var slot = _slots[i];
                    if (!slot.IsEmpty && slot.ItemData == itemData && slot.Quantity < itemData.MaxStack)
                    {
                        remainingAmount = slot.AddQuantity(remainingAmount);
                        OnSlotChanged?.Invoke(i);

                        if (remainingAmount <= 0) return true;
                    }
                }
            }

            // 2. 빈 슬롯에 분배
            for (int i = 0; i < UnlockedCapacity; i++)
            {
                var slot = _slots[i];
                if (slot.IsEmpty)
                {
                    int toAdd = Mathf.Min(remainingAmount, itemData.MaxStack);
                    slot.SetItem(itemData, toAdd);
                    remainingAmount -= toAdd;
                    OnSlotChanged?.Invoke(i);

                    if (remainingAmount <= 0) return true;
                }
            }

            return remainingAmount == 0;
        }

        public int GetItemCount(ItemData itemData)
        {
            if (itemData == null)
            {
                return 0;
            }

            int total = 0;
            for (int i = 0; i < UnlockedCapacity; i++)
            {
                InventorySlot slot = _slots[i];
                if (!slot.IsEmpty && slot.ItemData == itemData)
                {
                    total += slot.Quantity;
                }
            }

            return total;
        }

        public bool HasItem(ItemData itemData, int amount)
        {
            return itemData != null &&
                   amount > 0 &&
                   GetItemCount(itemData) >= amount;
        }

        public bool TryRemoveItem(ItemData itemData, int amount)
        {
            if (!HasItem(itemData, amount))
            {
                return false;
            }

            int remainingAmount = amount;
            for (int i = 0; i < UnlockedCapacity && remainingAmount > 0; i++)
            {
                InventorySlot slot = _slots[i];
                if (slot.IsEmpty || slot.ItemData != itemData)
                {
                    continue;
                }

                int removed = slot.RemoveQuantity(remainingAmount);
                remainingAmount -= removed;
                OnSlotChanged?.Invoke(i);
            }

            return remainingAmount == 0;
        }

        public bool RemoveItemAt(int slotIndex, int amount = 1)
        {
            if (slotIndex < 0 || slotIndex >= UnlockedCapacity || amount <= 0) return false;

            var slot = _slots[slotIndex];
            if (slot.IsEmpty) return false;

            slot.RemoveQuantity(amount);
            OnSlotChanged?.Invoke(slotIndex);
            return true;
        }

        /// <summary>
        /// 특정 아이템을 이 인벤토리에 몇 개까지 추가할 수 있는지(남은 수용 공간) 계산합니다. (단일 루프로 최적화)
        /// </summary>
        public int GetAvailableSpace(ItemData itemData)
        {
            if (itemData == null) return 0;

            int capacity = UnlockedCapacity;
            int maxStack = Mathf.Max(1, itemData.MaxStack);
            int totalSpace = 0;

            for (int i = 0; i < capacity; i++)
            {
                var slot = _slots[i];
                if (slot.IsEmpty)
                {
                    totalSpace += maxStack;
                }
                else if (maxStack > 1 && slot.ItemData == itemData && slot.Quantity < maxStack)
                {
                    totalSpace += (maxStack - slot.Quantity);
                }
            }

            return totalSpace;
        }

        public bool SwapSlots(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= UnlockedCapacity ||
                toIndex < 0 || toIndex >= UnlockedCapacity ||
                fromIndex == toIndex)
            {
                return false;
            }

            var fromSlot = _slots[fromIndex];
            var toSlot = _slots[toIndex];

            if (fromSlot.IsLocked || toSlot.IsLocked) return false;

            if (!fromSlot.IsEmpty && !toSlot.IsEmpty &&
                fromSlot.ItemData == toSlot.ItemData &&
                toSlot.Quantity < toSlot.ItemData.MaxStack)
            {
                int remaining = toSlot.AddQuantity(fromSlot.Quantity);
                if (remaining <= 0)
                {
                    fromSlot.Clear();
                }
                else
                {
                    fromSlot.SetItem(fromSlot.ItemData, remaining);
                }
            }
            else
            {
                ItemData tempItem = fromSlot.ItemData;
                int tempQuantity = fromSlot.Quantity;

                fromSlot.SetItem(toSlot.ItemData, toSlot.Quantity);
                toSlot.SetItem(tempItem, tempQuantity);
            }

            OnSlotChanged?.Invoke(fromIndex);
            OnSlotChanged?.Invoke(toIndex);
            return true;
        }

        /// <summary>
        /// 서로 다른 두 인벤토리 간의 슬롯 맞교환 또는 이동/중첩을 처리합니다.
        /// </summary>
        public bool SwapBetweenInventories(int fromIndex, Inventory targetInventory, int toIndex)
        {
            if (targetInventory == null || fromIndex < 0 || fromIndex >= UnlockedCapacity ||
                toIndex < 0 || toIndex >= targetInventory.UnlockedCapacity)
            {
                return false;
            }

            // 동일 인벤토리 내부인 경우 기존 SwapSlots 사용
            if (this == targetInventory)
            {
                return SwapSlots(fromIndex, toIndex);
            }

            var fromSlot = _slots[fromIndex];
            var toSlot = targetInventory._slots[toIndex];

            if (fromSlot.IsLocked || toSlot.IsLocked || fromSlot.IsEmpty)
            {
                return false;
            }

            // 1. 동일 아이템이고 도착 슬롯에 여유 공간이 있는 경우 -> 스택 합치기
            if (!toSlot.IsEmpty && fromSlot.ItemData == toSlot.ItemData && toSlot.Quantity < toSlot.ItemData.MaxStack)
            {
                int remaining = toSlot.AddQuantity(fromSlot.Quantity);
                if (remaining <= 0)
                {
                    fromSlot.Clear();
                }
                else
                {
                    fromSlot.SetItem(fromSlot.ItemData, remaining);
                }
            }
            // 2. 그 외의 경우 (도착 슬롯이 비어있거나, 다른 아이템이 들어있는 경우) -> 맞교환 (Swap / Move)
            else
            {
                ItemData tempItem = fromSlot.ItemData;
                int tempQty = fromSlot.Quantity;

                fromSlot.SetItem(toSlot.ItemData, toSlot.Quantity);
                toSlot.SetItem(tempItem, tempQty);
            }

            OnSlotChanged?.Invoke(fromIndex);
            targetInventory.OnSlotChanged?.Invoke(toIndex);
            return true;
        }

        /// <summary>
        /// 다른 인벤토리로 특정 슬롯의 아이템을 안전하게 전송합니다 (루팅 / 보관함 기능).
        /// 공간이 부족하면 들어갈 수 있는 수량만 전송하고 남은 수량은 원래 슬롯에 유지합니다.
        /// </summary>
        public bool TransferItem(int fromIndex, Inventory targetInventory, int amount = -1)
        {
            if (targetInventory == null || fromIndex < 0 || fromIndex >= UnlockedCapacity) return false;

            var slot = _slots[fromIndex];
            if (slot.IsEmpty) return false;

            int requestedAmount = (amount <= 0 || amount > slot.Quantity) ? slot.Quantity : amount;
            ItemData item = slot.ItemData;

            int availableSpace = targetInventory.GetAvailableSpace(item);
            if (availableSpace <= 0)
            {
                return false;
            }

            int transferAmount = Mathf.Min(requestedAmount, availableSpace);
            bool added = targetInventory.AddItem(item, transferAmount);
            if (added)
            {
                RemoveItemAt(fromIndex, transferAmount);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 이 인벤토리에 들어있는 모든 아이템을 대상 인벤토리로 안전하게 전송합니다 (상자 모두 획득 기능).
        /// 대상 공간이 가득 차면 남은 아이템은 이 인벤토리에 안전하게 남깁니다.
        /// </summary>
        public int TransferAll(Inventory targetInventory)
        {
            if (targetInventory == null) return 0;

            int capacity = UnlockedCapacity;
            int transferredSlots = 0;

            for (int i = 0; i < capacity; i++)
            {
                var slot = _slots[i];
                if (slot.IsEmpty) continue;

                ItemData item = slot.ItemData;
                int qty = slot.Quantity;

                int availableSpace = targetInventory.GetAvailableSpace(item);
                if (availableSpace <= 0) continue;

                int toTransfer = Mathf.Min(qty, availableSpace);
                if (targetInventory.AddItem(item, toTransfer))
                {
                    int remaining = qty - toTransfer;
                    if (remaining <= 0)
                    {
                        slot.Clear();
                    }
                    else
                    {
                        slot.SetItem(item, remaining);
                    }

                    OnSlotChanged?.Invoke(i);
                    transferredSlots++;
                }
            }

            return transferredSlots;
        }

        public bool IsEmpty()
        {
            for (int i = 0; i < UnlockedCapacity; i++)
            {
                if (!_slots[i].IsEmpty) return false;
            }

            return true;
        }

        #region Snapshot & Clearing (IInventorySnapshotProvider)

        public IReadOnlyList<ItemAmountSnapshot> GetItemSnapshots()
        {
            var list = new List<ItemAmountSnapshot>();
            for (int i = 0; i < UnlockedCapacity; i++)
            {
                var slot = _slots[i];
                if (!slot.IsEmpty && slot.ItemData != null && slot.Quantity > 0)
                {
                    list.Add(new ItemAmountSnapshot(slot.ItemData, slot.Quantity));
                }
            }

            return list;
        }

        public void ClearAllItems()
        {
            for (int i = 0; i < UnlockedCapacity; i++)
            {
                if (!_slots[i].IsEmpty)
                {
                    _slots[i].Clear();
                    OnSlotChanged?.Invoke(i);
                }
            }
        }

        #endregion

        #endregion
    }
}
