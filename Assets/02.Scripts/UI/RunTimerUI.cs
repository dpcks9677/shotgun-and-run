using System.Collections;
using ShotgunAndRun.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShotgunAndRun.UI
{
    /// <summary>
    /// GameManager 및 RunSession과 연동하여 현재 런의 남은 시간을 카운트다운(10:00 / 12:00 -> 00:00)하는 HUD UI 컴포넌트입니다.
    /// RunRules의 TimeLimit을 직접 참조하므로, 게임 규칙에서 제한 시간을 변경하면 UI도 자동으로 맞춰집니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class RunTimerUI : MonoBehaviour
    {
        [Header("UI Text Component")]
        [Tooltip("남은 시간을 표시할 TextMeshProUGUI 컴포넌트")]
        [SerializeField] private TextMeshProUGUI _timerText;

        [Header("Normal Appearance (평상시)")]
        [Tooltip("평상시 타이머 텍스트 색상")]
        [SerializeField] private Color _normalColor = Color.white;

        [Header("Warning & Danger Feedback (위기 연출)")]
        [Tooltip("위기 경고 연출 사용 여부 (시간 임박 시 붉은색 및 두근거림)")]
        [SerializeField] private bool _enableWarningFeedback = true;

        [Tooltip("위기 경고 상태로 진입할 남은 시간 기준 (초 단위, 기본: 60초)")]
        [SerializeField, Min(1f)] private float _warningThresholdSeconds = 60f;

        [Tooltip("위기 상태 시 타이머 텍스트 색상")]
        [SerializeField] private Color _warningColor = new Color(1f, 0.25f, 0.25f, 1f);

        [Tooltip("위기 상태에서 텍스트가 두근거리는(Pulse) 속도")]
        [SerializeField, Range(1f, 10f)] private float _pulseSpeed = 4.5f;

        [Tooltip("두근거릴 때 커지는 최대 배율")]
        [SerializeField, Range(1.05f, 1.4f)] private float _pulseScale = 1.15f;

        [Header("Time Over (제한 시간 종료 연출)")]
        [Tooltip("00:00 도달 시 'TIME OVER' 등의 문구로 교체할지 여부 (체크 해제 시 00:00 유지)")]
        [SerializeField] private bool _showTimeOverText = false;

        [Tooltip("시간 초과 시 표시할 텍스트")]
        [SerializeField] private string _timeOverCustomText = "TIME OVER";

        [Tooltip("시간 초과 시 텍스트 색상")]
        [SerializeField] private Color _timeOverColor = new Color(1f, 0.15f, 0.15f, 1f);

        [Header("Extraction Status (탈출구 상태 표시 - 선택 사항)")]
        [Tooltip("탈출구 상태를 나타낼 아이콘 Image (비워두어도 안전하게 무시됩니다)")]
        [SerializeField] private Image _extractionIcon;

        [Tooltip("탈출구가 아직 잠겨있을 때의 스프라이트 (선택 사항)")]
        [SerializeField] private Sprite _extractionLockedSprite;

        [Tooltip("탈출구가 열렸을 때의 스프라이트 (선택 사항)")]
        [SerializeField] private Sprite _extractionUnlockedSprite;

        [Tooltip("탈출구가 잠겨있을 때의 아이콘 색상")]
        [SerializeField] private Color _extractionLockedColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);

        [Tooltip("탈출구가 열렸을 때의 아이콘 색상 (탈출존 네온 블루: #4FB0FF)")]
        [SerializeField] private Color _extractionUnlockedColor = new Color(0.31f, 0.69f, 1f, 1f);

        [Tooltip("탈출구가 열렸을 때 활성화할 추가 UI 오브젝트/알림 배지 (선택 사항)")]
        [SerializeField] private GameObject _extractionUnlockedBadge;

        [Header("Extraction Unlocked Feedback (탈출 해금 시 타이머 연출)")]
        [Tooltip("탈출 구역 해금 시 타이머 텍스트 네온 및 외부 획 펄스 연출 활성화 여부")]
        [SerializeField] private bool _enableExtractionUnlockedFeedback = true;

        [Tooltip("탈출 해금 시 텍스트 내부 색상 (기본: 밝은 네온 스카이 #B5E6FF)")]
        [SerializeField] private Color _extractionTextColor = new Color(0.71f, 0.90f, 1f, 1f);

        [Tooltip("탈출 해금 시 텍스트 테두리(Outline) 발광 네온 색상 (기본: 탈출존 블루 #4FB0FF)")]
        [SerializeField] private Color _extractionOutlineColor = new Color(0.31f, 0.69f, 1f, 1f);

        [Tooltip("탈출 해금 시 텍스트 테두리 펄스용 약간 더 짙은/깊은 네온 색상 (톤온톤 농도 조절)")]
        [SerializeField] private Color _extractionOutlineDarkColor = new Color(0.18f, 0.48f, 0.85f, 1f);

        [Tooltip("탈출 해금 시 테두리 최소 두께")]
        [SerializeField, Range(0f, 1f)] private float _extractionOutlineMinWidth = 0.12f;

        [Tooltip("탈출 해금 시 테두리 최대 두께 (외부로 퍼져나가는 획 펄스)")]
        [SerializeField, Range(0f, 1f)] private float _extractionOutlineMaxWidth = 0.32f;

        [Tooltip("외부 테두리 획 및 색상 농도 펄스 속도")]
        [SerializeField, Range(0.5f, 10f)] private float _extractionPulseSpeed = 2.0f;

        [Tooltip("탈출 해금 시 적용할 전용 TMP 머티리얼 프리셋 (선택 사항: 비워두면 셰이더 프로퍼티 자동 조절)")]
        [SerializeField] private Material _extractionUnlockedMaterial;

        [Header("Extraction Hold Feedback (탈출 대기 안내)")]
        [Tooltip("탈출 대기 상태 안내 UI 전체 컨테이너 (평상시 숨김)")]
        [SerializeField] private GameObject _extractionHoldRoot;

        [Tooltip("탈출 카운트다운 텍스트 (예: 탈출까지 10초)")]
        [SerializeField] private TextMeshProUGUI _extractionCountdownText;

        [Tooltip("구역 대기 안내 보조 텍스트 (예: 구역 안에서 대기하세요)")]
        [SerializeField] private TextMeshProUGUI _extractionGuideText;

        [Tooltip("카운트다운 포맷 문자열 (2자리 고정: {0:00})")]
        [SerializeField] private string _countdownFormat = "탈출까지 {0:00}초";

        [Tooltip("구역 대기 기본 안내 문구")]
        [SerializeField] private string _guideMessage = "구역 안에서 대기하세요";

        private RunSession _currentSession;
        private float? _frozenRemaining; // 런 종료 시점의 남은 시간 (결과 화면 동안 유지)
        private Coroutine _bindRoutine;
        private int _lastDisplayedSeconds = -1;
        private bool _isWarningActive;
        private Vector3 _originalTextScale = Vector3.one;

        // 탈출 해금 네온 아웃라인 캐싱 변수
        private Material _originalFontSharedMaterial;
        private Color _originalOutlineColor;
        private float _originalOutlineWidth;
        private bool _hasOriginalOutlineCached;
        private bool _isExtractionOutlineApplied;

        // 탈출 대기 UI 캐싱 변수 (불필요한 SetActive 및 매 프레임 문자열 할당/GC 방지)
        private int _lastHoldSeconds = -1;
        private bool _isHoldUIVisible;

        private void Awake()
        {
            if (_timerText != null)
            {
                _originalTextScale = _timerText.transform.localScale;
                _originalFontSharedMaterial = _timerText.fontSharedMaterial;

                if (_originalFontSharedMaterial != null && _originalFontSharedMaterial.HasProperty(ShaderUtilities.ID_OutlineColor))
                {
                    _originalOutlineColor = _originalFontSharedMaterial.GetColor(ShaderUtilities.ID_OutlineColor);
                    _originalOutlineWidth = _originalFontSharedMaterial.GetFloat(ShaderUtilities.ID_OutlineWidth);
                    _hasOriginalOutlineCached = true;
                }
            }

            if (_extractionHoldRoot != null)
            {
                _extractionHoldRoot.SetActive(false);
            }
        }

        private void Start()
        {
            if (_bindRoutine != null)
            {
                StopCoroutine(_bindRoutine);
            }
            _bindRoutine = StartCoroutine(BindGameManagerRoutine());
        }

        private void OnEnable()
        {
            if (GameManager.Instance != null)
            {
                SubscribeGameManagerEvents();
                if (GameManager.Instance.CurrentRun != null)
                {
                    BindRunSession(GameManager.Instance.CurrentRun);
                }
            }
        }

        private void OnDisable()
        {
            if (_bindRoutine != null)
            {
                StopCoroutine(_bindRoutine);
                _bindRoutine = null;
            }

            UnsubscribeGameManagerEvents();
            UnbindRunSession();
            ResetTextVisual();
            HideExtractionHoldUI();
        }

        private void Update()
        {
            float remaining = GetRemainingSeconds();
            int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(remaining));

            // 초 단위가 바뀔 때만 텍스트 갱신 (GC Alloc 및 TMP 리빌드 최적화)
            if (totalSeconds != _lastDisplayedSeconds)
            {
                _lastDisplayedSeconds = totalSeconds;
                UpdateTimerText(totalSeconds, remaining <= 0f);
            }

            // 시각 연출 (위기 연출 & 탈출 해금 네온 펄스)
            UpdateVisualAnimation(remaining);
        }

        /// <summary>
        /// GameManager 싱글톤 인스턴스가 생성될 때까지 안전하게 대기 후 연결
        /// </summary>
        private IEnumerator BindGameManagerRoutine()
        {
            while (GameManager.Instance == null)
            {
                yield return null;
            }

            SubscribeGameManagerEvents();

            if (GameManager.Instance.CurrentRun != null)
            {
                BindRunSession(GameManager.Instance.CurrentRun);
            }

            // 초기 시간 UI 즉시 표시
            float initialTime = GetRemainingSeconds();
            int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(initialTime));
            _lastDisplayedSeconds = totalSeconds;
            UpdateTimerText(totalSeconds, isTimeOver: false);

            _bindRoutine = null;
        }

        private void SubscribeGameManagerEvents()
        {
            if (GameManager.Instance == null) return;

            GameManager.Instance.RunStarted -= HandleRunStarted;
            GameManager.Instance.RunStarted += HandleRunStarted;

            GameManager.Instance.RunEnded -= HandleRunEnded;
            GameManager.Instance.RunEnded += HandleRunEnded;
        }

        private void UnsubscribeGameManagerEvents()
        {
            if (GameManager.Instance == null) return;

            GameManager.Instance.RunStarted -= HandleRunStarted;
            GameManager.Instance.RunEnded -= HandleRunEnded;
        }

        private void HandleRunStarted(RunSession session)
        {
            BindRunSession(session);
        }

        private void HandleRunEnded(RunResult result)
        {
            // 세션을 끊으면 제한 시간 전체로 되돌아가므로 종료 시점의 값을 남겨 둡니다.
            if (_currentSession != null)
            {
                _frozenRemaining = _currentSession.RemainingTime;
            }

            UnbindRunSession();
            ResetTextVisual();
        }

        private void BindRunSession(RunSession session)
        {
            if (_currentSession == session) return;

            UnbindRunSession();
            _currentSession = session;
            _frozenRemaining = null;

            if (_currentSession != null)
            {
                _currentSession.ExtractionUnlocked += HandleExtractionUnlocked;
                _currentSession.TimeLimitExceeded += HandleTimeLimitExceeded;
                _currentSession.ExtractionHoldChanged += HandleExtractionHoldChanged;
                _currentSession.ExtractionHoldCancelled += HandleExtractionHoldCancelled;

                // 세션의 탈출구 상태 초기 동기화
                UpdateExtractionDisplay(_currentSession.IsExtractionUnlocked);
                HideExtractionHoldUI();
            }
        }

        private void UnbindRunSession()
        {
            if (_currentSession != null)
            {
                _currentSession.ExtractionUnlocked -= HandleExtractionUnlocked;
                _currentSession.TimeLimitExceeded -= HandleTimeLimitExceeded;
                _currentSession.ExtractionHoldChanged -= HandleExtractionHoldChanged;
                _currentSession.ExtractionHoldCancelled -= HandleExtractionHoldCancelled;
                _currentSession = null;
                HideExtractionHoldUI();
            }
        }

        private float GetRemainingSeconds()
        {
            if (_currentSession != null)
            {
                return _currentSession.RemainingTime;
            }

            if (_frozenRemaining.HasValue)
            {
                return _frozenRemaining.Value;
            }

            if (GameManager.Instance != null && GameManager.Instance.Rules != null)
            {
                return GameManager.Instance.Rules.TimeLimit;
            }

            // GameManager나 규칙을 찾지 못했을 때의 기본 fallback (10분 = 600초)
            return 600f;
        }

        /// <summary>
        /// 초 단위를 00:00 형식으로 변환하여 텍스트에 적용합니다.
        /// </summary>
        private void UpdateTimerText(int totalSeconds, bool isTimeOver)
        {
            if (_timerText == null) return;

            if (isTimeOver && _showTimeOverText)
            {
                _timerText.text = _timeOverCustomText;
                _timerText.color = _timeOverColor;
                return;
            }

            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;

            _timerText.text = $"{minutes:00} : {seconds:00}";

            // 00:00 도달 시 색상 처리
            if (isTimeOver)
            {
                _timerText.color = _timeOverColor;
            }
        }

        /// <summary>
        /// 타이머 텍스트의 시각 연출(위기 경고 펄스 및 탈출 해금 네온 펄스)을 상태 우선순위에 따라 갱신합니다.
        /// 1순위: Time Over (00:00 도달 - 정지)
        /// 2순위: Danger Warning (제한 시간 60초 임박 - 붉은색 강렬한 펄스)
        /// 3순위: Extraction Unlocked (탈출 해금 - 네온 블루 아웃라인 및 은은한 펄스)
        /// 4순위: Normal (평상시 화이트 텍스트)
        /// </summary>
        private void UpdateVisualAnimation(float remaining)
        {
            if (_timerText == null) return;

            bool isOverTime = remaining <= 0f;
            bool isWarning = _enableWarningFeedback && !isOverTime && remaining <= _warningThresholdSeconds;
            bool isExtractionActive = _enableExtractionUnlockedFeedback && !isOverTime && !isWarning
                                      && _currentSession != null && _currentSession.IsExtractionUnlocked;

            if (isWarning)
            {
                _isWarningActive = true;
                ResetOutline();

                _timerText.color = _warningColor;
                float sin = (Mathf.Sin(Time.unscaledTime * _pulseSpeed) + 1f) * 0.5f;
                float currentScale = Mathf.Lerp(1f, _pulseScale, sin);
                _timerText.transform.localScale = _originalTextScale * currentScale;
            }
            else if (isExtractionActive)
            {
                _isWarningActive = false;
                ApplyExtractionOutline();

                // 1. 시계 텍스트 본체는 가만히 고정 (스케일 변화 없음)
                _timerText.transform.localScale = _originalTextScale;
                _timerText.color = _extractionTextColor;

                // 2. 외부 획(Outline) 두께 펄스 및 은은한 톤온톤 색상 농도 펄스
                float sin = (Mathf.Sin(Time.unscaledTime * _extractionPulseSpeed) + 1f) * 0.5f;
                float currentWidth = Mathf.Lerp(_extractionOutlineMinWidth, _extractionOutlineMaxWidth, sin);
                Color currentColor = Color.Lerp(_extractionOutlineDarkColor, _extractionOutlineColor, sin);

                Material mat = _timerText.fontMaterial;
                if (mat != null)
                {
                    mat.SetColor(ShaderUtilities.ID_OutlineColor, currentColor);
                    mat.SetFloat(ShaderUtilities.ID_OutlineWidth, currentWidth);
                }
            }
            else if (!isOverTime)
            {
                if (_isWarningActive || _isExtractionOutlineApplied)
                {
                    ResetTextVisual();
                }
            }
        }

        private void ApplyExtractionOutline()
        {
            if (_isExtractionOutlineApplied || _timerText == null) return;
            _isExtractionOutlineApplied = true;

            if (_extractionUnlockedMaterial != null)
            {
                _timerText.fontSharedMaterial = _extractionUnlockedMaterial;
            }
            else
            {
                Material mat = _timerText.fontMaterial;
                if (mat != null)
                {
                    mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
                }
            }
        }

        private void ResetOutline()
        {
            if (!_isExtractionOutlineApplied || _timerText == null) return;
            _isExtractionOutlineApplied = false;

            if (_extractionUnlockedMaterial != null)
            {
                if (_originalFontSharedMaterial != null)
                {
                    _timerText.fontSharedMaterial = _originalFontSharedMaterial;
                }
            }
            else
            {
                Material mat = _timerText.fontMaterial;
                if (mat != null)
                {
                    if (_hasOriginalOutlineCached)
                    {
                        mat.SetColor(ShaderUtilities.ID_OutlineColor, _originalOutlineColor);
                        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, _originalOutlineWidth);
                        if (_originalOutlineWidth <= 0f)
                        {
                            mat.DisableKeyword(ShaderUtilities.Keyword_Outline);
                        }
                    }
                    else
                    {
                        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
                        mat.DisableKeyword(ShaderUtilities.Keyword_Outline);
                    }
                }
            }
        }

        private void ResetTextVisual()
        {
            _isWarningActive = false;
            ResetOutline();

            if (_timerText != null)
            {
                _timerText.color = _normalColor;
                _timerText.transform.localScale = _originalTextScale;
            }
        }

        private void HandleTimeLimitExceeded()
        {
            ResetOutline();

            if (_timerText != null)
            {
                _timerText.transform.localScale = _originalTextScale;
                _timerText.color = _timeOverColor;

                if (_showTimeOverText)
                {
                    _timerText.text = _timeOverCustomText;
                }
                else
                {
                    _timerText.text = "00 : 00";
                }
            }
        }

        private void HandleExtractionUnlocked()
        {
            UpdateExtractionDisplay(isUnlocked: true);
        }

        /// <summary>
        /// 탈출구 개방 상태를 UI에 표시합니다.
        /// 스프라이트나 UI 오브젝트가 비어있어도(null) 에러 없이 안전하게 동작합니다.
        /// </summary>
        private void UpdateExtractionDisplay(bool isUnlocked)
        {
            if (_extractionIcon != null)
            {
                _extractionIcon.color = isUnlocked ? _extractionUnlockedColor : _extractionLockedColor;

                if (isUnlocked && _extractionUnlockedSprite != null)
                {
                    _extractionIcon.sprite = _extractionUnlockedSprite;
                }
                else if (!isUnlocked && _extractionLockedSprite != null)
                {
                    _extractionIcon.sprite = _extractionLockedSprite;
                }
            }

            if (_extractionUnlockedBadge != null)
            {
                _extractionUnlockedBadge.SetActive(isUnlocked);
            }
        }

        /// <summary>
        /// 탈출 구역 내에서 대기 시간이 진행될 때 호출됩니다.
        /// 매 프레임 발생하는 이벤트이나, 초 단위 변경 시에만 문자열 포맷팅 및 TMP 갱신을 수행하여 GC Allocation을 방지합니다.
        /// </summary>
        private void HandleExtractionHoldChanged(float progress)
        {
            if (_currentSession == null) return;

            // UI 표시 활성화 (상태 변경 시 1회만 호출하여 불필요한 SetActive 비용 방지)
            if (!_isHoldUIVisible)
            {
                _isHoldUIVisible = true;
                if (_extractionHoldRoot != null)
                {
                    _extractionHoldRoot.SetActive(true);
                }

                // 정적 안내 문구는 활성화 시점에 1회만 할당
                if (_extractionGuideText != null && !string.IsNullOrEmpty(_guideMessage))
                {
                    _extractionGuideText.text = _guideMessage;
                }
            }

            // 남은 시간 계산 및 초 단위 캐싱 (초가 변경될 때만 문자열 포맷팅 및 TMP 갱신하여 GC 할당 최소화)
            float remaining = Mathf.Max(0f, _currentSession.RequiredExtractionHoldTime - _currentSession.ExtractionHoldTime);
            int remainingSeconds = Mathf.CeilToInt(remaining);

            if (remainingSeconds != _lastHoldSeconds)
            {
                _lastHoldSeconds = remainingSeconds;
                if (_extractionCountdownText != null)
                {
                    string format = !string.IsNullOrEmpty(_countdownFormat) ? _countdownFormat : "탈출까지 {0:00}초";
                    if (format.Contains("{0}"))
                    {
                        format = format.Replace("{0}", "{0:00}");
                    }

                    _extractionCountdownText.text = string.Format(format, remainingSeconds);
                }
            }
        }

        private void HandleExtractionHoldCancelled()
        {
            HideExtractionHoldUI();
        }

        /// <summary>
        /// 구역 이탈, 세션 해제, 비활성화 시 탈출 대기 UI를 안전하게 숨기고 캐시를 리셋합니다.
        /// </summary>
        private void HideExtractionHoldUI()
        {
            if (_isHoldUIVisible)
            {
                _isHoldUIVisible = false;
                _lastHoldSeconds = -1;
                if (_extractionHoldRoot != null)
                {
                    _extractionHoldRoot.SetActive(false);
                }
            }
        }
    }
}
