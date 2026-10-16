using ShotgunAndRun.Weapon;
using UnityEngine;
using UnityEngine.Serialization;

namespace ShotgunAndRun.Player
{
    // UI 버튼 또는 쉘터 상호작용에서 호출하는 파츠 강화 진입점입니다.
    [DisallowMultipleComponent]
    public sealed class WeaponUpgradeController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerWeapon _playerWeapon;
        [SerializeField] private WeaponUpgradeCatalogSO _upgradeCatalog;
        [FormerlySerializedAs("_inventoryProvider")]
        [Tooltip("IWeaponUpgradeContext를 구현한 GameManager를 할당합니다. 비어 있으면 자동 탐색합니다.")]
        [SerializeField]
        private MonoBehaviour _upgradeContextProvider;

        [Header("Test Settings")]
        [SerializeField] private bool _isShelter = true;

        private WeaponUpgradeService _upgradeService;
        private IWeaponUpgradeContext _upgradeContext;

        private void Start()
        {
            InitializeService();
        }

        // Unity UI Button에서 직접 연결할 수 있는 메서드입니다.
        public void UpgradeBarrel()
        {
            TryUpgrade(WeaponPartsType.Barrel);
        }

        public void UpgradeChamber()
        {
            TryUpgrade(WeaponPartsType.Chamber);
        }

        public void UpgradeBelt()
        {
            TryUpgrade(WeaponPartsType.Belt);
        }

        public void UpgradeStock()
        {
            TryUpgrade(WeaponPartsType.Stock);
        }

        public void UpgradeAmmo()
        {
            TryUpgrade(WeaponPartsType.Ammo);
        }

        private bool TryUpgrade(WeaponPartsType parts)
        {
            if (!InitializeService())
            {
                return false;
            }

            if (!_upgradeService.TryUpgrade(parts, _isShelter))
            {
                Debug.Log($"{parts} 강화에 실패했습니다. 강화 레벨 : {_playerWeapon.State.GetLevel(parts)}", this);
                return false;
            }

            // 레벨 변경 후 런타임 능력치를 다시 계산합니다.
            _playerWeapon.RefreshStats();
            _playerWeapon.RefillMagazine();
            _upgradeContext.CommitUpgrade();

            int newLevel = _playerWeapon.State.GetLevel(parts);
            Debug.Log($"{parts} 강화 성공. 현재 레벨: {newLevel}", this);
            return true;
        }

        private bool InitializeService()
        {
            if (_upgradeService != null)
            {
                return true;
            }

            if (_playerWeapon == null)
            {
                Debug.LogError("PlayerWeapon이 할당되지 않았습니다.", this);
                return false;
            }

            if (_upgradeCatalog == null)
            {
                Debug.LogError("WeaponUpgradeCatalogSO가 할당되지 않았습니다.", this);
                return false;
            }

            if (_playerWeapon.State == null)
            {
                Debug.LogError(
                    "PlayerWeapon이 초기화되기 전에 업그레이드가 호출되었습니다.",
                    this);
                return false;
            }

            IWeaponUpgradeContext upgradeContext = ResolveUpgradeContext();
            if (upgradeContext?.UpgradeState == null)
            {
                Debug.LogError(
                    "IWeaponUpgradeContext를 구현한 진행 데이터 공급자를 찾을 수 없습니다.",
                    this);
                return false;
            }

            _playerWeapon.SetUpgradeState(upgradeContext.UpgradeState);
            _upgradeContext = upgradeContext;

            _upgradeService = new WeaponUpgradeService(
                upgradeContext.UpgradeState,
                _upgradeCatalog,
                upgradeContext);

            return true;
        }

        private IWeaponUpgradeContext ResolveUpgradeContext()
        {
            if (_upgradeContextProvider is IWeaponUpgradeContext assignedContext)
            {
                return assignedContext;
            }

            MonoBehaviour[] candidates = GetComponentsInParent<MonoBehaviour>(true);
            foreach (MonoBehaviour candidate in candidates)
            {
                if (candidate is IWeaponUpgradeContext context)
                {
                    _upgradeContextProvider = candidate;
                    return context;
                }
            }

            candidates = FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (MonoBehaviour candidate in candidates)
            {
                if (candidate is IWeaponUpgradeContext context)
                {
                    _upgradeContextProvider = candidate;
                    return context;
                }
            }

            return null;
        }
    }
}
