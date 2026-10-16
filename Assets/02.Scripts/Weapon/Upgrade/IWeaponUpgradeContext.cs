using InventorySystem;

namespace ShotgunAndRun.Weapon
{
    /// <summary>
    /// 무기 강화에 필요한 영구 진행 데이터와 저장 경계를 제공합니다.
    /// </summary>
    public interface IWeaponUpgradeContext : IItemInventory
    {
        public WeaponUpgradeState UpgradeState
        {
            get;
        }
        public void CommitUpgrade();
    }
}
