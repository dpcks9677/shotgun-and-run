using UnityEngine;

namespace ShotgunAndRun.Combat
{
    public interface IStunnable
    {
        void Stun(float duration, RuntimeAnimatorController indicatorController);
    }
}
