using System;
using System.Collections;
using ShotgunAndRun.Core;
using ShotgunAndRun.Player;
using UnityEngine;
using UnityEngine.UI;
using PlayerCharacter = ShotgunAndRun.Player.Player;

namespace ShotgunAndRun.UI
{
    /// <summary>
    /// 화면 외곽에 표시될 비네팅 연출 모드
    /// 우선순위: OverTime(시간 초과 보라색) > LowHealth(빈사 상태 붉은색) > None(비활성화)
    /// </summary>
    public enum VignetteMode
    {
        None = 0,
        LowHealth = 1,
        OverTime = 2
    }

    /// <summary>
    /// 개별 비네트 연출 상태의 색상, 투명도 범위 및 맥박 속도를 정의하는 설정 데이터
    /// </summary>
    [Serializable]
    public struct VignetteStyle
    {
        [Tooltip("비네트 색상")]
        public Color color;

        [Tooltip("맥박 최저점 투명도 (0 ~ 1)")]
        [Range(0f, 1f)]
        public float minAlpha;

        [Tooltip("맥박 최고점 투명도 (0 ~ 1)")]
        [Range(0f, 1f)]
        public float maxAlpha;

        [Tooltip("두근거리는 맥박 주기/속도 (초당 진동수)")]
        [Range(0.5f, 15f)]
        public float pulseSpeed;

        public VignetteStyle(Color color, float minAlpha, float maxAlpha, float pulseSpeed)
        {
            this.color = color;
            this.minAlpha = minAlpha;
            this.maxAlpha = maxAlpha;
            this.pulseSpeed = pulseSpeed;
        }
    }

    /// <summary>
    /// 단일 책임 원칙(SRP)에 따라 화면 전체 비네팅 피드백만을 전담하는 UI 컴포넌트입니다.
    /// 체력 1칸 이하 시 붉은색 심장박동 비네트가 발동하며, 시간 초과 시 탈출 압박을 주는 보라색 비네트가 최우선으로 발동합니다.
    /// 별도의 비네트 스프라이트가 없어도 런타임에 고품질 원형 그라데이션 스프라이트를 자동 생성하여 표시합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ScreenVignetteFeedbackUI : MonoBehaviour
    {
        [Header("Target Image (비네트 UI 이미지)")]
        [Tooltip("화면 전체를 덮는 UI Image. 비워두면 자동으로 탐색하거나 풀스크린 자식 오브젝트를 생성합니다.")]
        [SerializeField] private Image _vignetteImage;

        [Tooltip("커스텀 비네트 스프라이트 (비워둘 시 부드러운 원형 그라데이션 스프라이트를 런타임에 자동 생성)")]
        [SerializeField] private Sprite _customVignetteSprite;

        [Header("Vignette Shape (모서리 집중 및 반경 설정)")]
        [Tooltip("비네트가 시작되는 내부 반경 (0 ~ 1). 높을수록 화면 중앙과 변이 맑고 모서리에만 맺힙니다. (현업 추천값: 0.70 ~ 0.75)")]
        [SerializeField, Range(0.4f, 1.0f)] private float _vignetteInnerRadius = 0.72f;

        [Tooltip("비네트가 최고 농도에 도달하는 외부 반경. (현업 추천값: 1.30 ~ 1.45)")]
        [SerializeField, Range(1.0f, 1.8f)] private float _vignetteOuterRadius = 1.35f;

        [Tooltip("모서리 집중 곡선 지수. 높을수록 화면 상하좌우 변은 투명해지고 대각선 4개 모서리에만 날카롭게 맺힙니다. (현업 추천값: 1.6 ~ 2.2)")]
        [SerializeField, Range(1.0f, 3.5f)] private float _vignetteSharpness = 1.8f;

        [Header("Low Health Vignette (체력 1칸 이하: 붉은색)")]
        [Tooltip("체력 위험 비네트 활성화 여부")]
        [SerializeField] private bool _enableLowHealthVignette = true;

        [Tooltip("빈사 상태로 판정할 체력 임계치 (기본: 하트 1칸 이하)")]
        [SerializeField, Min(1)] private int _lowHealthThreshold = 1;

        [Tooltip("체력 위험 비네트 스타일 (색상, 알파 범위, 맥박 속도)")]
        [SerializeField]
        private VignetteStyle _lowHealthStyle = new VignetteStyle(
            new Color(0.9f, 0.12f, 0.12f, 1f),
            minAlpha: 0.08f,
            maxAlpha: 0.30f,
            pulseSpeed: 3.8f
        );

        [Header("OverTime Vignette (시간 초과 탈출 압박: 보라색)")]
        [Tooltip("시간 초과 비네트 활성화 여부 (발동 시 저체력보다 최우선 표시)")]
        [SerializeField] private bool _enableOverTimeVignette = true;

        [Tooltip("시간 초과 비네트 스타일 (색상, 알파 범위, 맥박 속도)")]
        [SerializeField]
        private VignetteStyle _overTimeStyle = new VignetteStyle(
            new Color(0.68f, 0.12f, 0.88f, 1f),
            minAlpha: 0.12f,
            maxAlpha: 0.40f,
            pulseSpeed: 5.2f
        );

        [Header("Animation & Transitions (전환 연출)")]
        [Tooltip("상태 전환 시 색상이 자연스럽게 섞이는 블렌딩 속도")]
        [SerializeField, Range(1f, 15f)] private float _colorTransitionSpeed = 5.0f;

        [Tooltip("알파값이 목표치로 부드럽게 보간되는 속도")]
        [SerializeField, Range(1f, 15f)] private float _alphaTransitionSpeed = 4.0f;

        [Tooltip("비네트가 사라질 때의 페이드아웃 속도")]
        [SerializeField, Range(1f, 10f)] private float _fadeOutSpeed = 3.0f;

        [Tooltip("위기 상태 최초 진입(피격/타임오버) 순간 강하게 번쩍이는 임팩트 알파값")]
        [SerializeField, Range(0f, 1f)] private float _impactFlashAlpha = 0.45f;

        [Tooltip("임팩트 플래시 감쇠 속도")]
        [SerializeField, Range(1f, 10f)] private float _flashDecaySpeed = 3.5f;

        // 런타임 참조 및 상태
        private PlayerHealth _playerHealth;
        private RunSession _currentSession;
        private Sprite _generatedSprite;

        private VignetteMode _activeMode = VignetteMode.None;
        private Color _currentColor = Color.white;
        private float _currentAlpha = 0f;
        private float _pulseTimer = 0f;
        private float _flashAlpha = 0f;

        private bool _isOverTime = false;
        private bool _isPlayerDead = false;
        private int _currentHealth = -1;
        private Coroutine _bindRoutine;

        public VignetteMode ActiveMode => _activeMode;

        private void Awake()
        {
            EnsureVignetteImage();
            _currentColor = _lowHealthStyle.color;
            ApplyVisual(Color.clear);
        }

        private void Start()
        {
            if (_bindRoutine != null)
            {
                StopCoroutine(_bindRoutine);
            }
            _bindRoutine = StartCoroutine(BindDependenciesRoutine());
        }

        private void OnEnable()
        {
            SubscribeEvents();
        }

        private void OnDisable()
        {
            if (_bindRoutine != null)
            {
                StopCoroutine(_bindRoutine);
                _bindRoutine = null;
            }

            UnsubscribeEvents();
            _activeMode = VignetteMode.None;
            _currentAlpha = 0f;
            ApplyVisual(Color.clear);
        }

        private void OnDestroy()
        {
            // 절차적으로 생성한 텍스처/스프라이트 메모리 해제
            if (_generatedSprite != null)
            {
                if (_generatedSprite.texture != null)
                {
                    Destroy(_generatedSprite.texture);
                }
                Destroy(_generatedSprite);
                _generatedSprite = null;
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _pulseTimer += dt;

            // 플래시 감쇠
            if (_flashAlpha > 0f)
            {
                _flashAlpha = Mathf.MoveTowards(_flashAlpha, 0f, dt * _flashDecaySpeed);
            }

            // 우선순위 기반 목표 모드 결정
            VignetteMode targetMode = EvaluateTargetMode();
            _activeMode = targetMode;

            if (targetMode == VignetteMode.None)
            {
                // 비활성화 페이드아웃
                _currentAlpha = Mathf.MoveTowards(_currentAlpha, 0f, dt * _fadeOutSpeed);
                if (_currentAlpha <= 0.001f)
                {
                    _currentAlpha = 0f;
                    if (_vignetteImage != null && _vignetteImage.enabled)
                    {
                        _vignetteImage.enabled = false;
                    }
                    return;
                }
            }
            else
            {
                if (_vignetteImage != null && !_vignetteImage.enabled)
                {
                    _vignetteImage.enabled = true;
                }

                VignetteStyle activeStyle = (targetMode == VignetteMode.OverTime) ? _overTimeStyle : _lowHealthStyle;

                // 색상 부드러운 블렌딩 (빨강 -> 보라 등)
                _currentColor = Color.Lerp(_currentColor, activeStyle.color, dt * _colorTransitionSpeed);

                // 두근거리는 맥박 Sine 계산 (0 ~ 1)
                float sine = (Mathf.Sin(_pulseTimer * activeStyle.pulseSpeed) + 1f) * 0.5f;
                float pulseTargetAlpha = Mathf.Lerp(activeStyle.minAlpha, activeStyle.maxAlpha, sine);

                // 피격/타임오버 순간의 플래시 알파 반영
                float desiredAlpha = Mathf.Max(pulseTargetAlpha, _flashAlpha);

                _currentAlpha = Mathf.MoveTowards(_currentAlpha, desiredAlpha, dt * _alphaTransitionSpeed);
            }

            ApplyVisual(new Color(_currentColor.r, _currentColor.g, _currentColor.b, _currentAlpha));
        }

        /// <summary>
        /// 단일 책임 우선순위 판정:
        /// 1순위: 시간 초과 (OverTime -> 보라색)
        /// 2순위: 체력 1칸 이하 (LowHealth -> 붉은색)
        /// 그 외: None
        /// </summary>
        public VignetteMode EvaluateTargetMode()
        {
            if (_isPlayerDead)
            {
                return VignetteMode.None;
            }

            // [우선순위 1] 시간 초과가 켜져 있으면 체력 상태와 무관하게 보라색 비네트 우선
            if (_enableOverTimeVignette && _isOverTime)
            {
                return VignetteMode.OverTime;
            }

            // [우선순위 2] 체력이 위험 기준치 이하인 경우
            if (_enableLowHealthVignette && _currentHealth > 0 && _currentHealth <= _lowHealthThreshold)
            {
                return VignetteMode.LowHealth;
            }

            return VignetteMode.None;
        }

        private void ApplyVisual(Color color)
        {
            if (_vignetteImage != null)
            {
                _vignetteImage.color = color;
            }
        }

        #region Initialization & Image Setup

        /// <summary>
        /// 풀스크린 비네트 Image가 없을 경우 자식 오브젝트로 자동 생성하고 안전하게 설정합니다.
        /// </summary>
        private void EnsureVignetteImage()
        {
            if (_vignetteImage == null)
            {
                _vignetteImage = GetComponentInChildren<Image>();
            }

            if (_vignetteImage == null)
            {
                GameObject overlayGo = new GameObject("VignetteOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                overlayGo.transform.SetParent(transform, false);

                RectTransform rect = overlayGo.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;

                _vignetteImage = overlayGo.GetComponent<Image>();
            }

            if (_vignetteImage != null)
            {
                RectTransform rect = _vignetteImage.rectTransform;
                if (rect != null)
                {
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                    rect.localScale = Vector3.one;
                }

                // 마우스 클릭 및 버튼 인터랙션 방해 방지
                _vignetteImage.raycastTarget = false;

                // 스프라이트 연결 (커스텀이 없으면 절차적 그라데이션 스프라이트 생성)
                if (_vignetteImage.sprite == null)
                {
                    _vignetteImage.sprite = GetOrCreateVignetteSprite();
                }
            }
        }

        private Sprite GetOrCreateVignetteSprite()
        {
            if (_customVignetteSprite != null)
            {
                return _customVignetteSprite;
            }

            if (_generatedSprite == null)
            {
                _generatedSprite = ProceduralVignetteUtility.CreateRadialVignetteSprite(
                    resolution: 256,
                    innerRadius: _vignetteInnerRadius,
                    outerRadius: _vignetteOuterRadius,
                    sharpness: _vignetteSharpness
                );
            }

            return _generatedSprite;
        }

        /// <summary>
        /// 인스펙터나 런타임에서 반경/샤프니스 수치를 변경했을 때 스프라이트를 즉시 재성형합니다.
        /// </summary>
        public void RegenerateProceduralSprite()
        {
            if (_customVignetteSprite != null) return;

            if (_generatedSprite != null)
            {
                if (_generatedSprite.texture != null)
                {
                    Destroy(_generatedSprite.texture);
                }
                Destroy(_generatedSprite);
                _generatedSprite = null;
            }

            _generatedSprite = ProceduralVignetteUtility.CreateRadialVignetteSprite(
                resolution: 256,
                innerRadius: _vignetteInnerRadius,
                outerRadius: _vignetteOuterRadius,
                sharpness: _vignetteSharpness
            );

            if (_vignetteImage != null)
            {
                _vignetteImage.sprite = _generatedSprite;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying && _vignetteImage != null && _generatedSprite != null && _customVignetteSprite == null)
            {
                RegenerateProceduralSprite();
            }
        }
#endif

        #endregion

        #region Dependency Binding & Events

        private IEnumerator BindDependenciesRoutine()
        {
            // 1. PlayerHealth 대기 및 연결
            while (_playerHealth == null)
            {
                if (PlayerCharacter.PlayerInstance != null)
                {
                    _playerHealth = PlayerCharacter.PlayerInstance.Health;
                }

                if (_playerHealth == null)
                {
                    _playerHealth = FindFirstObjectByType<PlayerHealth>();
                }

                if (_playerHealth == null)
                {
                    yield return null;
                }
            }

            BindPlayerHealth(_playerHealth);

            // 2. GameManager / RunSession 연결 (존재하는 경우)
            if (GameManager.Instance != null)
            {
                SubscribeGameManagerEvents();
                if (GameManager.Instance.CurrentRun != null)
                {
                    BindRunSession(GameManager.Instance.CurrentRun);
                }
            }

            _bindRoutine = null;
        }

        private void SubscribeEvents()
        {
            if (_playerHealth != null)
            {
                _playerHealth.HealthChanged -= HandleHealthChanged;
                _playerHealth.HealthChanged += HandleHealthChanged;
                _playerHealth.Died -= HandlePlayerDied;
                _playerHealth.Died += HandlePlayerDied;
            }

            if (GameManager.Instance != null)
            {
                SubscribeGameManagerEvents();
            }

            if (_currentSession != null)
            {
                _currentSession.TimeLimitExceeded -= HandleTimeLimitExceeded;
                _currentSession.TimeLimitExceeded += HandleTimeLimitExceeded;
            }
        }

        private void UnsubscribeEvents()
        {
            if (_playerHealth != null)
            {
                _playerHealth.HealthChanged -= HandleHealthChanged;
                _playerHealth.Died -= HandlePlayerDied;
            }

            UnsubscribeGameManagerEvents();

            if (_currentSession != null)
            {
                _currentSession.TimeLimitExceeded -= HandleTimeLimitExceeded;
            }
        }

        private void BindPlayerHealth(PlayerHealth health)
        {
            if (health == null) return;
            _playerHealth = health;

            _playerHealth.HealthChanged -= HandleHealthChanged;
            _playerHealth.HealthChanged += HandleHealthChanged;
            _playerHealth.Died -= HandlePlayerDied;
            _playerHealth.Died += HandlePlayerDied;

            _isPlayerDead = _playerHealth.IsDead;
            _currentHealth = _playerHealth.CurrentHealth;

            // 이미 위험 체력인 상태로 시작된 경우
            if (_currentHealth <= _lowHealthThreshold && !_isPlayerDead)
            {
                _flashAlpha = _impactFlashAlpha;
            }
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
            UnbindRunSession();
            _isOverTime = false;
        }

        private void BindRunSession(RunSession session)
        {
            if (_currentSession == session) return;
            UnbindRunSession();

            _currentSession = session;
            if (_currentSession != null)
            {
                _currentSession.TimeLimitExceeded += HandleTimeLimitExceeded;
                _isOverTime = _currentSession.IsOverTime;

                if (_isOverTime)
                {
                    _flashAlpha = _impactFlashAlpha;
                }
            }
        }

        private void UnbindRunSession()
        {
            if (_currentSession != null)
            {
                _currentSession.TimeLimitExceeded -= HandleTimeLimitExceeded;
                _currentSession = null;
            }
        }

        private void HandleHealthChanged(int currentHealth, int maxHealth)
        {
            bool wasSafe = _currentHealth > _lowHealthThreshold;
            bool isNowLow = currentHealth <= _lowHealthThreshold;

            _currentHealth = currentHealth;
            _isPlayerDead = currentHealth <= 0;

            // 방금 안전 상태에서 체력 1칸으로 피격 진입 시 충격 플래시 발동
            if (wasSafe && isNowLow && !_isPlayerDead && !_isOverTime)
            {
                _flashAlpha = _impactFlashAlpha;
            }
        }

        private void HandlePlayerDied()
        {
            _isPlayerDead = true;
        }

        private void HandleTimeLimitExceeded()
        {
            _isOverTime = true;
            _flashAlpha = _impactFlashAlpha; // 타임오버 순간 강렬한 보라색 임팩트 플래시
        }

        #endregion

        #region Public Debug & Testing API

        /// <summary>
        /// 테스트 또는 외부 연동용 체력 상태 수동 설정
        /// </summary>
        public void SetHealthStateForTesting(int currentHealth, bool isDead)
        {
            _currentHealth = currentHealth;
            _isPlayerDead = isDead;
        }

        /// <summary>
        /// 테스트 또는 외부 연동용 시간 초과 상태 수동 설정
        /// </summary>
        public void SetOverTimeStateForTesting(bool isOverTime)
        {
            _isOverTime = isOverTime;
        }

        #endregion
    }

    /// <summary>
    /// 별도 텍스처 에셋 없이도 매끄러운 타원형 원형 비네팅 텍스처를 런타임에 생성하는 유틸리티입니다.
    /// 중앙은 완전 투명(alpha 0)하며 외곽으로 갈수록 Smoothstep 곡선으로 점진적으로 짙어집니다.
    /// </summary>
    public static class ProceduralVignetteUtility
    {
        public static Sprite CreateRadialVignetteSprite(
            int resolution = 256,
            float innerRadius = 0.72f,
            float outerRadius = 1.35f,
            float sharpness = 1.8f)
        {
            Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
            {
                name = "Procedural_Vignette_Radial",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color[] pixels = new Color[resolution * resolution];
            float half = resolution * 0.5f;

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    // [-1, 1] 범위로 정규화
                    float nx = (x - half + 0.5f) / half;
                    float ny = (y - half + 0.5f) / half;
                    float dist = Mathf.Sqrt(nx * nx + ny * ny);

                    float t = Mathf.InverseLerp(innerRadius, outerRadius, dist);
                    float smooth = Mathf.SmoothStep(0f, 1f, t);
                    float alpha = Mathf.Pow(smooth, sharpness);

                    // 기본 흰색에 알파만 지정 (UI Image의 Color를 곱해 붉은색/보라색을 자유자재로 표현)
                    pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(
                texture,
                new Rect(0, 0, resolution, resolution),
                new Vector2(0.5f, 0.5f)
            );
        }
    }
}
