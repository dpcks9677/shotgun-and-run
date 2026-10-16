using System;
using UnityEngine;

[Serializable]
public class EnemyWave
{
    [SerializeField] private EnemyChunk[] _enemyChunks;
    [SerializeField] private float _nextWaveRatio = 0.8f;
    [SerializeField] private float _nextWaveDelay = 2.0f;

    private int _maxEnemyCount;
    private int _currentEnemyCount;

    public EnemyChunk[] EnemyChunks => _enemyChunks;
    public float NextWaveRatio => _nextWaveRatio;
    public float NextWaveDelay => _nextWaveDelay;
}
