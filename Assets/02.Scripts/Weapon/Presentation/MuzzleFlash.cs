using UnityEngine;

namespace ShotgunAndRun.Weapon
{
    // 총구 섬광: 총구(Muzzle) 아래에 붙어 총과 같이 회전하며, 발사할 때마다 처음부터 다시 재생한다.
    // 스프라이트는 총구(왼쪽 가운데)가 피벗이고 +x 방향으로 뻗는다.
    public sealed class MuzzleFlash : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private Sprite[] _frames;
        [SerializeField] private Sprite[] _criticalFrames;

        [Tooltip("초당 재생 프레임 수")]
        [SerializeField, Min(1f)]
        private float _framesPerSecond = 30f;

        [Header("Glow")]
        [Tooltip("가산 블렌딩 부채꼴 빛. 총구 앞 바닥을 잠깐 밝히는 역할 (피벗 = 총구)")]
        [SerializeField] private SpriteRenderer _glow;

        [SerializeField] private Color _glowColor = new(1f, 0.67f, 0.27f, 0.9f);
        [SerializeField] private Color _criticalGlowColor = new(1f, 0.92f, 0.7f, 1f);

        [Tooltip("크리티컬 사격일 때 빛 크기 배율")]
        [SerializeField, Min(1f)]
        private float _criticalGlowScale = 1.5f;

        [Tooltip("빛이 사라지기까지 걸리는 시간. 단위: 초")]
        [SerializeField, Min(0.01f)]
        private float _glowDuration = 0.08f;

        [Header("Embers")]
        [Tooltip("총구에서 떠오르는 불티. 비워 두면 불티 없이 재생한다.")]
        [SerializeField] private ParticleSystem _embers;

        [SerializeField, Min(0)] private int _emberCount = 4;

        [Tooltip("크리티컬 사격일 때 불티에 곱하는 색")]
        [SerializeField] private Color _criticalTint = Color.white;

        private Vector3 _glowBaseScale = Vector3.one;
        private Sprite[] _currentFrames;
        private Color _currentGlowColor;
        private float _timer;

        private void Awake()
        {
            if (_glow != null)
            {
                _glowBaseScale = _glow.transform.localScale;
            }
        }

        public void Play(bool isCritical, bool flipY)
        {
            gameObject.SetActive(true);
            enabled = true;
            _timer = 0f;
            _currentFrames = isCritical ? _criticalFrames : _frames;

            if (_renderer != null)
            {
                _renderer.enabled = true;
                _renderer.flipY = flipY;
                _renderer.sprite = _currentFrames.Length > 0 ? _currentFrames[0] : null;
            }

            if (_glow != null)
            {
                _currentGlowColor = isCritical ? _criticalGlowColor : _glowColor;
                _glow.enabled = true;
                _glow.transform.localScale = _glowBaseScale * (isCritical ? _criticalGlowScale : 1f);
            }

            UpdateGlow();

            if (_embers != null)
            {
                var emitParams = new ParticleSystem.EmitParams();
                if (isCritical)
                {
                    emitParams.startColor = _criticalTint;
                }

                _embers.Emit(emitParams, _emberCount);
            }
        }

        private void Update()
        {
            _timer += Time.deltaTime;

            int frame = Mathf.FloorToInt(_timer * _framesPerSecond);
            if (frame >= _currentFrames.Length)
            {
                // 자식 불티가 사라지지 않도록 루트는 끄지 않고 섬광만 숨긴다.
                if (_renderer != null)
                {
                    _renderer.enabled = false;
                }

                if (_glow != null)
                {
                    _glow.enabled = false;
                }

                enabled = false;
                return;
            }

            if (_renderer != null)
            {
                _renderer.sprite = _currentFrames[frame];
            }

            UpdateGlow();
        }

        private void UpdateGlow()
        {
            if (_glow == null)
            {
                return;
            }

            Color color = _currentGlowColor;
            float fade = 1f - Mathf.Clamp01(_timer / _glowDuration);
            color.a *= fade * fade;
            _glow.color = color;
        }
    }
}
