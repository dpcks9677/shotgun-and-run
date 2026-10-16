using System.Collections;
using ShotgunAndRun.Combat;
using UnityEngine;

namespace Consumable
{
    public class FragmentGrenade : MonoBehaviour
    {
        [SerializeField] private float _throwDistance;
        [SerializeField] private float _radius;
        [SerializeField] private float _damage;
        [SerializeField] private float _countdown;
        [SerializeField] private Explosion _explosion;
        [SerializeField] private GameObject _vfx;
        [SerializeField] private ExplosionRangeIndicator _rangeIndicator;
        [SerializeField] private Transform _visual;
        [SerializeField] private float _rollDistance = 0.45f;
        [SerializeField] private float _rollDuration = 0.15f;

        private Vector2 _rollOffset;
        private float _rollAngle;

        public float Radius => _radius;
        
        private void Awake()
        {
            _explosion = GetComponent<Explosion>();
            _rangeIndicator = GetComponentInChildren<ExplosionRangeIndicator>();
        }

        private void Start()
        {
            _rangeIndicator.Show(_radius);
            _rangeIndicator.StartBlink();
            StartCoroutine(Exploded());
        }

        public void PrepareRoll(Vector2 startPosition, Vector2 targetPosition)
        {
            Vector2 displacement = targetPosition - startPosition;
            float distance = Mathf.Min(_rollDistance, displacement.magnitude);
            _rollOffset = -displacement.normalized * distance;

            float rollRadius = _visual.GetComponent<SpriteRenderer>().sprite.bounds.extents.x;
            _rollAngle = -Mathf.Sign(displacement.x) * distance / rollRadius * Mathf.Rad2Deg;
        }

        public void SetFlightProgress(float progress)
        {
            _visual.localPosition = Vector2.Lerp(Vector2.zero, _rollOffset, progress);
        }

        public void PlayRoll()
        {
            StartCoroutine(RollRoutine());
        }

        private IEnumerator RollRoutine()
        {
            Quaternion startRotation = _visual.localRotation;
            float elapsedTime = 0f;

            while (elapsedTime < _rollDuration)
            {
                float progress = elapsedTime / _rollDuration;
                float easedProgress = 1f - (1f - progress) * (1f - progress);
                _visual.localPosition = Vector2.Lerp(_rollOffset, Vector2.zero, easedProgress);
                _visual.localRotation = startRotation * Quaternion.Euler(0f, 0f, _rollAngle * easedProgress);

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            _visual.localPosition = Vector2.zero;
            _visual.localRotation = startRotation * Quaternion.Euler(0f, 0f, _rollAngle);
        }

        private IEnumerator Exploded()
        {
            yield return new WaitForSeconds(_countdown);
            Collider2D[] hits = _explosion.FindTargetsInExplosion(_radius);

            foreach (Collider2D hit in hits)
            {
                IDamageable damageable = hit.GetComponent<IDamageable>();

                if (damageable != null)
                {
                    DamageInfo damageInfo = new DamageInfo((int)_damage, hit.ClosestPoint(transform.position),
                        (hit.transform.position - transform.position), gameObject);
                    damageable.TryTakeDamage(damageInfo);
                }
            }

            // 회전 없이 생성한다: 수류탄은 비행 방향으로 루트가 돌아가 있지만 폭발은 방향이 없다.
            GameObject vfx = Instantiate(_vfx, transform.position, Quaternion.identity);
            if (vfx.TryGetComponent(out GrenadeExplosionVFX explosionVfx))
            {
                explosionVfx.Play(_radius);
            }
            _rangeIndicator.Hide();
            Destroy(gameObject);

            yield return null;
        }
    }
}
