using UnityEngine;

[CreateAssetMenu(fileName = "EnemyDataTableSO", menuName = "ShotgunAndRun/Enemy/Enemy Data Table")]
public class EnemyDataTableSO : ScriptableObject
{
    public EnemyData[] datas;

    public EnemyData ReadEnemyData(EnemyType type)
    {
        foreach (EnemyData data in datas)
        {
            if (data.enemyType == type)
            {
                return data;
            }
        }

        return null;
    }
}
