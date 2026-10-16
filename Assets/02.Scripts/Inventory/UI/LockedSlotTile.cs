using UnityEngine;

namespace InventorySystem
{
    /// <summary>
    /// 인벤토리/상자 패널 격자의 9·10번째 칸에 놓는 항상 잠긴 슬롯 타일.
    /// 기능 슬롯과 같은 SlotUI를 쓰며, 잠금 데이터를 넣어 잠금 표시(검은 칸)를 그리고 드래그·드롭·하이라이트를 막는다.
    /// InventoryUI·LootChestUI가 만들지 않는 칸이라 소유 인벤토리는 연결하지 않는다.
    /// </summary>
    [RequireComponent(typeof(SlotUI))]
    public class LockedSlotTile : MonoBehaviour
    {
        private void Start()
        {
            GetComponent<SlotUI>().UpdateView(new InventorySlot(true));
        }
    }
}
