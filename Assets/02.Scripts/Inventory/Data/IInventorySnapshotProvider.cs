using System;
using System.Collections.Generic;

namespace InventorySystem
{
    /// <summary>
    /// 인벤토리 슬롯에 담긴 특정 아이템과 수량 스냅샷입니다. (결과 정산 및 보관함 이동용)
    /// </summary>
    [Serializable]
    public readonly struct ItemAmountSnapshot
    {
        public ItemData Item
        {
            get;
        }
        public int Amount
        {
            get;
        }

        public ItemAmountSnapshot(ItemData item, int amount)
        {
            Item = item;
            Amount = amount;
        }
    }

    /// <summary>
    /// 외부 시스템(결과 정산, 탈출 보관, 사망 유실 처리)에서 인벤토리 아이템 목록을 읽고 정리할 수 있는 인터페이스입니다.
    /// </summary>
    public interface IInventorySnapshotProvider
    {
        public IReadOnlyList<ItemAmountSnapshot> GetItemSnapshots();
        public void ClearAllItems();
    }
}
