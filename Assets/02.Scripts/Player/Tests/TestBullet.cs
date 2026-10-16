using ShotgunAndRun.Combat;
using UnityEngine;

namespace ShotgunAndRun.Player
{
    public class TestBullet : MonoBehaviour
    {
        [SerializeField] private int _damage = 10;

        // Update is called once per frame
        private void Update()
        {
            transform.Translate(Vector3.up * Time.deltaTime * 5f);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.gameObject.CompareTag("Player"))
            {
                // IDamageable을 구현한 MonoBehaviour 컴포넌트를 찾는다.
                IDamageable damageable = other.GetComponent<IDamageable>();
                if (damageable == null)
                {
                    return;
                }

                // other.ClosestPoint(): 상대 Collider 표면에서 가장 가까운 지점
                Vector2 hitPoint = other.ClosestPoint(transform.position);
                Vector2 hitDirection =
                    (other.transform.position - transform.position).normalized;

                DamageInfo damageInfo = new(
                    _damage,
                    hitPoint,
                    hitDirection,
                    gameObject);

                damageable.TryTakeDamage(damageInfo);
            }
        }
    }
}
