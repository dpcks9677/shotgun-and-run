using System.Collections.Generic;
using UnityEngine;

namespace ShotgunAndRun.Enemy
{
    public class EnemyPathGizmoTest : MonoBehaviour
    {
        [SerializeField] private Transform _target;

        private readonly List<Vector2> _pathPoints = new List<Vector2>();

        [ContextMenu("Test A Star Path")]
        private void TestPath()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Play 중에 실행해주세요.");
                return;
            }

            _pathPoints.Clear();

            if (_target == null)
            {
                Debug.LogWarning("Target에 목적지 오브젝트를 연결해주세요.");
                return;
            }

            EnemyMove move = GetComponent<EnemyMove>();
            EnemyStateMachine fsm = GetComponent<EnemyStateMachine>();

            if (!fsm.HasBegun)
            {
                Debug.LogWarning("Enemy 초기화가 끝난 뒤 실행해주세요.");
                return;
            }

            EnemyNavigationGrid grid = new EnemyNavigationGrid(
                GroundTilemapProvider.Ground,
                move.BodyCollider.bounds.size);

            EnemyPathfinder pathfinder = new EnemyPathfinder(grid);

            Vector2Int startCell = grid.WorldToCell(
                move.BodyCollider.bounds.center);

            Vector2Int targetCell = grid.WorldToCell(_target.position);

            bool isSuccess = pathfinder.TryFindPath(
                startCell, targetCell, out List<Vector2Int> path);

            if (!isSuccess)
            {
                Debug.LogWarning(
                    $"경로 탐색 실패: {startCell} → {targetCell}");
                return;
            }

            foreach (Vector2Int cell in path)
            {
                _pathPoints.Add(grid.GetCellCenter(cell));
            }

            Debug.Log($"경로 탐색 성공: {_pathPoints.Count}개 칸");
        }

        private void OnDrawGizmosSelected()
        {
            if (_pathPoints.Count == 0) return;

            Gizmos.color = Color.yellow;

            for (int i = 0; i < _pathPoints.Count; i++)
            {
                Vector2 point = _pathPoints[i];

                Gizmos.DrawWireCube(
                    point, new Vector3(0.15f, 0.15f, 0f));

                if (i > 0)
                {
                    Gizmos.DrawLine(_pathPoints[i - 1], point);
                }
            }

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_pathPoints[0], 0.1f);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(
                _pathPoints[_pathPoints.Count - 1], 0.1f);
        }
    }
}
