using System;
using UnityEngine;

namespace ShotgunAndRun.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerDash : MonoBehaviour
    {
        [SerializeField] private int _dashCount = 2;
        [SerializeField] private float _dashChargeTime = 2f;

        private float _baseDashChargeTime = 2f;
        private const float DistanceEpsilon = 0.0001f;

        private PlayerRuntimeStats _stats;
        private Vector2 _dashDirection;
        private float _remainingDistance;
        private float _cooldownRemaining;
        private float _dashChargeTimer;
        private bool _initialized;
        private bool _dashEnabled = true;

        public event Action<Vector2> DashStarted;
        public event Action DashEnded;
        public event Action<int> DashCharged;

        public bool IsDashing
        {
            get;
            private set;
        }

        public float CooldownRemaining => _cooldownRemaining;
        public float DashChargeTime => _dashChargeTime;
        public Vector2 DashDirection => _dashDirection;

        public Vector2 DashVelocity => IsDashing && _initialized
            ? _dashDirection * _stats.DashSpeed
            : Vector2.zero;

        private void Awake()
        {
            _baseDashChargeTime = _dashChargeTime;
        }

        public void Initialize(PlayerRuntimeStats stats)
        {
            _stats = stats ?? throw new ArgumentNullException(nameof(stats));
            _baseDashChargeTime = _dashChargeTime;
            _initialized = true;
        }

        public void SetChargeTimeReduction(float reduction)
        {
            float safeReduction = Mathf.Max(0f, reduction);
            _dashChargeTime = Mathf.Max(0.2f, _baseDashChargeTime - safeReduction);
        }

        // TODO : 벽 근처에서 벽 너머로 Dash할 시 글리치 버그 수정
        // TODO : PlayerInfo에서 대쉬는 거리랑 이동 시간만 나타나도록 한다. + 기획서의 있는 단위로 설정하면 그대로 반영되도록.
        // ex) inspector에서 이동거리를 1.5로 지정하면 in-game에서 1.5u가 이동하게끔
        public bool TryStart(Vector2 direction)
        {
            if (!_initialized || !_dashEnabled || IsDashing || _cooldownRemaining > 0f)
            {
                return false;
            }

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            if (_dashCount <= 0)
            {
                return false;
            }

            _dashDirection = direction.normalized;
            _remainingDistance = _stats.DashSpeed * _stats.DashDuration;
            _cooldownRemaining = _stats.DashCooldown;
            IsDashing = true;
            _dashCount--;
            DashStarted?.Invoke(_dashDirection);
            return true;
        }

        // 남은 거리를 넘지 않도록 이번 물리 스텝의 속도를 계산합니다.
        public Vector2 ConsumeDashVelocity(float fixedDeltaTime)
        {
            if (!IsDashing || !_initialized || fixedDeltaTime <= 0f)
            {
                return Vector2.zero;
            }

            float step = Mathf.Min(_stats.DashSpeed * fixedDeltaTime, _remainingDistance);
            _remainingDistance -= step;
            Vector2 velocity = _dashDirection * (step / fixedDeltaTime);

            if (_remainingDistance <= DistanceEpsilon)
            {
                EndDash();
            }

            return velocity;
        }

        public void Tick(float deltaTime)
        {
            if (!_initialized || deltaTime <= 0f)
            {
                return;
            }

            _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - deltaTime);
        }

        public void SetDashEnabled(bool value)
        {
            _dashEnabled = value;
            if (!value)
            {
                EndDash();
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
            ChargeDash();
        }

        private void OnDisable()
        {
            EndDash();
        }

        private void EndDash()
        {
            if (!IsDashing)
            {
                return;
            }

            IsDashing = false;
            _remainingDistance = 0f;
            DashEnded?.Invoke();
        }

        private void ChargeDash()
        {
            if (_dashCount >= 2)
            {
                _dashChargeTimer = 0f;
                _dashCount = 2;
                return;
            }

            _dashChargeTimer += Time.deltaTime;

            if (_dashChargeTimer >= _dashChargeTime)
            {
                _dashChargeTimer = 0f;
                _dashCount++;
                DashCharged?.Invoke(_dashCount);
                Debug.Log($"대쉬 충전!");
                Debug.Log($"현재 대쉬 가능 횟수 : {_dashCount}");
            }
        }
    }
}
