using System.Collections.Generic;
using UnityEngine;

namespace ShotgunAndRun.Enemy
{
    public class EnemyPathfinder
    {
        private readonly EnemyNavigationGrid _grid;

        // 경로가 없을 때 탐색 길어지는 것 방지
        private const int MaxSearchCount = 700;
        private const int TargetSearchRadius = 2;

        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left,
        };

        public EnemyPathfinder(EnemyNavigationGrid grid)
        {
            _grid = grid;
        }

        public bool TryFindPath(Vector2Int startCell, Vector2Int targetCell, out List<Vector2Int> path)
        {
            // 1. 원래 목적지까지 갈 수 있거나, 탐색 한도 안에 닿지 않을 만큼 멀다면 그 경로를 그대로
            if (TryFindExactPath(startCell, targetCell, out path, out bool isSearchLimited) || isSearchLimited) return true;

            // 시작 칸 테스트
            if (!_grid.IsWalkable(startCell)) return false;

            // 2. 목적지 주변을 가까운 거리부터 탐색
            for (int radius = 1; radius <= TargetSearchRadius; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        // 상하좌우 이동 횟수가 현재 radius인 칸만 검사
                        int distance = Mathf.Abs(dx) + Mathf.Abs(dy);

                        if (distance != radius) continue;

                        Vector2Int candidateCell = targetCell + new Vector2Int(dx, dy);

                        // 현재 셀 제외
                        if (candidateCell == startCell) continue;

                        if (!TryFindExactPath(startCell, candidateCell, out List<Vector2Int> candidatePath,
                                out bool isCandidateLimited) && !isCandidateLimited) continue;

                        path = candidatePath;
                        return true;
                    }
                }
            }

            path = new List<Vector2Int>();
            return false;
        }


        // 탐색 한도에 걸리면 false와 함께 목적지에 가장 가까이 간 칸까지의 경로를 돌려줍니다.
        public bool TryFindExactPath(Vector2Int startCell, Vector2Int targetCell, out List<Vector2Int> path,
            out bool isSearchLimited)
        {
            // 반환할 경로 변수
            path = new List<Vector2Int>();
            isSearchLimited = false;

            if (!_grid.IsWalkable(startCell) || !_grid.IsWalkable(targetCell)) return false;

            // 0. OPEN / CLOSED 확인을 위한
            List<EnemyPathNode> openNodes = new List<EnemyPathNode>();
            HashSet<Vector2Int> closedCells = new HashSet<Vector2Int>();

            Dictionary<Vector2Int, EnemyPathNode> nodes = new Dictionary<Vector2Int, EnemyPathNode>();

            // 1. 시작 노드 세팅 및 추가
            EnemyPathNode startNode = new EnemyPathNode(startCell);
            startNode.GCost = 0;
            startNode.HCost = GetEstimatedCost(startCell, targetCell);

            nodes.Add(startCell, startNode);
            openNodes.Add(startNode);

            // 2. A * 알고리즘 반복 조건: OPEN 상태의 Node가 존재할 경우 + 탐색 count
            int searchCount = 0;
            EnemyPathNode closestNode = startNode;

            while (openNodes.Count > 0 && searchCount < MaxSearchCount)
            {
                searchCount++;

                // 2.0 먼저 현재 OPEN에 들어온 노드들 중 가장 FCost가 낮은 것 찾기
                EnemyPathNode currentNode = GetLowestCostNode(openNodes);

                // 2.1 현재 노드: 목적지인 경우 - Path를 생성
                if (currentNode.CellPosition == targetCell)
                {
                    path = BuildPath(currentNode);
                    return true;
                }

                if (currentNode.HCost < closestNode.HCost) closestNode = currentNode;

                // 2.2 현재 노드: 목적지가 아닌 경우 - 탐색 완료 후 주변 타일 확인
                openNodes.Remove(currentNode);
                closedCells.Add(currentNode.CellPosition);

                foreach (Vector2Int direction in Directions)
                {
                    Vector2Int nextCell = currentNode.CellPosition + direction;

                    // 이미 탐색한 곳 or 갈 수 없는 곳
                    if (closedCells.Contains(nextCell)) continue;
                    if (!_grid.IsWalkable(nextCell)) continue;

                    // 다음 칸까지 이동하는 구간도 장애물과 겹치지 않아야 함.
                    if (!_grid.CanMoveBetween(currentNode.CellPosition, nextCell)) continue;

                    if (!nodes.TryGetValue(nextCell, out EnemyPathNode nextNode))
                    {
                        nextNode = new EnemyPathNode(nextCell);
                        nextNode.HCost = GetEstimatedCost(nextCell, targetCell);
                        nodes.Add(nextCell, nextNode);
                    }

                    int nextGCost = currentNode.GCost + 10 + _grid.GetAgentCost(nextCell);

                    if (nextGCost >= nextNode.GCost) continue;

                    nextNode.GCost = nextGCost;
                    nextNode.Parent = currentNode;

                    if (!openNodes.Contains(nextNode))
                    {
                        openNodes.Add(nextNode);
                    }
                }
            }

            // 3. OPEN이 남았다면 목적지가 막힌 것이 아니라 탐색 한도에 걸린 것
            isSearchLimited = openNodes.Count > 0;

            if (isSearchLimited) path = BuildPath(closestNode);

            return false;
        }

        private static int GetEstimatedCost(Vector2Int currentCell, Vector2Int targetCell)
        {
            int xDistance = Mathf.Abs(targetCell.x - currentCell.x);
            int yDistance = Mathf.Abs(targetCell.y - currentCell.y);

            return (xDistance + yDistance) * 10;
        }

        private static EnemyPathNode GetLowestCostNode(List<EnemyPathNode> openNodes)
        {
            EnemyPathNode bestNode = openNodes[0];

            foreach (EnemyPathNode node in openNodes)
            {
                if (node.FCost < bestNode.FCost || (node.FCost == bestNode.FCost && node.HCost < bestNode.HCost))
                {
                    bestNode = node;
                }
            }

            return bestNode;
        }

        private static List<Vector2Int> BuildPath(EnemyPathNode targetNode)
        {
            List<Vector2Int> path = new List<Vector2Int>();
            EnemyPathNode currentNode = targetNode;

            while (currentNode != null)
            {
                path.Add(currentNode.CellPosition);
                currentNode = currentNode.Parent;
            }

            path.Reverse();
            return path;
        }
    }
}
