using System.Collections.Generic;
using UnityEngine;
using EnemyComponent = global::Enemy;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 컷씬이나 튜토리얼이 한꺼번에 소환한 적 무리입니다. 남은 수 확인 · 출발시키기 · 모두 없애기를 맡습니다.
    /// </summary>
    public sealed class CutsceneEnemyGroup
    {
        private readonly List<EnemyComponent> _enemies = new List<EnemyComponent>();

        public IReadOnlyList<EnemyComponent> Enemies => _enemies;

        public int Count => _enemies.Count;

        /// <summary>
        /// 아직 쓰러지지 않은 적의 수
        /// </summary>
        public int AliveCount
        {
            get
            {
                int alive = 0;
                foreach (var enemy in _enemies)
                {
                    if (IsAlive(enemy)) alive++;
                }

                return alive;
            }
        }

        public bool IsCleared => AliveCount == 0;

        /// <summary>
        /// 아직 쓰러지지 않은 적 중 from에서 가장 가까운 적까지의 거리. 산 적이 없으면 무한대입니다.
        /// </summary>
        public float NearestAliveDistance(Vector2 from)
        {
            float nearest = float.PositiveInfinity;
            foreach (var enemy in _enemies)
            {
                if (!IsAlive(enemy)) continue;

                float distance = Vector2.Distance(from, enemy.transform.position);
                if (distance < nearest) nearest = distance;
            }

            return nearest;
        }

        public void Add(EnemyComponent enemy)
        {
            _enemies.Add(enemy);
        }

        /// <summary>
        /// 대기시켜 둔(hold) 적들의 행동을 시작합니다. 이미 시작했다면 아무 일도 없습니다.
        /// </summary>
        public void Release()
        {
            foreach (var enemy in _enemies)
            {
                if (!IsAlive(enemy)) continue;

                var machine = enemy.GetComponent<EnemyStateMachine>();
                if (machine != null)
                {
                    machine.Begin();
                }
            }
        }

        /// <summary>
        /// 남은 적을 쓰러지는 연출 없이 바로 없앱니다. (암전 중 정리 등)
        /// </summary>
        public void Clear()
        {
            foreach (var enemy in _enemies)
            {
                if (enemy != null)
                {
                    enemy.Die();
                }
            }

            _enemies.Clear();
        }

        private static bool IsAlive(EnemyComponent enemy)
        {
            // 죽은 적은 풀로 반환되며 체력이 되살려지므로(IsDead가 false로 돌아옴) 꺼진 적도 쓰러진 것으로 본다
            return enemy != null && enemy.gameObject.activeInHierarchy && (enemy.EnemyHealth == null || !enemy.EnemyHealth.IsDead);
        }
    }
}
