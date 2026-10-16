using UnityEngine;

namespace ShotgunAndRun.Enemy
{
    [CreateAssetMenu(fileName = "RaidRoundDataTableSO", menuName = "ShotgunAndRun/Enemy/Raid Round Data Table")]
    public class RaidRoundDataTableSO : ScriptableObject
    {
        public RaidRoundData[] datas;
    }
}
