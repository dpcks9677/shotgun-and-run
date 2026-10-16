using System;
using UnityEngine;

namespace ShotgunAndRun.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerAim : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private Transform _aimOrigin;
        [SerializeField] private Transform _weaponPivot;
        [SerializeField] private SpriteRenderer _weaponSprite;
        [SerializeField] private SpriteRenderer _playerSprite;

        // 총열 방향 계산에 쓰는 총구입니다. Player가 PlayerWeapon의 FirePoint를 넘겨 줍니다.
        private Transform _muzzle;

        public event Action<Vector2> AimDirectionChanged;

        // 왼쪽을 보는 상태(스프라이트 플립)가 바뀔 때 호출됩니다. 인자는 왼쪽을 보는지 여부입니다.
        public event Action<bool> FacingLeftChanged;

        private Vector2 _pointerScreenPosition;
        private bool _hasPointer;
        private bool _hasAppliedPointerAim;

        public bool IsFacingLeft
        {
            get;
            private set;
        }

        // 총열 방향: 총구를 지나는 총열 직선이 커서를 정확히 지나도록 계산한 방향입니다.
        // AimDirection(AimOrigin → 커서)과 달리 WeaponPivot 위치와 총구의 옆 오프셋을 반영하므로,
        // 총 그림 · 발사 방향 · 산탄 인디케이터가 모두 이 방향을 씁니다.
        public Vector2 WeaponAimDirection
        {
            get;
            private set;
        } = Vector2.right;

        public Vector2 AimDirection
        {
            get;
            private set;
        } = Vector2.right;

        public float AimDistance
        {
            get;
            private set;
        }

        // 커서의 월드 좌표
        public Vector2 AimPoint
        {
            get;
            private set;
        }

        public bool Initialize()
        {
            Camera mainCamera = Camera.main;

            if (_camera == null)
            {
                _camera = mainCamera;
            }

            _aimOrigin ??= transform;
            if (_camera == null)
            {
                Debug.LogError("PlayerAim requires a camera or a camera tagged MainCamera.", this);
                enabled = false;
                return false;
            }

            if (_weaponPivot == null)
            {
                Debug.LogError("PlayerAim requires a weapon pivot.", this);
                enabled = false;
                return false;
            }

            _weaponSprite ??= _weaponPivot.GetComponentInChildren<SpriteRenderer>();
            _playerSprite ??= GetComponentInChildren<PlayerBody>(true).SpriteRenderer;

            ApplyPivotRotation();
            return true;
        }

        public void SetMuzzle(Transform muzzle)
        {
            _muzzle = muzzle;
        }

        // 총구를 지나며 방향이 θ인 총열 직선(WeaponPivot 기준 옆 오프셋 h)이 target을 지나는 θ의 방향을 구합니다.
        // 총구의 위치는 pivot + R(θ)·(t, h)이므로 n(θ)·(target - pivot) = h 이고,
        // 이를 풀면 θ = atan2(target - pivot) - asin(h / 거리)입니다.
        // 목표가 피벗에 너무 가까우면 (거리 ≤ |h|) 해가 없으므로 fallback을 돌려줍니다.
        public static Vector2 SolveBarrelDirection(
            Vector2 pivot,
            Vector2 target,
            float muzzleSideOffset,
            Vector2 fallback)
        {
            Vector2 toTarget = target - pivot;
            float distance = toTarget.magnitude;

            if (distance <= Mathf.Abs(muzzleSideOffset) + 0.0001f)
            {
                return fallback;
            }

            float angle =
                Mathf.Atan2(toTarget.y, toTarget.x) -
                Mathf.Asin(muzzleSideOffset / distance);

            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        public void SetPointerScreenPosition(Vector2 screenPosition)
        {
            _pointerScreenPosition = screenPosition;
            _hasPointer = true;
            RefreshAim();
        }

        // 커서가 멈춰 있어도 카메라가 움직이면 커서의 월드 좌표가 바뀌므로
        // 마지막 화면 좌표로 조준을 다시 계산합니다.
        // 최종 조준점은 PlayerCameraFollow가 카메라를 이동시킨 직후 한 번 더 갱신합니다.
        private void Update()
        {
            if (_hasPointer)
            {
                RefreshAim();
            }
        }

        public void RefreshAimAfterCameraMove()
        {
            if (_hasPointer)
            {
                RefreshAim();
            }
        }

        private void RefreshAim()
        {
            if (_camera == null || _aimOrigin == null)
            {
                return;
            }

            Vector2 screenPosition = _pointerScreenPosition;

            float depth = Mathf.Abs(_camera.transform.position.z - _aimOrigin.position.z);
            Vector3 worldPosition = _camera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, depth));
            AimDistance = Vector2.Distance(_aimOrigin.position, worldPosition);
            AimPoint = worldPosition;
            Vector2 direction = (Vector2)(worldPosition - _aimOrigin.position);

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            direction.Normalize();
            bool changed = Vector2.SqrMagnitude(direction - AimDirection) > 0.000001f;
            AimDirection = direction;
            ApplyPivotRotation();
            _hasAppliedPointerAim = true;

            if (changed)
            {
                AimDirectionChanged?.Invoke(AimDirection);
            }
        }

        private void Reset()
        {
            _aimOrigin = transform;
            _weaponPivot = transform.Find("AimOrigin/WeaponPivot");
        }

        private void OnValidate()
        {
            _aimOrigin ??= transform;
        }

        private void ApplyPivotRotation()
        {
            if (_weaponPivot == null)
            {
                return;
            }

            // 총구가 피벗에서 떨어져 있으므로 피벗과 커서가 너무 가까우면
            // 총구를 정확히 지나는 해가 없습니다. 이 구간에서는 좌우 플립을
            // 유지해 두어 커서가 조금 움직일 때 방향이 튀지 않게 합니다.
            float aimAngle =
                Mathf.Atan2(AimDirection.y, AimDirection.x) *
                Mathf.Rad2Deg;

            bool isFacingLeft = aimAngle > 90f || aimAngle < -90f;
            float muzzleSideMagnitude = 0f;
            if (_muzzle != null)
            {
                muzzleSideMagnitude = Mathf.Abs(
                    _weaponPivot.InverseTransformPoint(_muzzle.position).y);
            }

            if (_hasPointer && _hasAppliedPointerAim &&
                Vector2.Distance(_weaponPivot.position, AimPoint) <=
                muzzleSideMagnitude + 0.0001f)
            {
                isFacingLeft = IsFacingLeft;
            }

            if (_weaponSprite != null)
            {
                _weaponSprite.flipY = isFacingLeft;
            }

            if (_playerSprite != null)
            {
                _playerSprite.flipX = isFacingLeft;
            }

            // FirePoint의 좌우 위치를 먼저 갱신한 뒤 최종 총열 방향을 계산합니다.
            if (isFacingLeft != IsFacingLeft)
            {
                IsFacingLeft = isFacingLeft;
                FacingLeftChanged?.Invoke(isFacingLeft);
            }

            // 총구의 옆 오프셋은 무기가 뒤집히면 부호가 바뀝니다. (PlayerWeapon.SetFacingLeft와 같은 규칙)
            float sideOffset = 0f;
            if (_muzzle != null)
            {
                float magnitude = Mathf.Abs(_weaponPivot.InverseTransformPoint(_muzzle.position).y);
                sideOffset = isFacingLeft ? -magnitude : magnitude;
            }

            // 해가 없거나 아직 커서 좌표가 없을 때도 플레이어 좌우 방향과
            // 동일한 방향을 유지해 모든 발사 관련 표시가 일치하도록 합니다.
            Vector2 fallbackDirection = isFacingLeft ? Vector2.left : Vector2.right;
            WeaponAimDirection = _hasPointer
                ? SolveBarrelDirection(
                    _weaponPivot.position,
                    AimPoint,
                    sideOffset,
                    fallbackDirection)
                : fallbackDirection;

            float weaponAngle =
                Mathf.Atan2(WeaponAimDirection.y, WeaponAimDirection.x) *
                Mathf.Rad2Deg;

            _weaponPivot.rotation =
                Quaternion.Euler(0f, 0f, weaponAngle);
        }
    }
}
