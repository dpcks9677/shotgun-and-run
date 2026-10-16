using System;
using UnityEngine;

[Serializable]
public class EnemyEntry
{
    // SerializeField
    [SerializeField] private EnemyType _enemyType;
    [SerializeField] private int _count = 1;

    // Property
    public EnemyType EnemyType => _enemyType;
    public int Count => _count;
}
