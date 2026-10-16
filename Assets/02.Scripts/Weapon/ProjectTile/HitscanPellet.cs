using System.Collections.Generic;
using ShotgunAndRun.Combat;
using UnityEngine;

namespace ShotgunAndRun.Weapon
{
    // PlayerWeapon의 _projectilePrefab 자리에 넣어 쓰는 히트스캔 펠릿 (프로토타입)
    // PlayerWeapon이 Instantiate 후 transform.right를 설정하므로, Start()에서 그 방향으로 레이캐스트한다.
    // Rigidbody2D가 없으므로 PlayerWeapon의 속도 설정 코드는 자동으로 건너뛴다.
    [DisallowMultipleComponent]
    public sealed class HitscanPellet : MonoBehaviour
    {
        [SerializeField] private BulletTracer _tracerPrefab;

        [Header("Hitscan")]
        [SerializeField, Min(0.1f)] private float _range = 12f;
        [SerializeField] private LayerMask _hitMask = ~((1 << Layers.Water) | Physics2D.IgnoreRaycastLayer);
        [SerializeField] private bool _hitTriggers = true;

        private static readonly List<RaycastHit2D> Hits = new();
        private Transform _shooter;

        public void Initialize(Transform shooter)
        {
            _shooter = shooter;
        }

        public void Fire(
            ProjectTilePool pool,
            Vector2 origin,
            Vector2 direction,
            Transform shooter,
            ShotContext context,
            out bool hitEnemy)
        {
            _shooter = shooter;
            hitEnemy = false;

            float range = context.MaxRange > Mathf.Epsilon
                ? context.MaxRange
                : _range;
            
            Vector2 shotDirection =
                direction.sqrMagnitude > Mathf.Epsilon
                    ? direction.normalized
                    : Vector2.right;

            Vector2 endPoint = origin + shotDirection * range;
            
            HashSet<IDamageable> damagedTargets = new();

            PelletImpactKind impactKind = ProcessHits(
                pool,
                origin,
                direction,
                range,
                context,
                damagedTargets,
                ref hitEnemy,
                ref endPoint);

            pool.SpawnTracer(origin, endPoint, impactKind, context.IsCritical);
            pool.Release(this);
        }

        // 궤적이 끝나는 자리의 착탄 종류를 돌려준다.
        private PelletImpactKind ProcessHits(
            ProjectTilePool pool,
            Vector2 origin,
            Vector2 direction,
            float range,
            ShotContext context,
            HashSet<IDamageable> damagedTargets,
            ref bool hitEnemy,
            ref Vector2 endPoint)
        {
            ContactFilter2D filter = new()
            {
                useLayerMask = true,
                layerMask = _hitMask,
                useTriggers = _hitTriggers
            };

            // 결과는 거리순으로 정렬되어 반환된다.
            Hits.Clear();
            int hitCount = Physics2D.Raycast(
                origin,
                direction,
                filter,
                Hits,
                range);

            int damagedTargetCount = 0;
            for (int i = 0; i < hitCount && i < Hits.Count; i++)
            {
                RaycastHit2D hit = Hits[i];

                // 총구가 플레이어 콜라이더 안에 있으므로 자기 자신은 무시한다.
                if (_shooter != null && hit.collider.transform.IsChildOf(_shooter))
                {
                    continue;
                }

                IDamageable damageable =
                    hit.collider.GetComponentInParent<IDamageable>();

                // IDamageable이 아닌 콜라이더는 벽이나 장애물로 간주하고
                // 그 뒤의 적은 맞히지 않는다.
                if (damageable == null)
                {
                    endPoint = hit.point;
                    return PelletImpactKind.Wall;
                }

                // 한 적이 여러 콜라이더를 갖고 있어도 한 번만 피해를 준다.
                if (!damagedTargets.Add(damageable))
                {
                    continue;
                }

                damagedTargetCount++;
                hitEnemy = true;
                float damageMultiplier = damagedTargetCount == 1 ? 1f : 0.7f;
                int damage = Mathf.RoundToInt(
                    context.PelletDamage * damageMultiplier);

                endPoint = hit.point;
                ApplyDamage(hit, direction, damage);

                // 관통이 없으면 첫 대상에서 끝난다.
                // 관통이 있더라도 두 번째 고유 대상까지만 처리한다.
                if (!context.HasPenetration || damagedTargetCount >= 2)
                {
                    return PelletImpactKind.Enemy;
                }

                // Tanker처럼 관통을 막는 대상은 피해를 받은 뒤 멈춘다.
                if (hit.collider.GetComponentInParent<IPenetrationBlocker>() != null)
                {
                    return PelletImpactKind.Enemy;
                }

                // 관통해 지나가는 대상은 궤적 끝이 아니므로 착탄 효과를 바로 낸다.
                pool.SpawnImpact(PelletImpactKind.Enemy, hit.point);
            }

            return PelletImpactKind.None;
        }

        private void ApplyDamage(RaycastHit2D hit, Vector2 direction, int damage)
        {
            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                GameObject source = _shooter != null ? _shooter.gameObject : gameObject;
                damageable.TryTakeDamage(new DamageInfo(damage, hit.point, direction, source));
            }
        }

        public void ResetState()
        {
            _shooter = null;
        }
    }
}
