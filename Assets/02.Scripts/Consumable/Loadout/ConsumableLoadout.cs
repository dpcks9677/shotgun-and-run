using UnityEngine;

namespace Consumable
{
    public class ConsumableLoadout : MonoBehaviour
    {
        private ConsumableSlot[] _slots;
        public ConsumableSlot[] Slots => _slots;

        private int _slotLength = 3;
        public int SlotLength => _slotLength;

        private void Awake()
        {
            _slots = new ConsumableSlot[_slotLength];

            for (int i = 0; i < _slotLength; i++)
            {
                _slots[i] = new ConsumableSlot();
            }
        }

        public void EquipConsumable(int index, int quantity, ConsumableData consumable)
        {
            ConsumableSlot slot = new ConsumableSlot();
            slot.SetConsumable(consumable, quantity);
            _slots[index] = slot;
        }

        public ConsumableData ChangeSelectedConsumable(int index)
        {
            if (_slots == null || index < 0 || index >= _slots.Length)
            {
                return null;
            }

            return _slots[index].SetSelected();
        }

        public void DeselectConsumable()
        {
            if (_slots == null)
            {
                return;
            }

            foreach (ConsumableSlot slot in _slots)
            {
                if (slot == null)
                {
                    continue;
                }

                slot.SetDeselected();
            }
        }

        public bool UsedConsumable(ConsumableData selectedConsumable)
        {
            if (_slots == null)
            {
                return false;
            }

            foreach (ConsumableSlot slot in _slots)
            {
                if (slot.ConsumableData == selectedConsumable)
                {
                    int result = slot.RemoveQuantity(1);
                    return result == 1 ? true : false;
                }
            }

            return false;
        }

        public int GetQuantity(ConsumableData selectedConsumable)
        {
            if (_slots == null)
            {
                return 0;
            }

            foreach (ConsumableSlot slot in _slots)
            {
                if (slot.ConsumableData == selectedConsumable)
                {
                    return slot.Quantity;
                }
            }

            return 0;
        }

        public int GetUsedSlotIndex()
        {
            if (_slots == null)
            {
                return -1;
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].IsSelected)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
