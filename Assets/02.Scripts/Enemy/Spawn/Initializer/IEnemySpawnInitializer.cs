using UnityEngine;

public interface IEnemySpawnInitializer
{
    public void InitializeSpawnedEnemy(Enemy enemy, Vector2 spawnPosition, Transform playerTransform);
}

