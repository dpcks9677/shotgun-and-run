using UnityEngine;

namespace ShotgunAndRun.Player
{
    public interface IPelletEmitter
    {
        public void TryFire(Vector2 direction, float aimDistance);
    }
}
