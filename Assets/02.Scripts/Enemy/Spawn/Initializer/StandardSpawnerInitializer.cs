using UnityEngine;

public class StandardSpawnerInitializer : IEnemySpawnInitializer
{
    private readonly float _scoutRange;

    public StandardSpawnerInitializer(float scoutRange)
    {
        _scoutRange = scoutRange;
    }

    public void InitializeSpawnedEnemy(Enemy enemy, Vector2 spawnPosition, Transform playerTransform)
    {
        enemy.ScoutRange = _scoutRange;
        enemy.SpawnPosition = spawnPosition;
        enemy.EnemySensor.SetTargetTransform(playerTransform);
    }
}
