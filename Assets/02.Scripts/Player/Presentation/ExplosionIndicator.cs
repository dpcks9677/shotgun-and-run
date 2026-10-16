using Consumable;
using ShotgunAndRun.Combat;
using UnityEngine;

namespace ShotgunAndRun.Player
{
    [DisallowMultipleComponent]
    public sealed class ExplosionIndicator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerAim _aim;
        [SerializeField] private ExplosionRangeIndicator _indicator;
        [SerializeField] private PixelLineRenderer _trajectoryRenderer;
        [SerializeField] private Transform _start;

        [Header("Throw Range")]
        [SerializeField, Min(0f)] private float _minDistance = 1f;
        [SerializeField, Min(0f)] private float _maxDistance = 8f;

        [Header("Explosion")]
        [SerializeField, Min(0f)] private float _radius = 2f;

        [Header("Trajectory")]
        [SerializeField, Min(2)] private int _trajectorySegmentCount = 64;
        [SerializeField, Min(0.05f)] private float _flightTime = 0.8f;
        [SerializeField] private Vector2 _gravity = new Vector2(0f, -25f);

        private Vector2[] _trajectoryPoints;

        private void Awake()
        {
            _aim ??= GetComponentInParent<PlayerAim>();
            _indicator ??= GetComponentInChildren<ExplosionRangeIndicator>(true);

            if (_aim == null)
            {
                Debug.LogError("ExplosionIndicator가 PlayerAim을 찾지 못했습니다.", this);
                enabled = false;
                return;
            }

            if (_indicator == null)
            {
                Debug.LogError("ExplosionIndicator가 ExplosionRangeIndicator를 찾지 못했습니다.", this);
                enabled = false;
                return;
            }

            if (_trajectoryRenderer == null)
            {
                Debug.LogError("ExplosionIndicator에 궤적 PixelLineRenderer가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

            _trajectoryPoints = new Vector2[Mathf.Max(2, _trajectorySegmentCount)];
        }

        private void LateUpdate()
        {
            if (_aim == null || _indicator == null || _trajectoryRenderer == null)
            {
                return;
            }

            if (!TryGetThrowTrajectory(
                    out Vector2 startPosition,
                    out Vector2 targetPosition,
                    out Vector2 initialVelocity,
                    out float flightTime,
                    out Vector2 gravity))
            {
                Hide();
                return;
            }

            transform.position = targetPosition;
            _indicator.Show(_radius);
            DrawTrajectory(startPosition, initialVelocity, flightTime, gravity);
        }

        /// <summary>
        /// 현재 조준 상태를 기준으로 투척물 이동에 필요한 포물선 데이터를 반환합니다.
        /// 실제 투척물과 조준 궤적이 같은 계산을 사용하도록 ThrowGrenade에서 호출합니다.
        /// </summary>
        public bool TryGetThrowTrajectory(
            out Vector2 startPosition,
            out Vector2 targetPosition,
            out Vector2 initialVelocity,
            out float flightTime,
            out Vector2 gravity)
        {
            startPosition = default;
            targetPosition = default;
            initialVelocity = default;
            flightTime = 0f;
            gravity = _gravity;

            _aim ??= GetComponentInParent<PlayerAim>();
            if (_aim == null)
            {
                return false;
            }

            startPosition = _start != null
                ? _start.position
                : _aim.transform.position;

            Vector2 direction = _aim.AimDirection;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return false;
            }

            direction.Normalize();

            float maxDistance = Mathf.Max(0f, _maxDistance);
            float minDistance = Mathf.Clamp(_minDistance, 0f, maxDistance);
            float distance = Mathf.Clamp(_aim.AimDistance, minDistance, maxDistance);

            targetPosition = startPosition + direction * distance;
            flightTime = Mathf.Max(0.05f, _flightTime);
            initialVelocity =
                (targetPosition - startPosition - 0.5f * gravity * flightTime * flightTime) /
                flightTime;

            return true;
        }

        private void DrawTrajectory(
            Vector2 startPosition,
            Vector2 initialVelocity,
            float flightTime,
            Vector2 gravity)
        {
            // 궤적 렌더러는 이 오브젝트(착지 지점)의 자식이므로 착지 지점 기준 오프셋으로 넘깁니다.
            Vector2 origin = transform.position;
            int segmentCount = _trajectoryPoints.Length;

            for (int i = 0; i < segmentCount; i++)
            {
                float normalizedTime = i / (segmentCount - 1f);
                float time = normalizedTime * flightTime;

                _trajectoryPoints[i] =
                    startPosition +
                    initialVelocity * time +
                    0.5f * gravity * time * time -
                    origin;
            }

            _trajectoryRenderer.Draw(_trajectoryPoints, false);
        }

        private void OnDisable()
        {
            Hide();
        }

        private void Hide()
        {
            _indicator?.Hide();

            if (_trajectoryRenderer != null)
            {
                _trajectoryRenderer.Hide();
            }
        }

        public void SetRadius(float radius)
        {
            _radius = Mathf.Max(0f, radius);
        }
    }
}
