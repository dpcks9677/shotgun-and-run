using UnityEngine;
using UnityEngine.UI;

namespace ShotgunAndRun.UI
{
    /// <summary>
    /// 타이틀 로고 연출: 켜지면 잠시 뒤 서서히 나타나고, 이후 위아래로 천천히 떠다닙니다.
    /// 시간이 멈춰도(Time.timeScale 0) 계속 움직이도록 unscaledTime을 씁니다.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class TitleLogoIntro : MonoBehaviour
    {
        [SerializeField] private float _fadeDelay = 0.4f;
        [SerializeField] private float _fadeDuration = 1f;
        [Tooltip("떠오르는 높이(캔버스 단위). 원래 위치에서 이만큼 위까지 올라갑니다.")]
        [SerializeField] private float _floatHeight = 2f;
        [SerializeField] private float _floatPeriod = 4f;

        private Image _image;
        private RectTransform _rect;
        private Vector2 _home;
        private float _alpha;
        private float _startTime;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _rect = (RectTransform)transform;
            _home = _rect.anchoredPosition;
            _alpha = _image.color.a;
        }

        private void OnEnable()
        {
            _startTime = Time.unscaledTime;
            SetAlpha(0f);
        }

        private void Update()
        {
            float t = Time.unscaledTime - _startTime;
            SetAlpha(_alpha * Mathf.Clamp01((t - _fadeDelay) / _fadeDuration));

            // 원래 위치에서 출발해 가장 높은 곳까지 올랐다가 돌아오는 부드러운 파형
            float wave = (1f - Mathf.Cos(t * Mathf.PI * 2f / _floatPeriod)) * 0.5f;
            _rect.anchoredPosition = _home + Vector2.up * (wave * _floatHeight);
        }

        private void SetAlpha(float alpha)
        {
            var color = _image.color;
            color.a = alpha;
            _image.color = color;
        }
    }
}
