using UnityEngine;

public class DestructionSpawnerInitializer : IEnemySpawnInitializer
{
    public void InitializeSpawnedEnemy(Enemy enemy, Vector2 spawnPosition, Transform playerTransform)
    {
        enemy.SpawnPosition = spawnPosition;
        enemy.EnemySensor.SetTargetTransform(playerTransform);
    }
}
