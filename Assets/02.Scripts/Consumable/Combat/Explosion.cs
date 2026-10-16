using UnityEngine;

namespace Consumable
{
    public class Explosion : MonoBehaviour
    {
        [SerializeField] private LayerMask _targetLayer;

        public Collider2D[] FindTargetsInExplosion(float radius)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(
                transform.position,
                radius,
                _targetLayer);

            return hits;
        }
    }
}
