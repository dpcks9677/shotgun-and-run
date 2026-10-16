using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ShotgunAndRun.Enemy
{
    public class EnemyNavigationGrid
    {
        private readonly Tilemap _groundTilemap;
        private readonly Vector2 _bodySize;
        private readonly Collider2D _selfCollider;
        private readonly int _agentCellCost;
        private readonly ContactFilter2D _obstacleFilter;
        private readonly ContactFilter2D _agentFilter;
        private readonly Collider2D[] _obstacleResults = new Collider2D[1];
        private readonly RaycastHit2D[] _castResults = new RaycastHit2D[1];
        private readonly List<Collider2D> _agentResults = new List<Collider2D>();
        private readonly List<RaycastHit2D> _agentCastResults = new List<RaycastHit2D>();

        public EnemyNavigationGrid(Tilemap groundTilemap, Vector2 bodySize,
            Collider2D selfCollider = null, int agentCellCost = 0)
        {
            _groundTilemap = groundTilemap;

            // 월드 기준 Enemy의 Movement Collider 크기를 받기
            _bodySize = bodySize;
            _selfCollider = selfCollider;
            _agentCellCost = agentCellCost;

            _obstacleFilter = new ContactFilter2D()
            {
                useLayerMask = true,
                layerMask = LayerMask.GetMask("Default", "Water", "Obstacle", "Wall"),
                useTriggers = false
            };

            _agentFilter = new ContactFilter2D()
            {
                useLayerMask = true,
                layerMask = LayerMask.GetMask("EnemyBody"),
                useTriggers = false
            };
        }

        // Enemy의 발 및 중심의 월드 좌표를 탐색할 타일 좌표로 변환
        public Vector2Int WorldToCell(Vector2 worldPosition)
        {
            Vector3Int cell = _groundTilemap.WorldToCell(worldPosition);

            return new Vector2Int(cell.x, cell.y);
        }

        // 탐색할 타일의 중심을 월드 좌표로 반환
        public Vector2 GetCellCenter(Vector2Int cellPosition)
        {
            Vector3Int cell = new Vector3Int(cellPosition.x, cellPosition.y, 0);

            return _groundTilemap.GetCellCenterWorld(cell);
        }

        // 걸어갈 수 있는 지형인지 확인
        public bool IsWalkable(Vector2Int cellPosition)
        {
            Vector2 center = GetCellCenter(cellPosition);

            // 1. Movement Collider Box가 차지하는 모든 타일에 Ground가 있는지 검사
            if (!HasGroundUnderBody(center)) return false;

            // 2. 장애물이 있는지 검사
            int hitCount = Physics2D.OverlapBox(center, _bodySize, 0f, _obstacleFilter, _obstacleResults);

            return hitCount == 0;
        }

        // 해당 위치가 땅인지 체크
        private bool HasGroundUnderBody(Vector2 center)
        {
            Vector2 halfSize = _bodySize * 0.5f;

            // 경계에 정확히 닿은 옆 타일까지 포함하지 않도록
            // 최대 좌표를 아주 조금 안쪽으로 옮기기
            const float boundaryInset = 0.001f;

            Vector3Int minCell = _groundTilemap.WorldToCell(center - halfSize);
            Vector3Int maxCell = _groundTilemap.WorldToCell(center + halfSize - Vector2.one * boundaryInset);

            for (int x = minCell.x; x <= maxCell.x; x++)
            {
                for (int y = minCell.y; y <= maxCell.y; y++)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);

                    if (!_groundTilemap.HasTile(cell)) return false;
                }
            }

            return true;
        }

        public bool CanMoveBetween(Vector2Int fromCell, Vector2Int toCell)
        {
            Vector2 fromCenter = GetCellCenter(fromCell);
            Vector2 toCenter = GetCellCenter(toCell);
            Vector2 direction = toCenter - fromCenter;

            float distance = direction.magnitude;

            if (distance <= Mathf.Epsilon) return true;

            int hitCount = Physics2D.BoxCast(
                fromCenter,
                _bodySize,
                0f,
                direction / distance,
                _obstacleFilter,
                _castResults,
                distance);

            return hitCount == 0;
        }

        public int GetAgentCost(Vector2Int cellPosition)
        {
            if (_selfCollider == null || _agentCellCost == 0) return 0;

            Physics2D.OverlapBox(GetCellCenter(cellPosition), _bodySize, 0f,
                _agentFilter, _agentResults);

            foreach (Collider2D collider in _agentResults)
            {
                if (collider.attachedRigidbody != _selfCollider.attachedRigidbody)
                    return _agentCellCost;
            }

            return 0;
        }

        public bool CanMoveDirectly(Vector2 fromCenter, Vector2 toCenter, bool includeAgents = true)
        {
            Vector2 difference = toCenter - fromCenter;
            float distance = difference.magnitude;

            // 직선 구간의 Ground를 일정 간격으로 확인합니다.
            const float sampleInterval = 0.25f;
            int sampleCount = Mathf.Max(
                1, Mathf.CeilToInt(distance / sampleInterval));

            for (int i = 0; i <= sampleCount; i++)
            {
                Vector2 point = Vector2.Lerp(
                    fromCenter, toCenter, (float)i / sampleCount);

                if (!HasGroundUnderBody(point)) return false;
            }

            if (distance <= Mathf.Epsilon) return true;

            // 발밑 Box를 목적지까지 이동시켜 장애물을 검사합니다.
            int hitCount = Physics2D.BoxCast(
                fromCenter, _bodySize, 0f,
                difference / distance,
                _obstacleFilter, _castResults, distance);

            if (hitCount > 0) return false;
            if (!includeAgents || _selfCollider == null) return true;

            Physics2D.BoxCast(fromCenter, _bodySize, 0f,
                difference / distance, _agentFilter, _agentCastResults, distance);

            foreach (RaycastHit2D hit in _agentCastResults)
            {
                if (hit.collider.attachedRigidbody != _selfCollider.attachedRigidbody)
                    return false;
            }

            return true;
        }
    }
}
