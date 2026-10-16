using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class EnemySpawnerPositionUtils
{
    private const float CameraSpawnMargin = 1f;

    // Random Spawn Attribute
    private const float NonTankerEnemyColliderRadius = 0.5f;
    private const float TankerEnemyColliderRadius = 1.0f;
    private const float EnemyColliderRadiusMargin = 0.2f;
    private const int AttemptCount = 100;

    public static bool TryGetRandomSpawnPositionInArea(Vector2 centerPosition, EnemyType type,
        Vector2 spawnRectSizeVector,
        out Vector2 spawnPosition, LayerMask checkLayerMask, Tilemap groundTilemap)
    {
        float margin = GetEnemyColliderRadius(type);

        Vector2 offset = Vector2.zero;

        for (int i = 0; i < AttemptCount; i++)
        {
            Vector2 halfSpawnRectangle = spawnRectSizeVector * 0.5f;

            float x = Random.Range(-halfSpawnRectangle.x + margin, halfSpawnRectangle.x - margin);
            float y = Random.Range(-halfSpawnRectangle.y + margin, halfSpawnRectangle.y - margin);

            offset = new Vector2(x, y);

            Vector2 randomSpawnPosition = centerPosition + offset;

            bool isPossible = IsPossibleSpawnToPosition(randomSpawnPosition, type, checkLayerMask, groundTilemap);

            if (isPossible)
            {
                Debug.Log($"[EnemySpawner.cs : GetRandomSpawnPosition] - {randomSpawnPosition}");

                spawnPosition = randomSpawnPosition;
                return true;
            }
        }

        spawnPosition = default; // Vector2의 default는 (0, 0)

        return false;
    }

    public static bool IsPossibleSpawnToPosition(Vector2 position, EnemyType type, LayerMask checkLayerMask,
        Tilemap groundTilemap)
    {
        return IsObstacleFree(position, type, checkLayerMask) &&
               IsOnGround(position, type, groundTilemap);
    }

    public static bool IsPossibleSpawnToPosition(Vector2 position, EnemyType type, BoxCollider2D movementCollider,
        LayerMask checkLayerMask, Tilemap groundTilemap)
    {
        Vector2 scale = movementCollider.transform.lossyScale;
        Vector2 offset = Vector2.Scale(movementCollider.offset, scale);
        Vector2 size = Vector2.Scale(movementCollider.size,
            new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y)));

        return Physics2D.OverlapBox(position + offset, size, 0f, checkLayerMask) == null &&
               IsOnGround(position, type, groundTilemap);
    }

    private static bool IsObstacleFree(Vector2 position, EnemyType type, LayerMask checkLayerMask)
    {
        // 1. 충돌 반경 반지름 설정
        float checkRadius = GetEnemyColliderRadius(type);

        // 2. position 기준 충돌 반경에 Collider 체크
        Collider2D hit = Physics2D.OverlapCircle(position, checkRadius, checkLayerMask);

        // 3. Collider 없으면 가능
        return hit == null;
    }

    public static bool IsOnGround(Vector2 position, EnemyType type, Tilemap groundTilemap)
    {
        if (groundTilemap == null)
        {
            Debug.Log($"[EnemySpawner.cs : IsOnGround] - 그라운드 타일맵 is null");
            return false;
        }

        float radius = GetEnemyColliderRadius(type);
        float diagonal = radius / Mathf.Sqrt(2f); // 대각선

        return HasGroundTile(position, groundTilemap) &&
               HasGroundTile(position + Vector2.up * radius, groundTilemap) &&
               HasGroundTile(position + Vector2.down * radius, groundTilemap) &&
               HasGroundTile(position + Vector2.left * radius, groundTilemap) &&
               HasGroundTile(position + Vector2.right * radius, groundTilemap) &&
               HasGroundTile(position + new Vector2(diagonal, diagonal), groundTilemap) &&
               HasGroundTile(position + new Vector2(-diagonal, diagonal), groundTilemap) &&
               HasGroundTile(position + new Vector2(diagonal, -diagonal), groundTilemap) &&
               HasGroundTile(position + new Vector2(-diagonal, -diagonal), groundTilemap);
    }

    private static bool HasGroundTile(Vector2 worldPosition, Tilemap groundTilemap)
    {
        // 1. 월드 좌표를 TileMap의 Cell 좌표로 변환
        Vector3Int cell = groundTilemap.WorldToCell(worldPosition);

        // 2. 해당 Cell에 Ground Tile이 존재하는지 확인
        return groundTilemap.HasTile(cell);
    }

    public static float GetEnemyColliderRadius(EnemyType type)
    {
        return (type == EnemyType.Tanker ? TankerEnemyColliderRadius : NonTankerEnemyColliderRadius) +
               EnemyColliderRadiusMargin;
    }

    private static Rect GetSpawnableAreaRect(Vector2 centerPosition, Vector2 spawnableAreaSizeVector)
    {
        // Vector2 center = transform.position;
        // Vector2 halfSize = _spawnAreaSizeVector * 0.5f;

        Vector2 center = centerPosition;
        Vector2 halfSize = spawnableAreaSizeVector * 0.5f;

        return Rect.MinMaxRect(center.x - halfSize.x, center.y - halfSize.y, center.x + halfSize.x,
            center.y + halfSize.y);
    }

    private static Rect GetPlayerCenterRect(Vector2 playerPosition)
    {
        const float halfSize = 10f;
        return Rect.MinMaxRect(playerPosition.x - halfSize, playerPosition.y - halfSize, playerPosition.x + halfSize,
            playerPosition.y + halfSize);
    }

    public static List<Rect> GetSpawnableAreas(Vector2 centerPosition, Vector2 spawnableAreaSizeVector,
        Vector2 playerPosition)
    {
        Rect spawnAreaRect = EnemySpawnerPositionUtils.GetSpawnableAreaRect(centerPosition, spawnableAreaSizeVector);
        Rect playerCenterRect = EnemySpawnerPositionUtils.GetPlayerCenterRect(playerPosition);

        Rect excludedRect = Rect.MinMaxRect(playerCenterRect.xMin - CameraSpawnMargin,
            playerCenterRect.yMin - CameraSpawnMargin, playerCenterRect.xMax + CameraSpawnMargin,
            playerCenterRect.yMax + CameraSpawnMargin);

        List<Rect> areas = new List<Rect>();

        float overlapLeft = Mathf.Max(spawnAreaRect.xMin, excludedRect.xMin);
        float overlapRight = Mathf.Min(spawnAreaRect.xMax, excludedRect.xMax);
        float overlapBottom = Mathf.Max(spawnAreaRect.yMin, excludedRect.yMin);
        float overlapTop = Mathf.Min(spawnAreaRect.yMax, excludedRect.yMax);

        // 순서대로: 상 하 좌 우
        AddValidRect(areas, spawnAreaRect.xMin, overlapTop, spawnAreaRect.xMax, spawnAreaRect.yMax);
        AddValidRect(areas, spawnAreaRect.xMin, spawnAreaRect.yMin, spawnAreaRect.xMax, overlapBottom);
        AddValidRect(areas, spawnAreaRect.xMin, overlapBottom, overlapLeft, overlapTop);
        AddValidRect(areas, overlapRight, overlapBottom, spawnAreaRect.xMax, overlapTop);

        return areas;
    }

    private static void AddValidRect(List<Rect> areas, float xMin, float yMin, float xMax, float yMax)
    {
        if (xMax <= xMin || yMax <= yMin) return;

        areas.Add(Rect.MinMaxRect(xMin, yMin, xMax, yMax));
    }
}
