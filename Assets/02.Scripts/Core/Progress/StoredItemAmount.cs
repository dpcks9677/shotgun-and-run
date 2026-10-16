using System;

namespace ShotgunAndRun.Core
{
    [Serializable]
    public struct StoredItemAmount
    {
        public StoredItemAmount(string itemId, int amount)
        {
            ItemId = itemId;
            Amount = amount;
        }

        public string ItemId;
        public int Amount;
    }
}
