using UnityEngine;

namespace ShotgunAndRun.Tutorial
{
    /// <summary>
    /// (프로토타입) 전시대 위에 떠 있는 물건(샷건)을 위아래로 천천히 흔듭니다. 올라가면 접지 그림자는 작고 옅어집니다.
    /// 시간이 멈춰도(Time.timeScale 0) 계속 흔들리도록 unscaledTime을 씁니다.
    /// </summary>
    public sealed class FloatingDisplay : MonoBehaviour
    {
        [Header("흔들 대상")]
        [SerializeField] private Transform _body;
        [SerializeField] private SpriteRenderer _shadow;

        [Header("연출 수치")]
        [Tooltip("위아래 진폭(월드 단위). 1px = 0.0625")]
        [SerializeField, Min(0f)] private float _amplitude = 0.0625f;
        [SerializeField, Min(0.01f)] private float _period = 2.4f;
        [Tooltip("가장 높이 올라갔을 때 그림자 크기 · 투명도 배율")]
        [SerializeField, Range(0.3f, 1f)] private float _shadowMinFactor = 0.8f;

        private Vector3 _bodyHome;
        private Vector3 _shadowScale;
        private Color _shadowColor;

        private void Awake()
        {
            if (_body != null) _bodyHome = _body.localPosition;
            if (_shadow != null)
            {
                _shadowScale = _shadow.transform.localScale;
                _shadowColor = _shadow.color;
            }
        }

        private void Update()
        {
            float wave = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / _period);
            if (_body != null)
            {
                _body.localPosition = _bodyHome + Vector3.up * (wave * _amplitude);
            }

            if (_shadow != null)
            {
                float k = Mathf.Lerp(1f, _shadowMinFactor, (wave + 1f) * 0.5f);
                _shadow.transform.localScale = _shadowScale * k;
                var color = _shadowColor;
                color.a *= k;
                _shadow.color = color;
            }
        }
    }
}
