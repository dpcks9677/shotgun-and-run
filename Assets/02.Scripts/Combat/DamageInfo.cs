using UnityEngine;

namespace ShotgunAndRun.Combat
{
    public readonly struct DamageInfo
    {
        public int Damage
        {
            get;
        }

        public Vector2 HitPoint
        {
            get;
        }

        public Vector2 HitDirection
        {
            get;
        }

        public GameObject Source
        {
            get;
        }

        public DamageInfo(int damage, Vector2 hitPoint, Vector2 hitDirection, GameObject source = null)
        {
            Damage = Mathf.Max(0, damage);
            HitPoint = hitPoint;
            HitDirection = hitDirection;
            Source = source;
        }
    }
}
