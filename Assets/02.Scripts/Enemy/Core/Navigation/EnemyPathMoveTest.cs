using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShotgunAndRun.Enemy
{
    public class EnemyPathMoveTest : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private float _arrivalDistance = 0.05f;
        [SerializeField] private float _waypointTimeout = 5f;

        private bool _isTesting;

        [ContextMenu("Test A Star Movement")]
        private void TestMovement()
        {
            if (!Application.isPlaying || _isTesting) return;

            if (_target == null)
            {
                Debug.LogWarning("Target을 연결해주세요.");
                return;
            }

            StartCoroutine(FollowPath());
        }

        private IEnumerator FollowPath()
        {
            EnemyMove move = GetComponent<EnemyMove>();
            EnemyStateMachine fsm = GetComponent<EnemyStateMachine>();
            global::Enemy enemy = GetComponent<global::Enemy>();

            if (!fsm.HasBegun)
            {
                Debug.LogWarning("Enemy 초기화 후 실행해주세요.");
                yield break;
            }

            if (enemy.EnemyCombat.IsAttacking || move.IsBeingKnockedBack)
            {
                Debug.LogWarning("공격·넉백이 끝난 뒤 실행해주세요.");
                yield break;
            }

            float originalSpeed = move.MoveSpeed;

            if (originalSpeed <= 0f)
            {
                Debug.LogWarning("이동 속도가 0입니다.");
                yield break;
            }

            _isTesting = true;
            bool wasFsmEnabled = fsm.enabled;
            fsm.enabled = false;

            try
            {
                move.Stop();

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
                    Debug.LogWarning("이동할 경로를 찾지 못했습니다.");
                    yield break;
                }

                // 시작 칸도 포함됩니다.
                // 현재 발밑 위치를 시작 칸 중심에 먼저 맞춥니다.
                foreach (Vector2Int cell in path)
                {
                    Vector2 targetCenter = grid.GetCellCenter(cell);
                    float elapsedTime = 0f;

                    while (true)
                    {
                        Vector2 bodyCenter = move.BodyCollider.bounds.center;
                        Vector2 direction = targetCenter - bodyCenter;
                        float distance = direction.magnitude;

                        if (distance <= _arrivalDistance) break;

                        if (elapsedTime >= _waypointTimeout)
                        {
                            Debug.LogWarning($"이동 중 막힘 또는 도착 실패: {cell}");
                            yield break;
                        }

                        // 물리 한 프레임에 남은 거리보다 멀리 가지 않도록 제한
                        float speed = Mathf.Min(
                            originalSpeed,
                            distance / Time.fixedDeltaTime);

                        move.SetMoveSpeed(speed);
                        move.Move(direction);

                        yield return new WaitForFixedUpdate();

                        elapsedTime += Time.fixedDeltaTime;
                    }

                    // 다음 칸으로 방향을 전환하기 전에 정지
                    move.Stop();
                }

                Debug.Log("A* 경로 이동 완료");
            }
            finally
            {
                move.Stop();
                move.SetMoveSpeed(originalSpeed);
                fsm.enabled = wasFsmEnabled;
                _isTesting = false;
            }
        }
    }
}
