using System.Collections;
using UnityEngine;

namespace ShotgunAndRun.Enemy
{
    public class EnemyGridMoveTest : MonoBehaviour
    {
        [SerializeField] private float _arrivalDistance = 0.05f;
        [SerializeField] private float _targetTimeout = 5f;

        private bool _isTesting;

        [ContextMenu("Test Grid Movement")]
        private void TestGridMovement()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Play 중에 실행해주세요.");
                return;
            }

            if (_isTesting) return;

            Debug.Log($"[{name}] 타일 중심 이동 테스트 시작");
            StartCoroutine(TestMovement());
        }

        private IEnumerator TestMovement()
        {
            EnemyMove move = GetComponent<EnemyMove>();
            EnemyStateMachine fsm = GetComponent<EnemyStateMachine>();

            if (!fsm.HasBegun)
            {
                Debug.LogWarning("Enemy 초기화가 끝난 뒤 실행해주세요.");
                yield break;
            }

            _isTesting = true;

            bool wasFsmEnabled = fsm.enabled;
            fsm.enabled = false;

            try
            {
                move.Stop();

                Vector2 bodySize = move.BodyCollider.bounds.size;

                EnemyNavigationGrid grid = new EnemyNavigationGrid(
                    GroundTilemapProvider.Ground, bodySize);

                // 루트가 아니라 발밑 Box 중심으로 시작 칸을 찾습니다.
                Vector2Int startCell = grid.WorldToCell(
                    move.BodyCollider.bounds.center);

                Vector2Int[] offsets =
                {
                    Vector2Int.zero,
                    Vector2Int.up,
                    Vector2Int.zero,
                    Vector2Int.down,
                    Vector2Int.zero,
                    Vector2Int.left,
                    Vector2Int.zero,
                    Vector2Int.right,
                    Vector2Int.zero
                };

                foreach (Vector2Int offset in offsets)
                {
                    Vector2Int targetCell = startCell + offset;

                    if (!grid.IsWalkable(targetCell))
                    {
                        Debug.LogWarning($"이동 불가능한 칸: {targetCell}");
                        yield break;
                    }

                    Vector2 targetCenter = grid.GetCellCenter(targetCell);
                    float elapsedTime = 0f;

                    while (true)
                    {
                        Vector2 bodyCenter = move.BodyCollider.bounds.center;
                        Vector2 direction = targetCenter - bodyCenter;

                        if (direction.magnitude <= _arrivalDistance) break;

                        if (elapsedTime >= _targetTimeout)
                        {
                            Debug.LogWarning($"타일 중심 도착 실패: {targetCell}");
                            yield break;
                        }

                        move.Move(direction);
                        elapsedTime += Time.deltaTime;

                        yield return null;
                    }

                    move.Stop();

                    Debug.Log($"타일 중심 도착: {targetCell}");
                    yield return new WaitForSeconds(0.5f);
                }
            }
            finally
            {
                // 정상 종료하거나 중간에 실패해도 원래 상태로 복구합니다.
                move.Stop();
                fsm.enabled = wasFsmEnabled;
                _isTesting = false;
            }
        }
    }
}
