using System;
using UnityEngine;

namespace ShotgunAndRun.Combat
{
    public class Blood : MonoBehaviour
    {
        private const int DirectionCount = 8;
        private const int FrameCount = 8;
        private const int FramesPerSecond = 12;

        [SerializeField] private Animator _animator;
        // 인덱스 = (변형 * DirectionCount + 방향) * FrameCount + 프레임. 방향 0 = 동, 반시계 45° 단위
        [SerializeField] private Sprite[] _sprites;
        private SpriteRenderer _renderer;
        private int _spriteBase;
        private AnimatorClipInfo[] _clipInfo;
        private float _animationLength;
        private float _currentTime = 0f;
        private float _mutilplier = 0.5f;
        private bool _isInitialized;
        private Vector3 _startPosition;
        private Vector3 _targetPosition;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _renderer = GetComponent<SpriteRenderer>();
            _clipInfo = _animator.GetCurrentAnimatorClipInfo(0);
            _animationLength = 0.09f;
            _startPosition = transform.position;
        }

        private float _disappearTime = 3f;
        private float _elapsed = 0f;

        private void Update()
        {
            if (!_isInitialized)
            {
                return;
            }

            _currentTime += Time.deltaTime;

            float t = Mathf.Clamp01(_currentTime / _animationLength);

            transform.position = Vector3.Lerp(
                _startPosition,
                _targetPosition,
                t);

            int frame = Mathf.Min((int)(_currentTime * FramesPerSecond), FrameCount - 1);
            _renderer.sprite = _sprites[_spriteBase + frame];

            if (frame == FrameCount - 1)
            {
                _isInitialized = false;
                this.enabled = false;
            }
        }

        public void Initialize(Vector3 direction)
        {
            _startPosition = transform.position;

            Vector3 normalizedDirection =
                direction.sqrMagnitude > 0.001f
                    ? direction.normalized
                    : Vector3.down;

            int variantCount = _sprites.Length / (DirectionCount * FrameCount);
            int variant = UnityEngine.Random.Range(0, variantCount);
            _spriteBase = (variant * DirectionCount + DirectionIndex(normalizedDirection)) * FrameCount;
            _renderer.sprite = _sprites[_spriteBase];

            _targetPosition =
                _startPosition + normalizedDirection * _mutilplier;

            _currentTime = 0f;
            _isInitialized = true;
        }

        public static int DirectionIndex(Vector2 direction)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            int index = Mathf.RoundToInt(angle / (360f / DirectionCount));
            return (index + DirectionCount) % DirectionCount;
        }
    }
}
