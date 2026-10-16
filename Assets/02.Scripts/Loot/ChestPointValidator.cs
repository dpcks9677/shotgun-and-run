using System.Collections.Generic;
using ShotgunAndRun.Combat;
using UnityEngine;

namespace ShotgunAndRun.Loot
{
    // 씬의 상자 스폰 지점을 모으고, 상자 자리가 막혔는지 · 커넥터(홀) 상자가 벽 앞에 있는지 검사합니다.
    // 크기는 배치할 때 정해 두므로 런타임에는 검사하지 않습니다 (ChestPointTilemap 기즈모 · 컨텍스트 메뉴에서 사용).
    public static class ChestPointValidator
    {
        // 같은 씬이면 같은 순서가 나오도록 지점 순서를 고정합니다 (LootSpawner 시드 재현).
        public static List<ChestPointPlacement> CollectScenePoints()
        {
            List<ChestPointPlacement> points = new List<ChestPointPlacement>();
            foreach (ChestSpawnPoint point in Object.FindObjectsByType<ChestSpawnPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                points.Add(point.ToPlacement());
            }

            foreach (ChestPointTilemap tilemap in Object.FindObjectsByType<ChestPointTilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                tilemap.CollectPoints(points);
            }

            points.Sort((a, b) =>
            {
                int result = a.Kind.CompareTo(b.Kind);
                if (result != 0) return result;

                result = string.CompareOrdinal(a.OwnerId, b.OwnerId);
                if (result != 0) return result;

                Vector3 pa = a.Position;
                Vector3 pb = b.Position;
                result = pa.x.CompareTo(pb.x);
                return result != 0 ? result : pa.y.CompareTo(pb.y);
            });
            return points;
        }

        // 벽 · 소품 콜라이더(Default · Water · Obstacle · Wall, 트리거 제외)나 다른 지점의 상자 자리와 겹치는 지점의 번호
        public static List<int> FindBlocked(IReadOnlyList<ChestPointPlacement> points)
        {
            int layerMask = (1 << Layers.Default) | (1 << Layers.Water) | (1 << Layers.Obstacle) | (1 << Layers.Wall);
            ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(layerMask);
            List<Collider2D> hits = new List<Collider2D>();
            List<List<Vector2[]>> composites = CollectCompositePaths(layerMask);

            List<int> blocked = new List<int>();
            for (int i = 0; i < points.Count; i++)
            {
                Rect footprint = points[i].Footprint;
                // 타일맵 합성 콜라이더는 Polygons라 OverlapBox가 안쪽까지 잡습니다.
                // 경로 판정은 Outlines로 되돌아간 합성 콜라이더가 있어도 상자 자리가 통째로 안쪽에 들어간 경우를 잡기 위한 대비입니다.
                bool isBlocked = Physics2D.OverlapBox(footprint.center, footprint.size * 0.9f, 0f, filter, hits) > 0
                    || IsInsideAny(footprint.center, composites);
                for (int j = 0; !isBlocked && j < points.Count; j++)
                {
                    isBlocked = j != i && footprint.Overlaps(points[j].Footprint);
                }

                if (isBlocked)
                {
                    blocked.Add(i);
                }
            }

            return blocked;
        }

        // 커넥터(홀) 지점은 벽 앞에 놓습니다: 상자 자리의 한 변이 통째로 벽(Wall 레이어)에 붙어야 합니다.
        // Large는 북 · 남쪽 변, TallEast는 서쪽 변, TallWest는 동쪽 변, Small은 아무 변.
        public static List<int> FindNotAgainstWall(IReadOnlyList<ChestPointPlacement> points)
        {
            int wallMask = 1 << Layers.Wall;
            ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(wallMask);
            List<Collider2D> hits = new List<Collider2D>();
            List<List<Vector2[]>> composites = CollectCompositePaths(wallMask);

            bool IsWall(Vector2 cell)
            {
                return IsInsideAny(cell, composites) || Physics2D.OverlapBox(cell, new Vector2(0.5f, 0.5f), 0f, filter, hits) > 0;
            }

            // 상자 자리 바깥에 붙은 한 변의 칸들이 모두 벽인지
            bool SideIsWall(Rect footprint, Vector2Int side)
            {
                int count = side.x != 0 ? (int)footprint.height : (int)footprint.width;
                for (int k = 0; k < count; k++)
                {
                    Vector2 cell = side.x != 0
                        ? new Vector2(side.x > 0 ? footprint.xMax + 0.5f : footprint.xMin - 0.5f, footprint.yMin + 0.5f + k)
                        : new Vector2(footprint.xMin + 0.5f + k, side.y > 0 ? footprint.yMax + 0.5f : footprint.yMin - 0.5f);
                    if (!IsWall(cell)) return false;
                }

                return true;
            }

            List<int> result = new List<int>();
            for (int i = 0; i < points.Count; i++)
            {
                ChestPointPlacement point = points[i];
                if (point.Kind != ChestPointKind.Connector) continue;

                Rect footprint = point.Footprint;
                bool againstWall;
                switch (point.Size)
                {
                    case ChestPointSize.Large:
                        againstWall = SideIsWall(footprint, Vector2Int.up) || SideIsWall(footprint, Vector2Int.down);
                        break;
                    case ChestPointSize.TallEast:
                        againstWall = SideIsWall(footprint, Vector2Int.left);
                        break;
                    case ChestPointSize.TallWest:
                        againstWall = SideIsWall(footprint, Vector2Int.right);
                        break;
                    default:
                        againstWall = SideIsWall(footprint, Vector2Int.up) || SideIsWall(footprint, Vector2Int.down)
                            || SideIsWall(footprint, Vector2Int.left) || SideIsWall(footprint, Vector2Int.right);
                        break;
                }

                if (!againstWall)
                {
                    result.Add(i);
                }
            }

            return result;
        }

        // 합성 콜라이더마다 월드 좌표 경로 목록
        private static List<List<Vector2[]>> CollectCompositePaths(int layerMask)
        {
            List<List<Vector2[]>> composites = new List<List<Vector2[]>>();
            List<Vector2> path = new List<Vector2>();
            foreach (CompositeCollider2D composite in Object.FindObjectsByType<CompositeCollider2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!composite.enabled || composite.isTrigger || (layerMask & (1 << composite.gameObject.layer)) == 0) continue;

                List<Vector2[]> paths = new List<Vector2[]>(composite.pathCount);
                for (int i = 0; i < composite.pathCount; i++)
                {
                    composite.GetPath(i, path);
                    Vector2[] world = new Vector2[path.Count];
                    for (int j = 0; j < path.Count; j++)
                    {
                        world[j] = composite.transform.TransformPoint(path[j]);
                    }

                    paths.Add(world);
                }

                composites.Add(paths);
            }

            return composites;
        }

        // 짝홀 규칙: 한 합성 콜라이더의 모든 경로(바깥 · 구멍)를 가로지르는 횟수가 홀수면 안쪽
        private static bool IsInsideAny(Vector2 point, List<List<Vector2[]>> composites)
        {
            foreach (List<Vector2[]> paths in composites)
            {
                bool inside = false;
                foreach (Vector2[] path in paths)
                {
                    for (int i = 0, j = path.Length - 1; i < path.Length; j = i++)
                    {
                        Vector2 a = path[i];
                        Vector2 b = path[j];
                        if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                        {
                            inside = !inside;
                        }
                    }
                }

                if (inside) return true;
            }

            return false;
        }
    }
}
