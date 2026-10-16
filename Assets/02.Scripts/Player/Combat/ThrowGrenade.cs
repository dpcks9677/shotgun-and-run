using ShotgunAndRun.Combat;
using System.Collections;
using ShotgunAndRun.Player;
using UnityEngine;

namespace Consumable
{
    public class ThrowGrenade : MonoBehaviour, IThorwable
    {
        [SerializeField] private PlayerAim _playerAim;
        [SerializeField] private ExplosionIndicator _explosionIndicator;
        [SerializeField] private GameObject _fregmentGrenade;
        [SerializeField] private GameObject _flashBang;

        [Tooltip("스프라이트 원본이 오른쪽을 향하지 않는 경우 보정할 회전값입니다.")]
        [SerializeField] private float _spriteRotationOffset;

        private void Awake()
        {
            _playerAim ??= GetComponentInParent<PlayerAim>();

            if (_explosionIndicator == null)
            {
                Player player = GetComponentInParent<Player>();
                _explosionIndicator = player != null
                    ? player.GetComponentInChildren<ExplosionIndicator>(true)
                    : null;
            }
        }

        public bool TryThrow(ConsumableType type)
        {
            if (!(type == ConsumableType.FlashBang || type == ConsumableType.FragmentGrenade))
            {
                return false;
            }

            GameObject grenade = null;

            switch (type)
            {
                case ConsumableType.FragmentGrenade:
                    grenade = _fregmentGrenade;
                    break;
                case ConsumableType.FlashBang:
                    grenade = _flashBang;
                    break;
            }

            if (grenade == null)
            {
                return false;
            }

            if (_explosionIndicator == null ||
                !_explosionIndicator.TryGetThrowTrajectory(
                    out Vector2 startPosition,
                    out Vector2 targetPosition,
                    out Vector2 initialVelocity,
                    out float flightTime,
                    out Vector2 gravity))
            {
                Debug.LogWarning("투척 궤적을 계산할 ExplosionIndicator를 찾지 못했습니다.", this);
                return false;
            }

            GameObject grenadeInstance = Instantiate(grenade, startPosition, Quaternion.identity);
            StartCoroutine(MoveGrenadeAlongTrajectory(
                grenadeInstance,
                startPosition,
                targetPosition,
                initialVelocity,
                flightTime,
                gravity));

            return true;
        }

        private IEnumerator MoveGrenadeAlongTrajectory(
            GameObject grenade,
            Vector2 startPosition,
            Vector2 targetPosition,
            Vector2 initialVelocity,
            float flightTime,
            Vector2 gravity)
        {
            if (grenade == null)
            {
                yield break;
            }

            Transform grenadeTransform = grenade.transform;
            FragmentGrenade fragmentGrenade = grenade.GetComponent<FragmentGrenade>();
            fragmentGrenade?.PrepareRoll(startPosition, targetPosition);
            // 폭발 범위 원(PixelLineRenderer)은 회전하지 않도록 수류탄 그림만 찾습니다.
            SpriteRenderer spriteRenderer = System.Array.Find(
                grenade.GetComponentsInChildren<SpriteRenderer>(),
                renderer => !renderer.TryGetComponent<PixelLineRenderer>(out _));
            Transform rotationTarget = spriteRenderer != null
                ? spriteRenderer.transform
                : grenadeTransform;

            float elapsed = 0f;
            while (elapsed < flightTime && grenadeTransform != null)
            {
                Vector2 position = EvaluatePosition(
                    startPosition,
                    initialVelocity,
                    gravity,
                    elapsed);

                SetPosition(grenadeTransform, position);
                fragmentGrenade?.SetFlightProgress(elapsed / flightTime);
                RotateAlongVelocity(rotationTarget, initialVelocity + gravity * elapsed);

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (grenadeTransform == null)
            {
                yield break;
            }

            SetPosition(grenadeTransform, targetPosition);
            fragmentGrenade?.SetFlightProgress(1f);
            RotateAlongVelocity(
                rotationTarget,
                initialVelocity + gravity * flightTime);
            fragmentGrenade?.PlayRoll();
        }

        private static Vector2 EvaluatePosition(
            Vector2 startPosition,
            Vector2 initialVelocity,
            Vector2 gravity,
            float time)
        {
            return startPosition +
                   initialVelocity * time +
                   0.5f * gravity * time * time;
        }

        private static void SetPosition(Transform target, Vector2 position)
        {
            Vector3 worldPosition = target.position;
            worldPosition.x = position.x;
            worldPosition.y = position.y;
            target.position = worldPosition;
        }

        private void RotateAlongVelocity(Transform target, Vector2 velocity)
        {
            if (target == null || velocity.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            target.rotation = Quaternion.Euler(
                0f,
                0f,
                angle + _spriteRotationOffset);
        }
        
        public bool TryGetExplosionRadius(
            ConsumableType type,
            out float radius)
        {
            radius = 0f;

            switch (type)
            {
                case ConsumableType.FragmentGrenade:
                {
                    if (_fregmentGrenade == null ||
                        !_fregmentGrenade.TryGetComponent(
                            out FragmentGrenade grenade))
                    {
                        return false;
                    }

                    radius = grenade.Radius;
                    return true;
                }

                case ConsumableType.FlashBang:
                {
                    if (_flashBang == null ||
                        !_flashBang.TryGetComponent(
                            out FlashBang flashBang))
                    {
                        return false;
                    }

                    radius = flashBang.Radius;
                    return true;
                }

                default:
                    return false;
            }
        }
    }
}
