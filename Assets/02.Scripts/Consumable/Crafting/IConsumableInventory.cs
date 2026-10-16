using System;

namespace Consumable
{
    public interface IConsumableInventory
    {
        public int SlotCount
        {
            get;
        }
        public bool CanAddConsumable(ConsumableData data, int amount = 1);
        public bool TryAddConsumable(ConsumableData data, int amount = 1);
    }
}
