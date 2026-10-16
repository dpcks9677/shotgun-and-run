using System;
using System.Collections;
using System.Collections.Generic;
using Consumable;
using ShotgunAndRun.Weapon;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ShotgunAndRun.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerWeapon : MonoBehaviour, IPelletEmitter
    {
        private ShotgunDataSO _data;
        private ShotgunRuntimeStats _stats;
        public ShotgunRuntimeStats Stats => _stats;
        private WeaponUpgradeState _state;
        public WeaponUpgradeState State => _state;
        private PlayerDash _dash;
        private ConsumableSelectionController _selectionController;
        private SpreadArcIndicator _spreadArcIndicator;
        private ExplosionIndicator _explosionIndicator;

        [SerializeField] private Transform _firePoint;
        [SerializeField] private ProjectTilePool _projectilePool;
        [SerializeField] private SpriteRenderer _weaponSprite;
        [SerializeField] private ThrowGrenade _throwGrenade;

        private int _currentMagazine;

        // 탄창에 장전된 탄을 아래(먼저 넣은 탄)부터 나열하고, 크리티컬 확정 탄이면 true
        private readonly List<bool> _criticalShells = new();
        private int _shellsSinceCritical;

        [Header("Spread")]
        [SerializeField, Min(0f)] private float _pelletJitterAngle = 1.5f;

        private float _nextFireTime;
        private Coroutine _reloadCoroutine;
        private bool _isReloading;
        private bool _isShootng;
        private bool _isWeaponUsable = true;
        private bool _isShelter = false;
        private bool _chestInteractionLocked;

        public event Action<bool> Fired;
        public event Action<Vector2> CriticalHitConfirmed;
        public event Action ReloadStarted;
        public event Action Reloading;
        public event Action ReloadEnded;
        public event Action ReloadCanceled;
        public event Action PumpAction;
        public event Action<Vector2, float> RecoilRequested;
        public event Action<int> AmmoChanged;
        public event Action NoAmmo;
        public event Action<bool> WeaponUsableChanged;

        public int CurrentMagazine => _currentMagazine;
        public bool IsReloading => _isReloading;
        public Transform FirePoint => _firePoint;
        public bool IsWeaponUsable =>
            _isWeaponUsable && !_chestInteractionLocked;

        // 탄창 아래에서 position번째(1부터)로 장전된 탄이 크리티컬 확정 탄인지
        public bool IsCriticalShell(int position)
        {
            return position >= 1 && position <= _criticalShells.Count && _criticalShells[position - 1];
        }

        // 무기 스프라이트가 flipY로 뒤집히면 총구 그림은 조준 축의 반대편으로 옮겨 가지만
        // 총구 Transform은 그대로라서 발사 위치와 산탄 인디케이터 중심이 어긋납니다.
        // 그래서 왼쪽을 볼 때는 총구의 로컬 y를 반전합니다. (프리팹의 값은 오른쪽을 볼 때 기준)
        public void SetFacingLeft(bool isFacingLeft)
        {
            if (_firePoint == null)
            {
                return;
            }

            Vector3 localPosition = _firePoint.localPosition;
            float magnitude = Mathf.Abs(localPosition.y);
            localPosition.y = isFacingLeft ? -magnitude : magnitude;
            _firePoint.localPosition = localPosition;
        }

        // 산탄 반각 (도). 총열 레벨로만 정해지며, 발사와 산탄 범위 인디케이터가 같이 사용합니다.
        public float SpreadHalfAngle => _stats != null ? _stats.SpreadAngle * 0.5f : 0f;

        public void Initialize(ShotgunDataSO data, WeaponUpgradeState state, PlayerDash dash)
        {
            if (data == null || state == null)
            {
                Debug.LogError("무기 데이터 또는 강화 상태가 없습니다.", this);
                return;
            }

            _data = data;
            _state = state;
            _dash = dash;
            _stats = new ShotgunRuntimeStats(data, _state);
            _projectilePool = GetComponentInChildren<ProjectTilePool>();
            // 순서에 기대지 않고 샷건 스프라이트(PlayerWeaponFeedback이 붙은 WeaponSprite)를 직접 찾습니다.
            _weaponSprite = GetComponentInChildren<PlayerWeaponFeedback>(true).GetComponent<SpriteRenderer>();
            _selectionController = GetComponentInChildren<ConsumableSelectionController>();
            _spreadArcIndicator = GetComponentInChildren<SpreadArcIndicator>();
            _explosionIndicator = GetComponentInChildren<ExplosionIndicator>(true);
            _throwGrenade = GetComponentInChildren<ThrowGrenade>(true);

            _stats.StatsRecalculated += HandleStatsRecalculated;
            _dash.DashStarted += AtDashCancelReload;
            _selectionController.OnSelectedConsumable += HandleConsumableSelected;
            _selectionController.OnDeselectedConsumable += HandleConsumableDeselected;
            _currentMagazine = _stats.MagazineSize;
            _nextFireTime = 0f;
            _isReloading = false;
            _isShelter = false;
            _isWeaponUsable = true;
            RebuildCriticalShells();

            AmmoChanged?.Invoke(_currentMagazine);
        }

        public void SetUpgradeState(WeaponUpgradeState state)
        {
            if (state == null)
            {
                Debug.LogError("WeaponUpgradeState가 없습니다.", this);
                return;
            }

            _state = state;
            RefreshStats();
            RefillMagazine();
        }

        public void TryFire(Vector2 direction, float aimDistance)
        {
            if (_stats == null ||
                _firePoint == null ||
                _projectilePool == null ||
                Player.PlayerInstance == null ||
                Player.PlayerInstance.Dash.IsDashing ||
                !_isWeaponUsable ||
                _chestInteractionLocked)
            {
                return;
            }

            if (_currentMagazine <= 0)
            {
                NoAmmo?.Invoke();
            }


            // 재장전 중 탄이 남아 있으면 좌클릭으로 재장전을 취소하고 발사합니다.
            // 탄이 하나도 없으면 재장전을 취소하지 않습니다.
            if (_isReloading)
            {
                if (_currentMagazine <= 0)
                {
                    _spreadArcIndicator?.PlayNoAmmoFeedback();

                    return;
                }

                CancelReload();
            }

            if (Time.time < _nextFireTime || _currentMagazine <= 0)
            {
                if (_currentMagazine <= 0)
                {
                    _spreadArcIndicator?.PlayNoAmmoFeedback();
                }

                return;
            }

            Vector2 origin = _firePoint.position;
            int firedPelletCount = 0;
            bool hitEnemy = false;

            // 탄약 파츠 효과로 크리티컬이 확정된 탄(맨 위 탄)이거나, 크리 확률에 따른 크리티컬이면
            bool isCritical =
                IsCriticalShell(_currentMagazine) ||
                _stats.RollCritical();

            bool hasPenetration = _stats.HasPenetration;

            _isShootng = true;

            int pelletCount = _stats.PelletCount;
            ShotContext shotContext = new ShotContext(
                _stats.PelletDamage * (isCritical ? 1.5f : 1f),
                _stats.CriticalChance,
                isCritical,
                hasPenetration,
                _stats.MaxRange);
            for (int i = 0;
                 i < pelletCount;
                 i++)
            {
                Vector2 spreadDirection = ApplySpread(direction, i, pelletCount);

                if (_projectilePool.FireHitscan(
                        origin,
                        spreadDirection,
                        transform,
                        shotContext,
                        out bool pelletHitEnemy))
                {
                    firedPelletCount++;
                    hitEnemy |= pelletHitEnemy;
                }
            }

            _isShootng = false;

            Debug.Log($"Critical : {isCritical} / Damage : {(int)(_stats.PelletDamage * (isCritical ? 1.5f : 1f))}");
            if (firedPelletCount == 0) return;

            _nextFireTime = Time.time + _stats.FireInterval;
            _currentMagazine--;
            _criticalShells.RemoveRange(_currentMagazine, _criticalShells.Count - _currentMagazine);

            Debug.Log("남은 총알 : " + _currentMagazine);

            Vector2 recoilDirection = direction.sqrMagnitude > Mathf.Epsilon
                ? -direction.normalized
                : Vector2.zero;

            AmmoChanged?.Invoke(_currentMagazine);
            RecoilRequested?.Invoke(recoilDirection, _stats.RecoilDistance);
            Fired?.Invoke(isCritical);
            if (isCritical && hitEnemy)
            {
                Vector2 fireDirection = direction.sqrMagnitude > Mathf.Epsilon
                    ? direction.normalized
                    : Vector2.right;
                CriticalHitConfirmed?.Invoke(fireDirection);
            }
            PumpAction?.Invoke();

            isCritical = false;
        }

        public void TryReload()
        {
            if (_stats == null ||
                    _isReloading ||
                    _isShootng ||
                    !_isWeaponUsable ||
                    _chestInteractionLocked)
            {
                return;
            }

            int magazineCapacity = _stats.MagazineSize;

            // 탄창이 이미 가득 찼으면 재장전하지 않습니다.
            if (magazineCapacity <= 0 || _currentMagazine >= magazineCapacity)
            {
                return;
            }

            _reloadCoroutine = StartCoroutine(
                ReloadRoutine(magazineCapacity, _stats.ReloadDuration));
        }

        private IEnumerator ReloadRoutine(int magazineCapacity, float shellReloadInterval)
        {
            _isReloading = true;
            ReloadStarted?.Invoke();

            Debug.Log("재장전 시작");

            shellReloadInterval = Mathf.Max(0f, shellReloadInterval);

            WaitForSeconds shellLoadWait = shellReloadInterval > 0f
                ? new WaitForSeconds(shellReloadInterval)
                : null;

            while (_currentMagazine < magazineCapacity)
            {
                Reloading?.Invoke();
                // ReloadDuration을 탄환 한 발을 장전하는 간격으로 사용합니다.
                if (shellLoadWait != null)
                {
                    yield return shellLoadWait;
                }

                _currentMagazine++;
                AddCriticalFlag();

                Debug.Log($"탄환 장전: {_currentMagazine}/{magazineCapacity}");

                AmmoChanged?.Invoke(_currentMagazine);
            }

            _isReloading = false;
            _reloadCoroutine = null;

            Debug.Log("재장전 완료");

            ReloadEnded?.Invoke();
        }

        private void AtDashCancelReload(Vector2 direction)
        {
            CancelReload();
        }

        private void CancelReload()
        {
            if (!_isReloading)
            {
                return;
            }

            if (_reloadCoroutine != null)
            {
                StopCoroutine(_reloadCoroutine);
            }

            _reloadCoroutine = null;
            _isReloading = false;
            ReloadCanceled?.Invoke();
            ReloadEnded?.Invoke();
        }

        private void OnDisable()
        {
            if (_reloadCoroutine != null)
            {
                StopCoroutine(_reloadCoroutine);
            }

            _reloadCoroutine = null;
            _isReloading = false;
        }

        // 확산각을 펠릿 수만큼 나눈 칸에 펠릿을 하나씩 두고, 칸 안에서 조금씩 흔듭니다.
        // 커서 거리는 산탄각에 영향을 주지 않습니다. (인디케이터 호의 반지름에만 사용)
        private Vector2 ApplySpread(Vector2 direction, int pelletIndex, int pelletCount)
        {
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return Vector2.right;
            }

            direction.Normalize();

            float jitter = Random.Range(-_pelletJitterAngle, _pelletJitterAngle);
            float angle = ShotgunSpread.GetPelletAngle(
                pelletIndex,
                pelletCount,
                SpreadHalfAngle,
                jitter);

            return Quaternion.Euler(0f, 0f, angle) * direction;
        }

        private void HandleStatsRecalculated(
            ShotgunRuntimeStats stats)
        {
            if (stats == null)
            {
                return;
            }

            int previousMagazine = _currentMagazine;

            // 탄창 최대 용량보다 현재 탄약이 많아지지 않도록 보정
            _currentMagazine = Mathf.Clamp(
                _currentMagazine,
                0,
                stats.MagazineSize);

            // 탄약 강화로 크리티컬 간격이 바뀔 수 있으므로 장전된 탄의 표시를 다시 매깁니다.
            RebuildCriticalShells();

            // 재장전 중 능력치가 변경되었다면 현재 재장전 중단
            // 다음 재장전부터 변경된 MagazineSize와 ReloadDuration을 사용
            if (_isReloading)
            {
                CancelReload();
            }

            if (previousMagazine != _currentMagazine)
            {
                AmmoChanged?.Invoke(_currentMagazine);
            }

            Debug.Log(
                $"무기 능력치 갱신" +
                $"집탄률: {stats.SpreadAngle}, " +
                $"공격속도: {stats.FireInterval}, " +
                $"장전용량: {stats.MagazineSize}, " +
                $"반동거리: {stats.RecoilDistance}, " +
                $"데미지: {stats.PelletDamage}, "
            );
        }

        public void RefreshStats()
        {
            if (_stats == null || _data == null || _state == null)
            {
                return;
            }

            _stats.Recalculate(_data, _state);
        }

        private void OnDestroy()
        {
            if (_stats != null)
            {
                _stats.StatsRecalculated -= HandleStatsRecalculated;
                _dash.DashStarted -= AtDashCancelReload;
                _selectionController.OnSelectedConsumable -= HandleConsumableSelected;
                _selectionController.OnDeselectedConsumable -= HandleConsumableDeselected;
            }
        }

        /// <summary>
        /// 장전된 탄 수를 0~탄창 크기 사이 값으로 직접 맞춥니다. (튜토리얼 · 컷씬 연출용)
        /// </summary>
        public void SetMagazine(int count)
        {
            if (_stats == null)
            {
                return;
            }

            CancelReload();
            int clamped = Mathf.Clamp(count, 0, _stats.MagazineSize);
            if (clamped == _currentMagazine)
            {
                return;
            }

            _currentMagazine = clamped;
            RebuildCriticalShells();
            AmmoChanged?.Invoke(_currentMagazine);
        }

        /// <summary>
        /// 탄창을 현재 최대 용량(_stats.MagazineSize)으로 완전히 채웁니다. (던전 진입, 리스폰, 장비 동기화 등)
        /// </summary>
        public void RefillMagazine(bool skipFirstCriticalShot = false)
        {
            if (_stats == null)
            {
                return;
            }

            CancelReload();

            int previousMagazine = _currentMagazine;
            _currentMagazine = _stats.MagazineSize;
            RebuildCriticalShells(skipFirstCriticalShot);

            if (previousMagazine != _currentMagazine)
            {
                AmmoChanged?.Invoke(_currentMagazine);
            }
        }

        // 탄을 하나 장전할 때마다 세어, 간격에 도달한 그 탄을 크리티컬 확정 탄으로 표시합니다.
        private void AddCriticalFlag(bool suppressCritical = false)
        {
            int interval = _stats.CriticalShellInterval;
            bool isCritical = interval > 0 && ++_shellsSinceCritical >= interval;
            if (isCritical)
            {
                _shellsSinceCritical = 0;
            }

            _criticalShells.Add(isCritical && !suppressCritical);
        }

        // 이미 장전된 탄은 처음부터 하나씩 장전한 것으로 보고 표시를 다시 매깁니다.
        private void RebuildCriticalShells(bool skipFirstCriticalShot = false)
        {
            _criticalShells.Clear();
            _shellsSinceCritical = 0;

            for (int i = 0; i < _currentMagazine; i++)
            {
                bool isFirstShot = skipFirstCriticalShot && i == _currentMagazine - 1;
                AddCriticalFlag(isFirstShot);
            }
        }

        public void SetWeaponUsable(bool usable)
        {
            _isShelter = !usable;

            if (_isWeaponUsable == usable)
            {
                return;
            }

            _isWeaponUsable = usable;
            _weaponSprite.enabled = usable;
            _spreadArcIndicator.gameObject.SetActive(usable);
            WeaponUsableChanged?.Invoke(usable);

            if (!usable)
            {
                CancelReload();
            }
        }

        private void HandleConsumableSelected(
            ConsumableData consumable)
        {
            CancelReload();
            
            if (_isShelter)
            {
                return;
            }

            _isWeaponUsable = false;
            _spreadArcIndicator.gameObject.SetActive(false);

            if (_explosionIndicator == null ||
                _throwGrenade == null ||
                consumable == null)
            {
                return;
            }

            ConsumableType type = consumable.ConsumableType;

            bool isGrenade =
                type == ConsumableType.FragmentGrenade ||
                type == ConsumableType.FlashBang;

            if (!isGrenade)
            {
                _explosionIndicator.gameObject.SetActive(false);
                return;
            }

            if (_throwGrenade.TryGetExplosionRadius(
                    type,
                    out float radius))
            {
                _explosionIndicator.SetRadius(radius);
                _explosionIndicator.gameObject.SetActive(true);
            }
            else
            {
                _explosionIndicator.gameObject.SetActive(false);
            }
        }

        private void HandleConsumableDeselected()
        {
            _explosionIndicator?.gameObject.SetActive(false);

            if (_isShelter) return;

            _isWeaponUsable = true;
            _spreadArcIndicator.gameObject.SetActive(_isWeaponUsable);
        }

        public void SetChestInteractionLocked(bool locked)
        {
            _chestInteractionLocked = locked;

            if (locked)
            {
                // 재장전 중이면 즉시 취소
                CancelReload();
            }
        }
    }
}
