using System.Collections;
using System.Collections.Generic;
using ShotgunAndRun.Combat;
using UnityEngine;

namespace Consumable
{
    public sealed class ExplosionRangeIndicator : MonoBehaviour
    {
        [SerializeField] private PixelLineRenderer _lineRenderer;
        [SerializeField] private int _segmentCount = 64;
        [SerializeField] private Color _color;
        [SerializeField] private float _blinkInterval = 0.15f;
        [SerializeField, Range(0.1f, 2f)] private float _horizontalScale = 1f;
        [SerializeField, Range(0.1f, 2f)] private float _verticalScale = 0.65f;

        private Vector2[] _points;
        private SpriteRenderer _spriteRenderer;

        public IReadOnlyList<Vector2> Points => _points;

        private void Awake()
        {
            _lineRenderer ??= GetComponentInChildren<PixelLineRenderer>();

            if (_lineRenderer == null)
            {
                Debug.LogError(
                    "ExplosionRangeIndicator에 PixelLineRenderer가 없습니다.",
                    this);

                enabled = false;
                return;
            }

            _points = new Vector2[_segmentCount];
            _spriteRenderer = _lineRenderer.GetComponent<SpriteRenderer>();
            _spriteRenderer.color = _color;
        }

        public void Show(float radius)
        {
            radius = Mathf.Max(0f, radius);

            for (int i = 0; i < _segmentCount; i++)
            {
                float angle = Mathf.PI * 2f * i / _segmentCount;

                _points[i] = new Vector2(
                    Mathf.Cos(angle) * radius * _horizontalScale,
                    Mathf.Sin(angle) * radius * _verticalScale);
            }

            _lineRenderer.Draw(_points, true);
        }

        public void Hide()
        {
            if (_lineRenderer == null)
            {
                return;
            }

            _lineRenderer.Hide();
        }

        public void SetColor(Color color)
        {
            _spriteRenderer.color = color;
        }

        public void StartBlink()
        {
            StartCoroutine(BlinkRoutine());
        }

        private IEnumerator BlinkRoutine()
        {
            float elapsedTime = 0f;

            while (true)
            {
                elapsedTime += Time.deltaTime;
                float blend = Mathf.PingPong(elapsedTime / _blinkInterval, 1f);
                SetColor(Color.Lerp(_color, Color.white, blend));
                yield return null;
            }
        }
    }
}
