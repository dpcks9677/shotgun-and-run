using System.Collections;
using UnityEngine;

namespace ShotgunAndRun.UI
{
    // 캐릭터 머리 위의 행동 진행 막대 (재장전 · 순간이동 채널링 · 상자 열기)
    // 양 끝에 세로선이 있는 흰 가로선 위를 표식이 왼쪽에서 오른쪽으로 이동합니다.
    // 가로선 · 표식은 도트 스프라이트이며, 표식은 기본적으로 픽셀 사이를 부드럽게 이동합니다. (_snapToPixel로 픽셀 단위 이동 가능)
    // 여러 행동이 겹치면 먼저 표시를 시작한 쪽(owner)이 Hide를 부를 때까지 막대를 씁니다.
    [DisallowMultipleComponent]
    public sealed class ActionProgressBar : MonoBehaviour
    {
        [Tooltip("표시할 때 켜고 숨길 때 끄는 오브젝트 (가로선 · 표식)")]
        [SerializeField] private GameObject _visual;
        [SerializeField] private Transform _marker;

        [Header("Pixel Layout")]
        [Tooltip("양 끝 세로선을 포함한 가로선 전체 폭 (픽셀)")]
        [SerializeField, Min(4)] private int _trackPixels = 24;
        [SerializeField, Min(1)] private int _markerPixels = 2;
        [SerializeField, Min(1f)] private float _pixelsPerUnit = 16f;
        [Tooltip("켜면 표식이 1픽셀씩 끊어서 이동합니다. 끄면 진행률에 맞춰 부드럽게 이동합니다.")]
        [SerializeField] private bool _snapToPixel;

        [Header("Cancel Feedback")]
        [Tooltip("취소 연출 시 표시할 색상")]
        [SerializeField] private Color _cancelColor = new Color(0.95f, 0.25f, 0.25f, 1f);
        [Tooltip("흔들리는 전체 시간 (초)")]
        [SerializeField, Min(0.05f)] private float _shakeDuration = 0.25f;
        [Tooltip("회전 진동수 (초당 왕복 횟수)")]
        [SerializeField, Min(1f)] private float _shakeFrequency = 22f;
        [Tooltip("최대 회전 기울기 각도 (도)")]
        [SerializeField, Range(1f, 45f)] private float _maxTiltAngle = 12f;
        [Tooltip("흔들린 후 페이드아웃되는 시간 (초)")]
        [SerializeField, Min(0f)] private float _fadeDuration = 0.08f;

        private object _owner;
        private Coroutine _fillRoutine;
        private Coroutine _cancelRoutine;
        private SpriteRenderer[] _renderers;
        private Color[] _defaultColors;
        private Vector3 _visualBaseLocalPosition;
        private Quaternion _visualBaseLocalRotation;

        public bool IsVisible => _owner != null;

        private void Awake()
        {
            CacheRenderers();
            SetVisible(false);
        }

        private void OnDisable()
        {
            StopFillRoutine();
            StopCancelRoutine();
            _owner = null;
            SetVisible(false);
        }

        // 다른 행동이 막대를 쓰고 있으면 무시하고 false를 반환합니다.
        public bool SetProgress(object owner, float progress)
        {
            if (!TryClaim(owner))
            {
                return false;
            }

            StopFillRoutine();
            ApplyProgress(progress);
            return true;
        }

        // 시간이 지나면 저절로 끝까지 차는 표시 (재장전처럼 진행 시간이 정해진 행동용)
        // 끝까지 차도 숨기지 않으므로, 행동이 끝나면 Hide를 호출합니다.
        public bool StartFill(object owner, float duration)
        {
            if (!TryClaim(owner))
            {
                return false;
            }

            StopFillRoutine();
            ApplyProgress(0f);

            if (duration <= 0f)
            {
                ApplyProgress(1f);
                return true;
            }

            if (isActiveAndEnabled)
            {
                _fillRoutine = StartCoroutine(FillRoutine(duration));
            }

            return true;
        }

        // 막대를 쓰고 있는 owner일 때만 숨깁니다.
        public void Hide(object owner)
        {
            if (owner == null || _owner != owner)
            {
                return;
            }

            StopFillRoutine();
            StopCancelRoutine();
            _owner = null;
            SetVisible(false);
        }

        // 진행 도중 취소되었을 때 빨간색으로 흔들리다 사라지는 연출을 시작합니다.
        public void Cancel(object owner)
        {
            if (owner == null || _owner != owner)
            {
                return;
            }

            StopFillRoutine();

            if (!isActiveAndEnabled || _visual == null)
            {
                StopCancelRoutine();
                _owner = null;
                SetVisible(false);
                return;
            }

            StopCancelRoutine();
            _cancelRoutine = StartCoroutine(CancelRoutine());
        }

        private bool TryClaim(object owner)
        {
            // Hide를 부르지 못하고 파괴된 owner는 풀어 줍니다.
            if (_owner is Object ownerObject && ownerObject == null)
            {
                _owner = null;
            }

            if (owner == null)
            {
                return false;
            }

            // 취소 연출 중인 경우, 새 행동이 즉시 막대를 쓸 수 있도록 양보합니다.
            if (_owner != null && _owner != owner)
            {
                if (_cancelRoutine != null)
                {
                    StopCancelRoutine();
                    _owner = null;
                }
                else
                {
                    return false;
                }
            }

            StopCancelRoutine();
            _owner = owner;
            SetVisible(true);
            return true;
        }

        private void ApplyProgress(float progress)
        {
            if (_marker == null)
            {
                return;
            }

            // 표식은 양 끝 세로선 안쪽에서만 움직입니다.
            int travelPixels = Mathf.Max(0, _trackPixels - 2 - _markerPixels);
            float offsetPixels = Mathf.Clamp01(progress) * travelPixels;
            if (_snapToPixel)
            {
                offsetPixels = Mathf.Round(offsetPixels);
            }
            float leftEdge = -_trackPixels * 0.5f + 1f;
            float centerPixels = leftEdge + offsetPixels + _markerPixels * 0.5f;

            Vector3 position = _marker.localPosition;
            position.x = centerPixels / _pixelsPerUnit;
            _marker.localPosition = position;
        }

        private IEnumerator FillRoutine(float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                ApplyProgress(elapsed / duration);
                yield return null;
            }

            ApplyProgress(1f);
            _fillRoutine = null;
        }

        private void StopFillRoutine()
        {
            if (_fillRoutine != null)
            {
                StopCoroutine(_fillRoutine);
                _fillRoutine = null;
            }
        }

        private IEnumerator CancelRoutine()
        {
            ApplyCancelColor();

            float elapsed = 0f;
            while (elapsed < _shakeDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / _shakeDuration);
                float damping = 1f - progress;
                float wave = Mathf.Sin(elapsed * _shakeFrequency * Mathf.PI * 2f);
                float angle = wave * _maxTiltAngle * damping;

                if (_visual != null)
                {
                    _visual.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                }

                yield return null;
            }

            if (_visual != null)
            {
                _visual.transform.localRotation = _visualBaseLocalRotation;
            }

            if (_fadeDuration > 0f)
            {
                float fadeElapsed = 0f;
                while (fadeElapsed < _fadeDuration)
                {
                    fadeElapsed += Time.deltaTime;
                    float alphaMultiplier = 1f - Mathf.Clamp01(fadeElapsed / _fadeDuration);
                    ApplyCancelColorAlpha(alphaMultiplier);
                    yield return null;
                }
            }

            ResetVisualProperties();
            _owner = null;
            SetVisible(false);
            _cancelRoutine = null;
        }

        private void StopCancelRoutine()
        {
            if (_cancelRoutine != null)
            {
                StopCoroutine(_cancelRoutine);
                _cancelRoutine = null;
            }

            ResetVisualProperties();
        }

        private void CacheRenderers()
        {
            if (_visual != null)
            {
                _visualBaseLocalPosition = _visual.transform.localPosition;
                _visualBaseLocalRotation = _visual.transform.localRotation;
                _renderers = _visual.GetComponentsInChildren<SpriteRenderer>(true);
                if (_renderers != null && _renderers.Length > 0)
                {
                    _defaultColors = new Color[_renderers.Length];
                    for (int i = 0; i < _renderers.Length; i++)
                    {
                        _defaultColors[i] = _renderers[i] != null ? _renderers[i].color : Color.white;
                    }
                }
            }
        }

        private void ApplyCancelColor()
        {
            if (_renderers == null)
            {
                CacheRenderers();
            }

            if (_renderers == null) return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                {
                    _renderers[i].color = _cancelColor;
                }
            }
        }

        private void ApplyCancelColorAlpha(float alphaMultiplier)
        {
            if (_renderers == null) return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                {
                    Color c = _cancelColor;
                    c.a *= Mathf.Clamp01(alphaMultiplier);
                    _renderers[i].color = c;
                }
            }
        }

        private void RestoreDefaultColors()
        {
            if (_renderers == null || _defaultColors == null) return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null && i < _defaultColors.Length)
                {
                    _renderers[i].color = _defaultColors[i];
                }
            }
        }

        private void ResetVisualProperties()
        {
            if (_visual != null)
            {
                _visual.transform.localPosition = _visualBaseLocalPosition;
                _visual.transform.localRotation = _visualBaseLocalRotation;
            }

            RestoreDefaultColors();
        }

        private void SetVisible(bool value)
        {
            if (_visual != null && _visual.activeSelf != value)
            {
                _visual.SetActive(value);
            }
        }
    }
}
