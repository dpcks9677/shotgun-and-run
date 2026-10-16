using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Consumable
{
    /// <summary>
    /// 수류탄류 폭발 연출(파편 · 섬광)의 루트. 프레임 스프라이트 레이어를 한 타임라인으로 재생하고,
    /// 시각 반경을 판정 반경에 맞춘 뒤 수명이 끝나면 스스로 파괴한다.
    /// 파티클(먼지 · 불씨 · 잔광)은 ParticleSystem이 자기 시간으로 재생하므로 여기서 다루지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GrenadeExplosionVFX : MonoBehaviour
    {
        private const float PixelsPerUnit = 16f;

        [Serializable]
        private struct FrameLayer
        {
            public SpriteRenderer Renderer;
            public Sprite[] Frames;

            [Tooltip("초당 재생 프레임 수")]
            [Min(1f)] public float FramesPerSecond;

            [Tooltip("폭발 시작 후 이 시간이 지나야 보이기 시작한다. 단위: 초")]
            [Min(0f)] public float Delay;

            [Tooltip("첫 프레임을 더 오래 보여 주는 시간. 단위: 초")]
            [Min(0f)] public float Hold;

            [Tooltip("보이기 시작한 뒤 사라질 때까지의 시간. 마지막 프레임은 끝까지 유지한다. 단위: 초")]
            [Min(0.01f)] public float Duration;

            [Tooltip("켜면 보이는 동안 알파가 1에서 0으로 선형으로 줄어든다")]
            public bool FadeOut;
        }

        [Tooltip("스프라이트를 그린 기준 반경. Play(radius)가 이 값 대비 비율로 전체 크기를 맞춘다. 단위: u")]
        [SerializeField, Min(0.01f)] private float _referenceRadius = 2.5f;

        [Tooltip("모든 연출이 끝나는 시간. 이 시간이 지나면 오브젝트를 파괴한다. 단위: 초")]
        [SerializeField, Min(0.1f)] private float _lifetime = 1f;

        [SerializeField] private FrameLayer[] _layers;

        [Header("Light")]
        [Tooltip("폭발 순간 주변을 밝히는 임시 점광. 비워 둬도 된다.")]
        [SerializeField] private Light2D _light;

        [SerializeField, Min(0f)] private float _lightIntensity = 1f;

        [Tooltip("점광이 사라지기까지 걸리는 시간. 단위: 초")]
        [SerializeField, Min(0.01f)] private float _lightDuration = 0.45f;

        private float _elapsed;

        private void Awake()
        {
            Refresh();
        }

        /// <summary>
        /// 시각 반경을 판정 반경에 맞춘다. 도트가 월드 픽셀 격자에 맞도록 위치도 1px 단위로 맞춘다.
        /// </summary>
        public void Play(float radius)
        {
            transform.localScale = Vector3.one * (Mathf.Max(0f, radius) / _referenceRadius);

            Vector3 position = transform.position;
            position.x = Mathf.Round(position.x * PixelsPerUnit) / PixelsPerUnit;
            position.y = Mathf.Round(position.y * PixelsPerUnit) / PixelsPerUnit;
            transform.position = position;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            Refresh();

            if (_elapsed >= _lifetime)
            {
                Destroy(gameObject);
            }
        }

        private void Refresh()
        {
            foreach (FrameLayer layer in _layers)
            {
                UpdateLayer(layer);
            }

            if (_light != null)
            {
                float t = Mathf.Clamp01(_elapsed / _lightDuration);
                _light.intensity = _lightIntensity * (1f - t);
                _light.enabled = t < 1f;
            }
        }

        private void UpdateLayer(FrameLayer layer)
        {
            if (layer.Renderer == null || layer.Frames == null || layer.Frames.Length == 0)
            {
                return;
            }

            float local = _elapsed - layer.Delay;
            if (local < 0f || local >= layer.Duration)
            {
                layer.Renderer.enabled = false;
                return;
            }

            int frame = Mathf.FloorToInt(Mathf.Max(0f, local - layer.Hold) * layer.FramesPerSecond);
            layer.Renderer.sprite = layer.Frames[Mathf.Min(frame, layer.Frames.Length - 1)];

            Color color = layer.Renderer.color;
            color.a = layer.FadeOut ? 1f - local / layer.Duration : 1f;
            layer.Renderer.color = color;
            layer.Renderer.enabled = true;
        }
    }
}
