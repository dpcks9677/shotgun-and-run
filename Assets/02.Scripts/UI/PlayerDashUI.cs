using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ShotgunAndRun.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PlayerCharacter = ShotgunAndRun.Player.Player;

namespace ShotgunAndRun.UI
{
    /// <summary>
    /// 플레이어의 대시 충전량(스택) 상태 및 충전 시 팝(Pop) 연출을 표시하는 UI 컴포넌트입니다.
    /// PlayerDash 스크립트 수정 없이 기존 이벤트(DashStarted, DashCharged)를 구독하여 동작합니다.
    /// 별도의 빈 스프라이트 없이 단일 대시 아이콘의 알파값(투명도)을 조절하여 충전 상태를 표현합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerDashUI : MonoBehaviour
    {
        [Header("Target Dash")]
        [Tooltip("비워두면 씬에서 Player.PlayerInstance.Dash를 자동으로 찾아 연결합니다. (프리팹 에셋이 연결되어 있어도 씬 인스턴스로 자동 보정)")]
        [SerializeField]
        private PlayerDash _dash;

        [Header("Dash Configuration")]
        [Tooltip("최대 대시 충전 횟수 (기본값: 2)")]
        [SerializeField, Min(1)]
        private int _maxDashCount = 2;

        [Header("UI Slot Setup")]
        [Tooltip("미리 씬/프리팹에 배치해둔 대시 슬롯 Image 목록입니다. 할당되어 있으면 동적 생성 대신 이를 우선 사용합니다.")]
        [SerializeField]
        private List<Image> _preplacedSlots = new();

        [Tooltip("동적 생성 시 대시 슬롯들이 배치될 부모 컨테이너 (Horizontal Layout Group 등). 내부에 이미 슬롯이 들어있다면 자동으로 수집합니다.")]
        [SerializeField]
        private Transform _slotContainer;

        [Tooltip("동적 생성 시 사용할 대시 슬롯 Image 프리팹 (또는 프로토타입)")]
        [SerializeField]
        private Image _slotPrefab;

        [Tooltip("선택 사항: '2 / 2' 형태로 숫자를 띄우고 싶을 때 연결하는 TMP 텍스트")]
        [SerializeField]
        private TextMeshProUGUI _dashCountText;

        [Header("Slot Appearance (단일 스프라이트 투명도 제어)")]
        [Tooltip("사용 가능한 상태의 슬롯 기본 색상")]
        [SerializeField]
        private Color _activeColor = Color.white;

        [Tooltip("대시가 소모되었을 때 적용할 알파(투명도) 값 (0: 완전 투명 ~ 1: 불투명)")]
        [SerializeField, Range(0f, 1f)]
        private float _emptyAlpha = 0.25f;

        [Header("Juice & Pop Animation (충전 연출)")]
        [Tooltip("대시 충전 완료 시 톡 튀어나오는 팝 애니메이션 사용 여부")]
        [SerializeField]
        private bool _enablePopAnimation = true;

        [Tooltip("충전 시 순간적으로 커지는 배율")]
        [SerializeField, Range(1f, 2f)]
        private float _popScale = 1.35f;

        [Tooltip("팝 애니메이션 지속 시간 (초)")]
        [SerializeField, Range(0.05f, 0.5f)]
        private float _popDuration = 0.15f;

        private readonly List<Image> _slots = new();
        private readonly Dictionary<Transform, Coroutine> _activePopRoutines = new();
        private Coroutine _bindRoutine;
        private bool _isSubscribed;
        private int _currentDashCount = -1;

        public int CurrentDashCount => _currentDashCount;
        public int MaxDashCount => _maxDashCount;

        private void Awake()
        {
            InitializeSlots();
        }

        private void OnEnable()
        {
            if (IsSceneComponent(_dash) && !_isSubscribed)
            {
                SubscribeDashEvents();
                _currentDashCount = ReadInitialDashCount();
                UpdateDisplay(isChargedPop: false, changedSlotIndex: -1);
            }
        }

        private void Start()
        {
            if (_bindRoutine != null)
            {
                StopCoroutine(_bindRoutine);
            }

            _bindRoutine = StartCoroutine(BindDashRoutine());
        }

        private void OnDisable()
        {
            if (_bindRoutine != null)
            {
                StopCoroutine(_bindRoutine);
                _bindRoutine = null;
            }

            UnsubscribeDashEvents();
            StopAllPopAnimations();
        }

        private void OnDestroy()
        {
            UnsubscribeDashEvents();
            StopAllPopAnimations();
        }

        /// <summary>
        /// 컴포넌트가 프리팹 에셋이 아닌 실제 로드된 씬에 속해 있는지 검사합니다.
        /// </summary>
        private static bool IsSceneComponent(Component comp)
        {
            return comp != null && comp.gameObject != null && comp.gameObject.scene.IsValid();
        }

        /// <summary>
        /// 인스펙터에 사전 배치된 슬롯 또는 프리팹/컨테이너를 기반으로 슬롯 리스트를 초기화합니다.
        /// </summary>
        private void InitializeSlots()
        {
            _slots.Clear();

            // 1. 인스펙터에 미리 배치된 슬롯이 등록되어 있다면 우선 사용
            if (_preplacedSlots != null && _preplacedSlots.Count > 0)
            {
                for (int i = 0; i < _preplacedSlots.Count; i++)
                {
                    if (_preplacedSlots[i] != null)
                    {
                        _slots.Add(_preplacedSlots[i]);
                    }
                }

                if (_slots.Count > 0)
                {
                    _maxDashCount = _slots.Count;
                    return;
                }
            }

            // 2. _slotContainer가 지정된 경우
            if (_slotContainer != null)
            {
                // 2-1. 컨테이너 하위에 이미 배치되어 있는 자식 슬롯이 있다면 자동 수집
                Image[] children = _slotContainer.GetComponentsInChildren<Image>(true);
                foreach (Image img in children)
                {
                    if (img.transform != _slotContainer)
                    {
                        _slots.Add(img);
                    }
                }

                if (_slots.Count > 0)
                {
                    _maxDashCount = _slots.Count;
                    return;
                }

                // 2-2. 컨테이너가 비어있고 _slotPrefab이 있다면 동적 스폰
                if (_slotPrefab != null)
                {
                    for (int i = 0; i < _maxDashCount; i++)
                    {
                        Image newSlot = Instantiate(_slotPrefab, _slotContainer);
                        newSlot.gameObject.SetActive(true);
                        _slots.Add(newSlot);
                    }
                }
            }
        }

        /// <summary>
        /// 씬에 스폰된 Player 및 PlayerDash를 안전하게 대기/탐색하고 이벤트를 바인딩하는 루틴.
        /// (인스펙터에 프리팹 에셋이 할당되어 있더라도 씬 인스턴스로 자동 교체)
        /// </summary>
        private IEnumerator BindDashRoutine()
        {
            while (!IsSceneComponent(_dash))
            {
                if (PlayerCharacter.PlayerInstance != null && IsSceneComponent(PlayerCharacter.PlayerInstance.Dash))
                {
                    _dash = PlayerCharacter.PlayerInstance.Dash;
                }

                if (!IsSceneComponent(_dash))
                {
                    _dash = FindFirstObjectByType<PlayerDash>();
                }

                if (!IsSceneComponent(_dash))
                {
                    yield return null;
                }
            }

            // 슬롯이 아직 초기화되지 않았다면 재시도
            if (_slots.Count == 0)
            {
                InitializeSlots();
            }

            SubscribeDashEvents();
            _currentDashCount = ReadInitialDashCount();
            UpdateDisplay(isChargedPop: false, changedSlotIndex: -1);
            _bindRoutine = null;
        }

        private void SubscribeDashEvents()
        {
            if (_isSubscribed || !IsSceneComponent(_dash))
            {
                return;
            }

            _dash.DashStarted += HandleDashStarted;
            _dash.DashCharged += HandleDashCharged;
            _isSubscribed = true;
        }

        private void UnsubscribeDashEvents()
        {
            if (!_isSubscribed || _dash == null)
            {
                return;
            }

            _dash.DashStarted -= HandleDashStarted;
            _dash.DashCharged -= HandleDashCharged;
            _isSubscribed = false;
        }

        /// <summary>
        /// PlayerDash의 private _dashCount 초기값을 안전하게 1회 조회 (리플렉션 실패 시 _maxDashCount 기본값 사용)
        /// </summary>
        private int ReadInitialDashCount()
        {
            if (!IsSceneComponent(_dash))
            {
                return _maxDashCount;
            }

            try
            {
                FieldInfo field =
                    typeof(PlayerDash).GetField("_dashCount", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    object value = field.GetValue(_dash);
                    if (value is int intValue)
                    {
                        return Mathf.Clamp(intValue, 0, _maxDashCount);
                    }
                }
            }
            catch
            {
                // Reflection 실패 시 방어적으로 기본값 사용
            }

            return _maxDashCount;
        }

        private void HandleDashStarted(Vector2 direction)
        {
            _currentDashCount = Mathf.Max(0, _currentDashCount - 1);
            UpdateDisplay(isChargedPop: false, changedSlotIndex: _currentDashCount);
        }

        private void HandleDashCharged(int newDashCount)
        {
            _currentDashCount = Mathf.Clamp(newDashCount, 0, _maxDashCount);
            int chargedSlotIndex = _currentDashCount - 1;
            UpdateDisplay(isChargedPop: true, changedSlotIndex: chargedSlotIndex);
        }

        /// <summary>
        /// 현재 잔여 대시 수량에 맞춰 단일 스프라이트의 알파값을 갱신하고 충전 팝 애니메이션을 처리합니다.
        /// </summary>
        private void UpdateDisplay(bool isChargedPop, int changedSlotIndex)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                Image slot = _slots[i];
                if (slot == null)
                {
                    continue;
                }

                bool isReady = i < _currentDashCount;

                // 단일 스프라이트 알파값 토글: 사용 가능하면 원본 알파, 소모되면 _emptyAlpha
                Color slotColor = _activeColor;
                slotColor.a = isReady ? _activeColor.a : _emptyAlpha;
                slot.color = slotColor;

                // 충전 완료된 슬롯에 팝 애니메이션 재생
                if (isChargedPop && i == changedSlotIndex && _enablePopAnimation)
                {
                    PlayPopAnimation(slot.transform);
                }
            }

            UpdateText();
        }

        private void UpdateText()
        {
            if (_dashCountText == null)
            {
                return;
            }

            int count = Mathf.Max(0, _currentDashCount);
            _dashCountText.text = $"{count} / {_maxDashCount}";
        }

        private void PlayPopAnimation(Transform target)
        {
            if (target == null)
            {
                return;
            }

            if (_activePopRoutines.TryGetValue(target, out Coroutine runningRoutine) && runningRoutine != null)
            {
                StopCoroutine(runningRoutine);
                target.localScale = Vector3.one;
            }

            _activePopRoutines[target] = StartCoroutine(PopRoutine(target));
        }

        private IEnumerator PopRoutine(Transform target)
        {
            if (target == null)
            {
                yield break;
            }

            Vector3 normalScale = Vector3.one;
            Vector3 peakScale = Vector3.one * _popScale;

            float halfDuration = _popDuration * 0.5f;
            float elapsed = 0f;

            // 1. 커지는 구간
            while (elapsed < halfDuration)
            {
                if (target == null) yield break;

                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                target.localScale = Vector3.Lerp(normalScale, peakScale, t);
                yield return null;
            }

            elapsed = 0f;

            // 2. 원래 크기로 복귀 구간
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
                _activePopRoutines.Remove(target);
            }
        }

        private void StopAllPopAnimations()
        {
            foreach (var kvp in _activePopRoutines)
            {
                if (kvp.Value != null)
                {
                    StopCoroutine(kvp.Value);
                }

                if (kvp.Key != null)
                {
                    kvp.Key.localScale = Vector3.one;
                }
            }

            _activePopRoutines.Clear();
        }
    }
}
