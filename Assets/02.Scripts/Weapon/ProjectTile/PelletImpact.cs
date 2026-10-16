using UnityEngine;

namespace ShotgunAndRun.Weapon
{
    // 펠릿이 벽이나 적에 닿은 자리에서 몇 프레임짜리 도트 애니메이션을 한 번 재생하고 풀로 돌아간다.
    // 픽셀아트라 회전하지 않고, 같은 모양이 반복돼 보이지 않도록 좌우만 무작위로 뒤집는다.
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PelletImpact : MonoBehaviour
    {
        private const float PixelsPerUnit = 16f;

        [SerializeField] private Sprite[] _frames;

        [Tooltip("초당 재생 프레임 수")]
        [SerializeField, Min(1f)]
        private float _framesPerSecond = 24f;

        [Tooltip("가산 블렌딩 헤일로. 첫 프레임에 가장 밝고 점점 사라진다. 비워 둬도 된다.")]
        [SerializeField] private SpriteRenderer _glow;

        [Tooltip("헤일로가 사라지기까지 걸리는 시간. 단위: 초")]
        [SerializeField, Min(0.01f)]
        private float _glowDuration = 0.1f;

        private ProjectTilePool _pool;
        private SpriteRenderer _renderer;
        private Color _glowColor;
        private float _timer;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();

            if (_glow != null)
            {
                _glowColor = _glow.color;
            }
        }

        public void Bind(ProjectTilePool pool)
        {
            _pool = pool;
        }

        public void Play(Vector2 position)
        {
            // 도트가 월드 픽셀 격자에 맞도록 위치를 1px 단위로 맞춘다.
            transform.position = new Vector3(
                Mathf.Round(position.x * PixelsPerUnit) / PixelsPerUnit,
                Mathf.Round(position.y * PixelsPerUnit) / PixelsPerUnit,
                transform.position.z);

            _timer = 0f;
            _renderer.flipX = Random.value < 0.5f;
            _renderer.sprite = _frames.Length > 0 ? _frames[0] : null;
            UpdateGlow();
        }

        private void Update()
        {
            _timer += Time.deltaTime;

            int frame = Mathf.FloorToInt(_timer * _framesPerSecond);
            if (frame >= _frames.Length)
            {
                _pool.Release(this);
                return;
            }

            _renderer.sprite = _frames[frame];
            UpdateGlow();
        }

        private void UpdateGlow()
        {
            if (_glow == null)
            {
                return;
            }

            Color color = _glowColor;
            color.a *= 1f - Mathf.Clamp01(_timer / _glowDuration);
            _glow.color = color;
        }

        public void ResetState()
        {
            _timer = 0f;
        }
    }
}
