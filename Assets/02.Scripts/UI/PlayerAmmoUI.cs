using System.Collections;
using System.Collections.Generic;
using ShotgunAndRun.Player;
using ShotgunAndRun.Weapon;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PlayerCharacter = ShotgunAndRun.Player.Player;

namespace ShotgunAndRun.UI
{
    public enum EmptyDisplayMode
    {
        [Tooltip("빈 탄피 스프라이트 대신 어둡고 반투명하게 표시 (슬롯 위치와 남은 용량을 알아보기 좋음)")]
        Dimmed,

        [Tooltip("탄약이 빠지면 스프라이트/오브젝트를 완전히 숨김")]
        Hidden
    }

    public enum FillOrder
    {
        [Tooltip("아래(또는 오른쪽)부터 차곡차곡 채우고, 쏠 때는 위(또는 왼쪽)부터 소모")]
        BottomToTop,

        [Tooltip("위(또는 왼쪽)부터 차곡차곡 채우고, 쏠 때는 아래(또는 오른쪽)부터 소모")]
        TopToBottom
    }

    /// <summary>
    /// 플레이어의 탄약 수량 및 샷건 쉘 장전 과정을 화면 HUD에 표시하는 UI 컴포넌트
    /// PlayerWeapon 스크립트 수정 없이 이벤트만 구독하여 연동됩니다.
    /// </summary>
    public class PlayerAmmoUI : MonoBehaviour
    {
        [Header("Target Weapon")]
        [Tooltip("비워두면 씬에서 PlayerWeapon을 자동으로 찾아 연결합니다.")]
        [SerializeField]
        private PlayerWeapon _weapon;

        [Header("UI Layout Elements")]
        [Tooltip("탄약 아이콘들이 배치될 부모 컨테이너 (Vertical 또는 Horizontal Layout Group 추천)")]
        [SerializeField]
        private Transform _shellContainer;

        [Tooltip("탄약 1발을 나타낼 Image 프리팹 (또는 씬 내 프로토타입 Image)")]
        [SerializeField]
        private Image _shellPrefab;

        [Tooltip("선택 사항: '4 / 6' 형태로 숫자를 띄우고 싶을 때 연결하는 TMP 텍스트")]
        [SerializeField]
        private TextMeshProUGUI _ammoText;

        [Header("Fill & Consume Order (소모/장전 방향)")]
        [Tooltip("총알이 차오르고 빠져나가는 방향 (기본: 아래에서 채우고 위에서부터 빠짐)")]
        [SerializeField]
        private FillOrder _fillOrder = FillOrder.BottomToTop;

        [Header("Empty Shell Appearance (빈 탄피 연출)")]
        [Tooltip("빈 탄피 전용 스프라이트가 없을 때의 표시 방식")]
        [SerializeField]
        private EmptyDisplayMode _emptyDisplayMode = EmptyDisplayMode.Dimmed;

        [Tooltip("장전되어 있는 탄약 색상")]
        [SerializeField]
        private Color _filledColor = Color.white;

        [Tooltip("탄약이 빠졌을 때의 색상 (Dimmed 모드 시 어둡고 반투명하게 설정)")]
        [SerializeField]
        private Color _emptyColor = new Color(0.25f, 0.25f, 0.25f, 0.35f);

        [Header("Juice & Animation (장전 연출)")]
        [Tooltip("탄환이 1발씩 찰칵 장전될 때 살짝 커졌다 작아지는 팝 애니메이션 사용 여부")]
        [SerializeField]
        private bool _enableReloadPop = true;

        [Tooltip("장전 시 순간적으로 커지는 배율")]
        [SerializeField, Range(1f, 2f)]
        private float _popScale = 1.35f;

        [Tooltip("팝 애니메이션 지속 시간 (초)")]
        [SerializeField, Range(0.05f, 0.3f)]
        private float _popDuration = 0.12f;

        [Header("Shell Effects (크리티컬 탄환 · 발사 연출)")]
        [Tooltip("비워두면 크리티컬 표시와 발사 연출 없이 기존처럼 동작합니다.")]
        [SerializeField]
        private ShellEffectData _effects;

        private readonly List<Image> _spawnedShells = new();
        private Coroutine _activePopCoroutine;
        private Transform _activePopTarget;
        private Coroutine _bindRoutine;
        private bool _isSubscribed;
        private int _lastAmmo = -1;

        private void OnEnable()
        {
            if (_weapon != null && !_isSubscribed)
            {
                SubscribeWeaponEvents();
            }
        }

        private void Start()
        {
            if (_bindRoutine != null)
            {
                StopCoroutine(_bindRoutine);
            }

            _bindRoutine = StartCoroutine(BindWeaponRoutine());
        }

        private void OnDisable()
        {
            if (_bindRoutine != null)
            {
                StopCoroutine(_bindRoutine);
                _bindRoutine = null;
            }

            UnsubscribeWeaponEvents();
        }

        /// <summary>
        /// Player 및 PlayerWeapon의 초기화(Awake)가 완료될 때까지 안전하게 대기 후 바인딩
        /// </summary>
        private IEnumerator BindWeaponRoutine()
        {
            // 1. 무기 컴포넌트 탐색
            while (_weapon == null)
            {
                if (PlayerCharacter.PlayerInstance != null)
                {
                    _weapon = PlayerCharacter.PlayerInstance.GetComponent<PlayerWeapon>();
                }

                if (_weapon == null)
                {
                    _weapon = FindFirstObjectByType<PlayerWeapon>();
                }

                if (_weapon == null)
                {
                    yield return null;
                }
            }

            // 2. 무기의 Stats(ShotgunRuntimeStats)가 초기화될 때까지 대기
            while (_weapon.Stats == null)
            {
                yield return null;
            }

            SubscribeWeaponEvents();
            RebuildShellGrid();
            _lastAmmo = _weapon.CurrentMagazine;
            UpdateAmmoDisplay(_weapon.CurrentMagazine, isReloadPop: false, previousAmmo: _lastAmmo);
            _bindRoutine = null;
        }

        private void SubscribeWeaponEvents()
        {
            if (_isSubscribed || _weapon == null) return;

            _weapon.AmmoChanged += HandleAmmoChanged;
            _weapon.Reloading += HandleReloading;
            _weapon.Fired += HandleFired;

            if (_weapon.Stats != null)
            {
                _weapon.Stats.StatsRecalculated += HandleStatsRecalculated;
            }

            _isSubscribed = true;
        }

        private void UnsubscribeWeaponEvents()
        {
            if (!_isSubscribed || _weapon == null) return;

            _weapon.AmmoChanged -= HandleAmmoChanged;
            _weapon.Reloading -= HandleReloading;
            _weapon.Fired -= HandleFired;

            if (_weapon.Stats != null)
            {
                _weapon.Stats.StatsRecalculated -= HandleStatsRecalculated;
            }

            _isSubscribed = false;
        }

        /// <summary>
        /// 최대 탄창 크기만큼 UI 슬롯 오브젝트들을 생성/배치합니다.
        /// </summary>
        private void RebuildShellGrid()
        {
            if (_shellContainer == null || _shellPrefab == null || _weapon == null || _weapon.Stats == null)
            {
                return;
            }

            int capacity = _weapon.Stats.MagazineSize;

            // 기존 슬롯 정리
            foreach (Transform child in _shellContainer)
            {
                if (child.gameObject == _shellPrefab.gameObject)
                {
                    child.gameObject.SetActive(false);
                    continue;
                }

                Destroy(child.gameObject);
            }

            _spawnedShells.Clear();

            // 최대 탄창 용량만큼 쉘 슬롯 생성
            for (int i = 0; i < capacity; i++)
            {
                Image shell = Instantiate(_shellPrefab, _shellContainer);
                shell.gameObject.SetActive(true);
                shell.transform.localScale = Vector3.one;

                if (_effects != null)
                {
                    shell.gameObject.AddComponent<AmmoShellIcon>().Initialize(shell, _effects);
                }

                _spawnedShells.Add(shell);
            }
        }

        private void HandleAmmoChanged(int currentAmmo)
        {
            // 탄약이 증가(장전)했을 때만 해당 탄환 슬롯에 팝 애니메이션 연출 적용
            bool isReload = _enableReloadPop && _lastAmmo >= 0 && currentAmmo > _lastAmmo;
            int previousAmmo = _lastAmmo;
            _lastAmmo = currentAmmo;
            UpdateAmmoDisplay(currentAmmo, isReloadPop: isReload, previousAmmo: previousAmmo);
        }

        private void HandleFired(bool isCritical)
        {
            // 발사 시 연출 확장 포인트
        }

        private void HandleReloading()
        {
            // 장전 시작 시 연출 확장 포인트 (UI 갱신은 탄환이 실제로 채워지는 HandleAmmoChanged에서 수행)
        }

        private void HandleStatsRecalculated(ShotgunRuntimeStats stats)
        {
            RebuildShellGrid();
            if (_weapon != null)
            {
                _lastAmmo = _weapon.CurrentMagazine;
                UpdateAmmoDisplay(_weapon.CurrentMagazine, isReloadPop: false, previousAmmo: _lastAmmo);
            }
        }

        /// <summary>
        /// 잔탄 수 및 소모/장전 방향에 따라 슬롯들의 스프라이트 상태를 갱신합니다.
        /// </summary>
        private void UpdateAmmoDisplay(int currentAmmo, bool isReloadPop, int previousAmmo)
        {
            int maxCapacity = _weapon != null && _weapon.Stats != null
                ? _weapon.Stats.MagazineSize
                : _spawnedShells.Count;

            // 텍스트 표시
            if (_ammoText != null)
            {
                _ammoText.text = $"{currentAmmo} / {maxCapacity}";
            }

            int emptyCount = Mathf.Max(0, maxCapacity - currentAmmo);

            // 각 쉘 아이콘 갱신
            for (int i = 0; i < _spawnedShells.Count; i++)
            {
                var shellImage = _spawnedShells[i];
                if (shellImage == null) continue;

                bool isLoaded;
                bool isJustReloadedSlot = false;

                if (_fillOrder == FillOrder.BottomToTop)
                {
                    // [BottomToTop 모드]
                    // 위쪽(0번 인덱스부터) emptyCount개만큼 비워지고, 아래쪽부터 채워짐
                    // 쏠 때: 위에서부터 하나씩 꺼짐
                    // 장전 시: 아래에서 위로 하나씩 채워짐
                    isLoaded = i >= emptyCount;
                    isJustReloadedSlot = (i == emptyCount);
                }
                else
                {
                    // [TopToBottom 모드]
                    // 위쪽부터 채워지고 아래쪽부터 꺼짐
                    isLoaded = i < currentAmmo;
                    isJustReloadedSlot = (i == currentAmmo - 1);
                }

                shellImage.TryGetComponent(out AmmoShellIcon icon);

                if (isLoaded)
                {
                    // 장전된 상태
                    icon?.CancelFire();
                    shellImage.gameObject.SetActive(true);
                    shellImage.color = _filledColor;

                    // 방금 장전된 탄환에 팝 애니메이션 적용
                    if (isReloadPop && isJustReloadedSlot)
                    {
                        PlayPopAnimation(shellImage.transform);
                    }
                }
                else if (icon != null && _emptyDisplayMode == EmptyDisplayMode.Dimmed &&
                         (IsSlotLoaded(i, previousAmmo, maxCapacity) || icon.IsFiring))
                {
                    // 방금 소모된 칸은 발사 연출이 끝나면서 빈 탄피 색으로 바뀝니다. 연출 중인 칸은 다시 건드리지 않습니다.
                    if (!icon.IsFiring)
                    {
                        shellImage.gameObject.SetActive(true);
                        icon.PlayFire(_emptyColor);
                    }
                }
                else
                {
                    // 비어있는 상태
                    switch (_emptyDisplayMode)
                    {
                        case EmptyDisplayMode.Dimmed:
                            shellImage.gameObject.SetActive(true);
                            shellImage.color = _emptyColor;
                            break;

                        case EmptyDisplayMode.Hidden:
                            shellImage.gameObject.SetActive(false);
                            break;
                    }
                }
            }

            RefreshCriticalShell();
        }

        private bool IsSlotLoaded(int index, int ammo, int capacity)
        {
            return _fillOrder == FillOrder.BottomToTop ? index >= capacity - ammo : index < ammo;
        }

        /// <summary>
        /// 장전된 탄 중 크리티컬이 확정된 탄의 칸을 크리티컬 탄환으로 표시합니다.
        /// </summary>
        private void RefreshCriticalShell()
        {
            if (_effects == null)
            {
                return;
            }

            for (int i = 0; i < _spawnedShells.Count; i++)
            {
                if (_spawnedShells[i].TryGetComponent(out AmmoShellIcon icon))
                {
                    // 가장 먼저 장전된 칸(BottomToTop이면 맨 아래)이 1번째
                    int position = _fillOrder == FillOrder.BottomToTop ? _spawnedShells.Count - i : i + 1;
                    icon.SetCritical(_weapon.IsCriticalShell(position));
                }
            }
        }

        private void PlayPopAnimation(Transform target)
        {
            if (_activePopCoroutine != null)
            {
                StopCoroutine(_activePopCoroutine);
                if (_activePopTarget != null)
                {
                    _activePopTarget.localScale = RestScaleOf(_activePopTarget);
                }
            }

            _activePopTarget = target;
            _activePopCoroutine = StartCoroutine(PopRoutine(target));
        }

        // 가로로 반전된 칸(AmmoShellIcon)은 반전을 유지한 채 팝 효과를 줘야 합니다.
        private static Vector3 RestScaleOf(Transform target)
        {
            return target.TryGetComponent(out AmmoShellIcon icon) ? icon.RestScale : Vector3.one;
        }

        private IEnumerator PopRoutine(Transform target)
        {
            if (target == null) yield break;

            Vector3 normalScale = RestScaleOf(target);
            Vector3 peakScale = normalScale * _popScale;

            float halfDuration = _popDuration * 0.5f;
            float elapsed = 0f;

            // 커지는 구간
            while (elapsed < halfDuration)
            {
                if (target == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                target.localScale = Vector3.Lerp(normalScale, peakScale, t);
                yield return null;
            }

            elapsed = 0f;
            // 원래 크기로 돌아오는 구간
            while (elapsed < halfDuration)
            {
                if (target == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                target.localScale = Vector3.Lerp(peakScale, normalScale, t);
                yield return null;
            }

            if (target != null)
            {
                target.localScale = normalScale;
            }

            _activePopTarget = null;
            _activePopCoroutine = null;
        }
    }
}
