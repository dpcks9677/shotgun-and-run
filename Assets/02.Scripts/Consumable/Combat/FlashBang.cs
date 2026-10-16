using System.Collections;
using System.Collections.Generic;
using ShotgunAndRun.Combat;
using UnityEngine;

namespace Consumable
{
    public class FlashBang : MonoBehaviour
    {
        [SerializeField] private float _radius;
        public float Radius => _radius;
        [SerializeField] private float _stunTime;
        [SerializeField] private float _countdown;
        [SerializeField] private Explosion _explosion;
        [SerializeField] private GameObject _vfx;
        [SerializeField] private ExplosionRangeIndicator _rangeIndicator;
        [SerializeField] private RuntimeAnimatorController _stunController;

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

        private IEnumerator Exploded()
        {
            yield return new WaitForSeconds(_countdown);

            Collider2D[] hits = _explosion.FindTargetsInExplosion(_radius);
            HashSet<IStunnable> stunnedTargets = new HashSet<IStunnable>();

            foreach (Collider2D hit in hits)
            {
                IStunnable target = hit.GetComponentInParent<IStunnable>();
                if (target != null && stunnedTargets.Add(target))
                {
                    target.Stun(_stunTime, _stunController);
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
