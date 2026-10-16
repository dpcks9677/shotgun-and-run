using UnityEngine;

namespace ShotgunAndRun.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D _rigidbody;
        [SerializeField] private PlayerMovement _movement;
        [SerializeField] private PlayerDash _dash;

        private bool _initialized;
        private bool _motorEnabled = true;

        public Vector2 CurrentVelocity => _rigidbody != null ? _rigidbody.linearVelocity : Vector2.zero;

        private Vector2 _recoilVelocity;
        [SerializeField, Min(0f)] private float _recoilRecoverySpeed = 20f;

        public void Initialize(PlayerMovement movement, PlayerDash dash)
        {
            _rigidbody ??= GetComponent<Rigidbody2D>();
            _movement = movement != null ? movement : throw new System.ArgumentNullException(nameof(movement));
            _dash = dash != null ? dash : throw new System.ArgumentNullException(nameof(dash));
            _initialized = _rigidbody != null;

            if (!_initialized)
            {
                Debug.LogError("PlayerMotor requires a Rigidbody2D.", this);
            }
        }

        public void SetMotorEnabled(bool value)
        {
            _motorEnabled = value;
            if (!value)
            {
                Stop();
            }
        }

        public void Stop()
        {
            _recoilVelocity = Vector2.zero;

            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector2.zero;
            }
        }

        private void FixedUpdate()
        {
            if (!_initialized || !_motorEnabled)
            {
                Stop();
                return;
            }

            if (_dash.IsDashing)
            {
                _recoilVelocity = Vector2.zero;
                _rigidbody.linearVelocity = _dash.ConsumeDashVelocity(Time.fixedDeltaTime);
                return;
            }

            _rigidbody.linearVelocity =
                _movement.DesiredVelocity + _recoilVelocity;

            _recoilVelocity = Vector2.MoveTowards(
                _recoilVelocity,
                Vector2.zero,
                _recoilRecoverySpeed * Time.fixedDeltaTime);
        }

        private void Reset()
        {
            ResolveComponents();
        }

        private void OnValidate()
        {
            ResolveComponents();
        }

        private void OnDisable()
        {
            Stop();
        }

        private void ResolveComponents()
        {
            _rigidbody ??= GetComponent<Rigidbody2D>();
            _movement ??= GetComponent<PlayerMovement>();
            _dash ??= GetComponent<PlayerDash>();
        }

        public void ApplyRecoil(Vector2 direction, float distance)
        {
            if (!_initialized || !_motorEnabled || distance <= 0f ||
                _recoilRecoverySpeed <= 0f ||
                direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            // 감속 중 이동 거리(distance)가 지정한 반동 거리와 비슷해지도록
            // v^2 = 2as 공식을 사용해 초기 반동 속도를 계산합니다.
            float recoilSpeed = Mathf.Sqrt(
                2f * _recoilRecoverySpeed * distance);

            _recoilVelocity += direction.normalized * recoilSpeed;
        }
    }
}
