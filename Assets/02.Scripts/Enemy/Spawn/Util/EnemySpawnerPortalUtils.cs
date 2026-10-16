using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ShotgunAndRun.Enemy
{
    public sealed class EnemyPortalSpawnCheck
    {
        public Vector2 Origin;
        public Vector2 SpawnPosition;
        public bool OriginValid;
        public bool Found;
    }

    public static class EnemySpawnerPortalUtils
    {
        // Enemy 포탈 생성 관련 Field
        public static readonly Vector2 PortalSize = new Vector2(3.125f, 4.25f);
        public static readonly Vector2 PortalCenterOffset = new Vector2(0.0625f, 1.25f);
        public static readonly Vector2 NoPortalSpawnSize = new Vector2(4f, 4f);
        private const int AttemptCount = 100;
        private const int SpawnAttemptCount = 24;
        private const float NearPortalSpawnDepth = 0.5f;

        public static Vector2 GetPreferredSpawnPosition(RaidPortal portal, BoxCollider2D movementCollider)
        {
            Vector2 position = portal.SpawnPosition;
            float scaleY = movementCollider.transform.lossyScale.y;
            float bodyTop = (movementCollider.offset.y + movementCollider.size.y * 0.5f) * scaleY;
            position.y = portal.BlockBounds.min.y - bodyTop;
            return position;
        }

        public static Rect GetSpawnCandidateArea(Bounds blockBounds, EnemyType type,
            BoxCollider2D movementCollider, int side)
        {
            float radius = EnemySpawnerPositionUtils.GetEnemyColliderRadius(type);
            float scaleY = movementCollider.transform.lossyScale.y;
            float bodyTop = (movementCollider.offset.y + movementCollider.size.y * 0.5f) * scaleY;

            return side == 0
                ? Rect.MinMaxRect(blockBounds.min.x, blockBounds.min.y - bodyTop - NearPortalSpawnDepth,
                    blockBounds.max.x, blockBounds.min.y - bodyTop)
                : Rect.MinMaxRect(blockBounds.min.x, blockBounds.max.y + radius,
                    blockBounds.max.x, blockBounds.max.y + radius + NearPortalSpawnDepth);
        }

        public static bool TryGetSpawnPositionNearPortal(RaidPortal portal, EnemyType type,
            BoxCollider2D movementCollider,
            LayerMask obstacleLayer, Tilemap groundTilemap, out Vector2 spawnPosition)
        {
            spawnPosition = GetPreferredSpawnPosition(portal, movementCollider);
            if (EnemySpawnerPositionUtils.IsPossibleSpawnToPosition(
                    spawnPosition, type, movementCollider, obstacleLayer, groundTilemap))
                return true;

            for (int attempt = 0; attempt < SpawnAttemptCount; attempt++)
            {
                int side = attempt % 2;
                Rect area = GetSpawnCandidateArea(portal.BlockBounds, type, movementCollider, side);
                Vector2 candidate = new Vector2(
                    Random.Range(area.xMin, area.xMax),
                    Random.Range(area.yMin, area.yMax));

                bool isPossible = side == 0
                    ? EnemySpawnerPositionUtils.IsPossibleSpawnToPosition(
                        candidate, type, movementCollider, obstacleLayer, groundTilemap)
                    : EnemySpawnerPositionUtils.IsPossibleSpawnToPosition(
                        candidate, type, obstacleLayer, groundTilemap);
                if (!isPossible)
                    continue;

                spawnPosition = candidate;
                return true;
            }

            return false;
        }

        public static void DrawSpawnGizmos(List<Vector2> portalPositions, List<Bounds> blockBounds,
            List<EnemyPortalSpawnCheck> tankerChecks, BoxCollider2D tankerMovementCollider)
        {
            Gizmos.color = Color.yellow;
            foreach (Vector2 portalPosition in portalPositions)
            {
                Gizmos.DrawWireCube(portalPosition + PortalCenterOffset, PortalSize);
            }

            foreach (Bounds bounds in blockBounds)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireCube(bounds.center, bounds.size);

                Gizmos.color = Color.cyan;
                for (int side = 0; side < 2; side++)
                {
                    Rect area = GetSpawnCandidateArea(bounds, EnemyType.Tanker, tankerMovementCollider, side);
                    Gizmos.DrawWireCube(area.center, area.size);
                }
            }

            float tankerRadius = EnemySpawnerPositionUtils.GetEnemyColliderRadius(EnemyType.Tanker);
            foreach (EnemyPortalSpawnCheck check in tankerChecks)
            {
                if (!check.OriginValid)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireSphere(check.Origin, tankerRadius);
                }

                if (!check.Found) continue;

                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(check.SpawnPosition, tankerRadius);
            }
        }

        // 포탈 위치에 포탈을 열고, 플레이어를 바라보게 한다. 프리팹이 없으면 null
        public static RaidPortal OpenPortal(Vector2 portalPosition, RaidPortal portalPrefab, Transform playerTransform)
        {
            if (portalPrefab == null)
            {
                Debug.LogWarning("[EnemySpawner.cs : OpenPortal] - _portalPrefab이 할당되지 않아 포탈 없이 스폰합니다.");
                return null;
            }

            RaidPortal portal = Object.Instantiate(portalPrefab, portalPosition, Quaternion.identity);

            Vector2 lookTarget = playerTransform != null ? (Vector2)playerTransform.position : portalPosition;
            portal.Open(lookTarget);

            return portal;
        }

        public static List<Vector2> GetRandomPortalPositions(List<Rect> spawnableAreas, int chunkLength,
            LayerMask obstacleLayer, Tilemap groundTilemap)
        {
            List<Vector2> portalPositions = new List<Vector2>();

            for (int i = 0; i < chunkLength; i++)
            {
                // 1. 해당 영역이 포탈이 들어갈 만큼 큰 영역인지 확인 및 영역 확정
                for (int areaAttempt = 0; areaAttempt < spawnableAreas.Count; areaAttempt++)
                {
                    // 1.1 영역 선택
                    int areaIndex = (i + areaAttempt) % spawnableAreas.Count;
                    Rect selectedArea = spawnableAreas[areaIndex];

                    // 1.2 해당 영역에 포탈 생성 가능한지 확인
                    bool isSuccess = CanPlacePortal(selectedArea, obstacleLayer, groundTilemap, portalPositions,
                        out Vector2 portalPosition); // 30 번 시도

                    if (!isSuccess) continue; // 안되면 다음 영역

                    // 1.3 가능한 경우 포탈 위치 추가
                    portalPositions.Add(portalPosition);
                    break;
                }
            }

            return portalPositions;
        }

        private static bool CanPlacePortal(Rect areaRect, LayerMask obstacleLayer, Tilemap groundTilemap,
            List<Vector2> createdPortalPositions, out Vector2 portalPosition)
        {
            portalPosition = default;

            // 1. 포탈 크기보다 작은지 판별
            if (areaRect.width < PortalSize.x || areaRect.height < PortalSize.y) return false;

            // 2. 해당 영역의 랜덤 x, y좌표 추출
            Vector2 halfSize = PortalSize * 0.5f;

            for (int attempt = 0; attempt < AttemptCount; attempt++)
            {
                // 2.1 랜덤 x, y 좌표 추출
                float x = UnityEngine.Random.Range(areaRect.xMin + halfSize.x, areaRect.xMax - halfSize.x);
                float y = UnityEngine.Random.Range(areaRect.yMin + halfSize.y, areaRect.yMax - halfSize.y);

                Vector2 boxCenter = new Vector2(x, y);
                Vector2 randomPosition = boxCenter - PortalCenterOffset;

                // 2.2 다른 포탈과 겹치는지 체크
                Rect candidateRect = new Rect(boxCenter - halfSize, PortalSize);
                bool overlapsPortal = false;

                foreach (Vector2 createdPortalPosition in createdPortalPositions)
                {
                    Vector2 createdCenter = createdPortalPosition + PortalCenterOffset;

                    Rect createdRect = new Rect(createdCenter - halfSize, PortalSize);

                    if (!candidateRect.Overlaps(createdRect)) continue;

                    overlapsPortal = true;
                    break;
                }

                if (overlapsPortal) continue;

                // 2.2 장애물 & Ground 체크
                if (!IsPortalOnGround(boxCenter)) continue; // Ground 체크

                // 3. 장애물 or 이동 불가능 지형 체크
                Collider2D obstacle = Physics2D.OverlapBox(boxCenter, PortalSize, 0f, obstacleLayer);

                if (obstacle != null) continue;

                // 4.
                portalPosition = randomPosition;
                return true;
            }

            return false;
        }

        // 포탈 바닥(PortalRadius 사각형)의 중심 · 모서리 · 변 중점 9곳이 모두 그라운드 타일 위인지 확인
        private static bool IsPortalOnGround(Vector2 portalPosition)
        {
            Tilemap ground = GroundTilemapProvider.Ground;
            Vector2 halfSize = PortalSize * 0.5f;

            // 그라운드 타일맵이 없는 씬에서는 기존처럼 충돌체 검사만 사용
            if (ground == null) return true;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    Vector2 point = portalPosition + new Vector2(dx * halfSize.x, dy * halfSize.y);

                    if (!ground.HasTile(ground.WorldToCell(point))) return false;
                }
            }

            return true;
        }
    }
}
