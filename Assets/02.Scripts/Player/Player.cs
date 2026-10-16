using Consumable;
using InventorySystem;
using ShotgunAndRun.Weapon;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace ShotgunAndRun.Player
{
    // 동일한 GameObject에 Player 컴포넌트를 두 개 이상 추가하지 못하게 함
    [DisallowMultipleComponent]
    // Player가 동작하기 위해 필요한 컴포넌트를 지정한다.
    // Player를 GameObject에 추가하면 Unity가 부족한 컴포넌트를 함께 추가한다.
    [RequireComponent(typeof(PlayerInputReader))] // 키보드와 마우스 입력을 읽는 클래스
    [RequireComponent(typeof(PlayerMovement))] // 플레이어의 이동 속도를 계산하는 클래스
    [RequireComponent(typeof(PlayerDash))] // 대시와 관련된 클래스
    [RequireComponent(typeof(PlayerMotor))] // Rigidbody2D를 통해 플레이어에게 물리 연산을 수행하는 클래스
    [RequireComponent(typeof(PlayerHealth))] // 플레이어 체력 / 사망 등을 관리하는 클래스
    [RequireComponent(typeof(PlayerInvulnerability))] // 플레이어 무적 시간과 관련된 클래스
    [RequireComponent(typeof(PlayerAim))] // 플레이어의 Aim(마우스 포인터)와 관련된 클래스
    [RequireComponent(typeof(PlayerWeapon))] // 플레이어의 무기 발사와 관련된 클래스
    [RequireComponent(typeof(PlayerFeedback))]
    [RequireComponent(typeof(PlayerConsumableInventory))] // 플레이어 소모품 퀵슬롯 관리 클래스

    // 플레이어 관련 기능을 초기화하고 연결하는 역할
    public sealed class Player : MonoBehaviour
    {
        private static Player _instance;
        public static Player PlayerInstance => _instance;

        [FormerlySerializedAs("_data")]
        [Header("Configuration")]
        [SerializeField] private PlayerDataSO _playerData;
        [SerializeField] private ShotgunDataSO _shotgunData;
        [SerializeField] private WeaponUpgradeState _state;

        [Header("Components")]
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private PlayerMovement _movement;
        [SerializeField] private PlayerDash _dash;
        [SerializeField] private PlayerMotor _motor;
        [SerializeField] private PlayerHealth _health;
        [SerializeField] private PlayerInvulnerability _invulnerability;
        [SerializeField] private PlayerAim _aim;
        [SerializeField] private PlayerWeapon _weapon;
        [SerializeField] private PlayerFeedback _feedback;
        [SerializeField] private PlayerConsumableInventory _consumableInventory;
        [SerializeField] private ConsumableSelectionController _selectionController;
        [SerializeField] private ConsumableEffectService _effectService;
        [SerializeField] private Light2D _playerLight;

        private IInventoryCapacity _inventoryCapacity;

        public PlayerDataSO PlayerData => _playerData;
        public ShotgunDataSO ShotgunData => _shotgunData;

        public PlayerRuntimeStats RuntimeStats
        {
            get;
            private set;
        }

        public PlayerHealth Health => _health;
        public PlayerDash Dash => _dash;
        public PlayerAim Aim => _aim;
        public PlayerMovement Movement => _movement;
        public PlayerInvulnerability Invulnerability => _invulnerability;
        public PlayerConsumableInventory ConsumableInventory => _consumableInventory;
        public IInventoryCapacity InventoryCapacity => _inventoryCapacity;
        public PlayerWeapon Weapon => _weapon;

        public void RefillWeaponMagazine(bool skipFirstCriticalShot = false)
        {
            _weapon?.RefillMagazine(skipFirstCriticalShot);
        }

        public void SetWeaponUpgradeState(WeaponUpgradeState state)
        {
            if (state == null)
            {
                Debug.LogError("WeaponUpgradeState is null.", this);
                return;
            }

            _state = state;
            _weapon.SetUpgradeState(state);
        }

        public void ApplyPlayerLevel(int level)
        {
            int safeLevel = Mathf.Max(1, level);

            if (RuntimeStats != null)
            {
                RuntimeStats.SetMoveSpeedMultiplier(
                    PlayerLevelRules.LevelStatSourceId,
                    PlayerLevelRules.GetMoveSpeedMultiplier(safeLevel));

                RuntimeStats.SetMaxHealthBonus(
                    PlayerLevelRules.LevelStatSourceId,
                    PlayerLevelRules.GetMaxHealthBonus(safeLevel));
            }

            if (_health != null)
            {
                _health.SetHealth(_health.MaxHealth);
            }

            if (_dash != null)
            {
                _dash.SetChargeTimeReduction(PlayerLevelRules.GetDashChargeReduction(safeLevel));
            }

            _inventoryCapacity ??= GetComponent<IInventoryCapacity>();
            _inventoryCapacity?.SetUnlockedCapacity(PlayerLevelRules.GetBagCapacity(safeLevel));
        }

        // Subscribe가 호출되어 이벤트가 이미 등록되었는지를 나타냄
        private bool _subscribed;

        private void Awake()
        {
            // 비동기 씬 전환에서는 새 씬의 Awake가 이전 씬 언로드보다 먼저 실행되므로
            // 같은 씬 안의 중복만 제거하고, 언로드될 이전 씬의 플레이어는 새 플레이어로 교체합니다.
            if (_instance != null && _instance.gameObject.scene == gameObject.scene)
            {
                Destroy(gameObject);

                return;
            }

            _instance = this;

            ResolveComponents();
            SetDungeonLight(false);

            if (_playerData == null)
            {
                Debug.LogError("PlayerDataSO is not assigned.", this);
                DisableGameplay();
                return;
            }

            RuntimeStats = new PlayerRuntimeStats(_playerData);

            // 각 플레이어 기능과 관련된 클래스의 초기화 함수를 호추
            _movement.Initialize(RuntimeStats);
            _dash.Initialize(RuntimeStats);
            _invulnerability.Initialize();
            _health.Initialize(RuntimeStats, _invulnerability);
            _motor.Initialize(_movement, _dash);
            _weapon.Initialize(_shotgunData, _state, _dash);

            // 입력과 조준의 초기화 성공 여부를 저장
            bool inputReady = _input.Initialize();
            _aim.SetMuzzle(_weapon.FirePoint);
            bool aimReady = _aim.Initialize();
            Subscribe();

            // 이벤트를 구독하기 전에 이미 입력값이 설정되었을 가능성이 있으므로 현재 값을 직접 전달
            HandleMoveChanged(_input.MoveInput);
            HandleAimChanged(_input.PointerScreenPosition);

            if (!inputReady || !aimReady)
            {
                Debug.LogError("Player could not initialize all required dependencies.", this);
                DisableGameplay();
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Reset()
        {
            ResolveComponents();
        }

        // Inspector 창에서 스크립트의 속성(프로퍼티 값)이 수정될 때마다 호출되는 함수
        private void OnValidate()
        {
            ResolveComponents();
        }

        // 직접 Inspector에서 할당하지 않아도 GetComponent로 초기화
        private void ResolveComponents()
        {
            _input ??= GetComponent<PlayerInputReader>();
            _movement ??= GetComponent<PlayerMovement>();
            _dash ??= GetComponent<PlayerDash>();
            _motor ??= GetComponent<PlayerMotor>();
            _health ??= GetComponent<PlayerHealth>();
            _invulnerability ??= GetComponent<PlayerInvulnerability>();
            _aim ??= GetComponent<PlayerAim>();
            _weapon ??= GetComponent<PlayerWeapon>();
            _feedback ??= GetComponent<PlayerFeedback>();
            _consumableInventory ??= GetComponent<PlayerConsumableInventory>();
            _selectionController ??= GetComponentInChildren<ConsumableSelectionController>();
            _effectService ??= GetComponentInChildren<ConsumableEffectService>();
            _playerLight ??= GetComponent<Light2D>();
            _inventoryCapacity ??= GetComponent<IInventoryCapacity>();

            if (_consumableInventory != null && _selectionController != null)
            {
                _consumableInventory.SetSelectionController(_selectionController);
                _selectionController.SetInventory(_consumableInventory);
            }
        }

        //입력, 대시, 사망 이벤트와 각 클래스에 정의된 Action에 처리 메서드를 연결
        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            _input.MoveChanged += HandleMoveChanged;
            _input.DashPressed += HandleDashPressed;
            _input.AimChanged += HandleAimChanged;
            _input.MoveDashPressed += HandleMoveDashPressed;
            _input.FirePressed += HandleFirePressed;
            _input.FireReleased += HandleFireReleased;
            _input.ReloadPressed += HandleReloadPressed;
            _dash.DashStarted += HandleDashStarted;
            _weapon.RecoilRequested += HandleRecoilRequested;
            _weapon.WeaponUsableChanged += HandleWeaponUsableChanged;
            _aim.FacingLeftChanged += _weapon.SetFacingLeft;

            _health.Died += HandleDied;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            _input.MoveChanged -= HandleMoveChanged;
            _input.AimChanged -= HandleAimChanged;
            _input.DashPressed -= HandleDashPressed;
            _input.MoveDashPressed -= HandleMoveDashPressed;
            _input.FirePressed -= HandleFirePressed;
            _input.FireReleased -= HandleFireReleased;
            _input.ReloadPressed -= HandleReloadPressed;
            _dash.DashStarted -= HandleDashStarted;
            _weapon.RecoilRequested -= HandleRecoilRequested;
            _weapon.WeaponUsableChanged -= HandleWeaponUsableChanged;
            _aim.FacingLeftChanged -= _weapon.SetFacingLeft;
            _health.Died -= HandleDied;
            _subscribed = false;
        }

        // PlayerInputReader가 전달한 WASD 입력을 PlayerMovement로 넘깁니다.
        private void HandleMoveChanged(Vector2 input)
        {
            _movement.SetInput(input);
        }

        // 마우스의 화면 좌표를 PlayerAim에 전달합니다.
        private void HandleAimChanged(Vector2 pointerScreenPosition)
        {
            _aim.SetPointerScreenPosition(pointerScreenPosition);
        }

        // 대시 버튼을 눌렀을 때 호출됩니다.
        private void HandleDashPressed()
        {
            if (!_health.IsDead)
            {
                _dash.TryStart(_aim.AimDirection);
            }
        }

        // 대시 시작 시 무적 활성화
        // 대시 이동 시간과 대시 무적 시간 중 긴 쪽만큼 유지되며, 시간이 지나면 자동으로 해제된다.
        private void HandleDashStarted(Vector2 direction)
        {
            _invulnerability.ActivateTimed(
                InvulnerabilityReason.Dash,
                Mathf.Max(RuntimeStats.DashDuration, RuntimeStats.DashInvincibilityDuration));
        }

        private void HandleMoveDashPressed(Vector2 direction)
        {
            if (_health.IsDead)
            {
                return;
            }

            // direction이 (0, 0)이면 PlayerDash가 대시를 거부함
            _dash.TryStart(direction);
        }

        // 마우스 좌클릭 (Fire) Action이 발생했을 떄 호출
        private void HandleFirePressed()
        {
            if (_health.IsDead)
            {
                return;
            }

            if (_selectionController != null &&
                _selectionController.IsConsumableSelected())
            {
                _selectionController.UseConsumable();
                return; // 소모품 사용 시 샷건은 발사하지 않음
            }


            _weapon.TryFire(_aim.WeaponAimDirection, _aim.AimDistance);
        }

        private void HandleFireReleased()
        {
            // 힐 킷은 마우스 버튼을 놓으면 2초 시전이 완료되지 않은 경우 취소합니다.
            // ConsumableSelectionController가 CanceledConsumable 이벤트를 받아 선택 상태도 정리합니다.
            if (_input != null && _input.IsFireHeld)
            {
                return;
            }

            if (_effectService != null && _effectService.IsUsingHealKit)
            {
                _effectService.CancelActiveConsumable();
            }
        }

        private void HandleReloadPressed()
        {
            if (_health.IsDead)
            {
                return;
            }

            _weapon.TryReload();
        }

        // 사격시 반동 처리
        private void HandleRecoilRequested(
            Vector2 direction,
            float distance)
        {
            _motor.ApplyRecoil(direction, distance);
        }

        // 사망 처리
        // 총이 보이지 않을 때(컷씬에서 숨김 · 맨손 구간)는 팔이 없는 스프라이트로 바꾼다.
        // 사망한 뒤에는 맨손 애니메이터에 사망 모습이 없으므로 그대로 둔다
        private void HandleWeaponUsableChanged(bool usable)
        {
            if (_health.IsDead)
            {
                return;
            }

            _feedback.SetWeaponUsable(usable);
            _feedback.SetShelterMode(!usable);
        }

        private void HandleDied()
        {
            _input.SetGameplayEnabled(false);
            _movement.SetMovementEnabled(false);
            _dash.SetDashEnabled(false);
            _motor.SetMotorEnabled(false);
        }

        private void DisableGameplay()
        {
            if (_input != null)
            {
                _input.SetGameplayEnabled(false);
            }

            if (_movement != null)
            {
                _movement.SetMovementEnabled(false);
            }

            if (_dash != null)
            {
                _dash.SetDashEnabled(false);
            }

            if (_motor != null)
            {
                _motor.SetMotorEnabled(false);
            }

            enabled = false;
        }

        public void SetShelterMode(bool isShelter)
        {
            _weapon.SetWeaponUsable(!isShelter);
            _feedback.SetWeaponUsable(!isShelter);
            _feedback.SetShelterMode(isShelter);
            _effectService.SetConsumableUsable(!isShelter);
        }

        public void SetDungeonLight(bool isDungeon)
        {
            if (_playerLight != null)
            {
                _playerLight.enabled = isDungeon;
            }
        }
    }
}
