using System;
using InventorySystem;

namespace ShotgunAndRun.Core
{
    /// <summary>
    /// 런 결과 정산(획득/유실)에 사용되는 아이템 및 수량 기록입니다.
    /// </summary>
    [Serializable]
    public readonly struct RunItemRecord
    {
        public ItemData Item
        {
            get;
        }
        public int Amount
        {
            get;
        }

        public RunItemRecord(ItemData item, int amount)
        {
            Item = item;
            Amount = amount;
        }
    }
}
