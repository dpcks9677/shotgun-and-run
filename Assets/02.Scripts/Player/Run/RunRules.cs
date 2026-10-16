using UnityEngine;

namespace ShotgunAndRun.Player
{
    public class RunRules : MonoBehaviour
    {
        [Header("Over Time Penalty")]
        [Range(0.01f, 1f)] public float OverTimeMaxHealthDebuffMultiplier = 0.5f;

        [Range(0.01f, 1f)] public float OverTimeMoveSpeedDebuffMultiplier = 0.5f;
    }
}
