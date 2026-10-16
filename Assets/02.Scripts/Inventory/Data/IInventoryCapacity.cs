namespace InventorySystem
{
    /// <summary>
    /// 인벤토리의 슬롯 용량 조회 및 해금 용량 동기화를 지원하는 인터페이스입니다.
    /// (ShotgunAndRun.Inventory.Data 어셈블리에 위치하여 Player 어셈블리와 결합도 없이 연동 지원)
    /// </summary>
    public interface IInventoryCapacity
    {
        public int UnlockedCapacity
        {
            get;
        }

        public int MaxCapacity
        {
            get;
        }

        public void SetUnlockedCapacity(int targetCapacity);
    }
}
