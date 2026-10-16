using UnityEngine;

namespace ShotgunAndRun.Player
{
    // 카메라가 이동한 뒤 PlayerAim과 화면 표시 컴포넌트가 갱신되도록 합니다.
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class PlayerCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _offset = new(0f, 0f, -10f);
        [SerializeField, Min(0f)] private float _smoothTime = 0.15f;

        private Vector3 _velocity;
        private PlayerAim _targetAim;

        public Transform Target => _target;

        public void SetTarget(Transform target, bool snapImmediately = true)
        {
            _target = target;
            _targetAim = target != null ? target.GetComponent<PlayerAim>() : null;
            _velocity = Vector3.zero;

            if (snapImmediately)
            {
                SnapToTarget();
            }
        }

        private void Awake()
        {
            ResolveTarget();
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                ResolveTarget();

                if (_target != null)
                {
                    SnapToTarget();
                    return;
                }
            }

            if (_target == null)
            {
                return;
            }

            Vector3 targetPosition = _target.position + _offset;
            if (_smoothTime <= 0f)
            {
                transform.position = targetPosition;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(
                    transform.position,
                    targetPosition,
                    ref _velocity,
                    _smoothTime);
            }

            // ScreenToWorldPoint는 현재 카메라 위치를 사용하므로,
            // 카메라를 이동시킨 직후에 조준점을 갱신해야 합니다.
            ResolveAim();
            _targetAim?.RefreshAimAfterCameraMove();
        }

        private void ResolveTarget()
        {
            if (_target != null)
            {
                return;
            }

            Player player = Player.PlayerInstance;
            if (player != null)
            {
                _target = player.transform;
                _targetAim = player.Aim;
            }

            ResolveAim();
        }

        private void ResolveAim()
        {
            if (_targetAim == null && _target != null)
            {
                _targetAim = _target.GetComponent<PlayerAim>();
            }
        }

        private void SnapToTarget()
        {
            if (_target != null)
            {
                transform.position = _target.position + _offset;
            }
        }

        private void OnValidate()
        {
            _smoothTime = Mathf.Max(0f, _smoothTime);
        }
    }
}
