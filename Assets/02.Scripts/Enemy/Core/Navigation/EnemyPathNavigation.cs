using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ShotgunAndRun.Enemy
{
    public class EnemyPathNavigation : MonoBehaviour
    {
        private float _repathInterval = 0.3f;
        private float _arrivalDistance = 0.05f;
        private float _lookAheadDistance = 0f;
        [SerializeField] private int _agentCellCost = 60;

        private Collider2D _bodyCollider;
        private EnemyNavigationGrid _grid;
        private EnemyPathfinder _pathfinder;

        private List<Vector2Int> _path = new List<Vector2Int>();
        private int _pathIndex;
        private float _nextRepathTime;

        public void Initialize(Tilemap groundTilemap, Collider2D bodyCollider)
        {
            _bodyCollider = bodyCollider;
            _grid = new EnemyNavigationGrid(
                groundTilemap, bodyCollider.bounds.size, bodyCollider, _agentCellCost);

            _pathfinder = new EnemyPathfinder(_grid);
            ClearPath();
        }

        // 목적지는 발밑 Box 중심 기준의 월드 좌표입니다.
        public bool TryGetDirection(Vector2 targetPosition, float moveSpeed, bool isBlocked, out Vector2 direction)
        {
            direction = Vector2.zero;

            Vector2 currentPosition = _bodyCollider.bounds.center;

            float arrivalDistance = Mathf.Max(_arrivalDistance, moveSpeed * Time.fixedDeltaTime);

            // 경로가 끝났거나 현재 이동점에 도착했는지 확인합니다.
            bool canRepath = isBlocked || _pathIndex >= _path.Count;

            if (!canRepath)
            {
                Vector2 waypoint = _grid.GetCellCenter(_path[_pathIndex]);

                canRepath = Vector2.Distance(currentPosition, waypoint) <= arrivalDistance;
            }

            // 칸 사이 이동 중에는 경로를 바꾸지 않습니다.
            if (canRepath && Time.time >= _nextRepathTime)
            {
                Vector2Int startCell = _grid.WorldToCell(currentPosition);
                Vector2Int targetCell = _grid.WorldToCell(targetPosition);

                _pathfinder.TryFindPath(startCell, targetCell, out _path);

                // 경로의 첫 칸은 현재 서 있는 칸이므로 그 중심으로 되돌아가지 않습니다.
                _pathIndex = _path.Count > 0 ? 1 : 0;
                _nextRepathTime = Time.time + _repathInterval;
            }

            // 도착한 칸은 건너뛰고 다음 칸으로 향합니다.
            while (_pathIndex < _path.Count)
            {
                Vector2 waypoint = _grid.GetCellCenter(_path[_pathIndex]);

                if (Vector2.Distance(currentPosition, waypoint) > arrivalDistance)
                    break;

                _pathIndex++;
            }

            if (_pathIndex >= _path.Count)
                return false;

            float lookAheadDistanceSquared = _lookAheadDistance * _lookAheadDistance;

            for (int index = _pathIndex + 1; index < _path.Count; index++)
            {
                Vector2 candidate = _grid.GetCellCenter(_path[index]);

                if ((candidate - currentPosition).sqrMagnitude > lookAheadDistanceSquared)
                    break;

                if (!_grid.CanMoveDirectly(currentPosition, candidate))
                    break;

                _pathIndex = index;
            }

            Vector2 target = _grid.GetCellCenter(_path[_pathIndex]);
            direction = (target - currentPosition).normalized;
            return true;
        }

        public void ClearPath()
        {
            _path.Clear();
            _pathIndex = 0;
            _nextRepathTime = 0f;
        }

        private void OnDisable()
        {
            ClearPath();
        }

        public bool CanMoveDirectly(Vector2 targetBodyCenter, bool includeAgents = true)
        {
            return _grid.CanMoveDirectly(_bodyCollider.bounds.center, targetBodyCenter, includeAgents);
        }
    }
}
