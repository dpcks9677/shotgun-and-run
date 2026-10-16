using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EnemyComponent = global::Enemy;

namespace ShotgunAndRun.Cutscene
{
    // 적 명령: 소환 · 출발 · 정리. 쓰려면 DeclareRequirements에서 r.EnemySpawner()를 선언해야 하고, 씬에 CutsceneEnemySpawner가 있어야 합니다.
    public sealed partial class CutsceneContext
    {
        private CutsceneEnemySpawner _enemySpawner;

        /// <summary>
        /// 두 마커 사이에 일정한 간격으로 적을 count마리 세웁니다. (from과 to가 같으면 한 지점에 모읍니다)
        /// hold가 true면 행동을 시작하지 않고 세워 두며, 반환된 무리의 Release()로 출발시킵니다.
        /// </summary>
        public CutsceneEnemyGroup SpawnEnemyLine(EnemyType type, int count, string from, string to, bool hold = false,
            EnemyStateMode mode = EnemyStateMode.Destruction)
        {
            var spawner = RequireEnemySpawner();
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count), "소환할 적 수는 1 이상이어야 합니다.");

            Vector3 start = MarkerPosition(from);
            Vector3 end = MarkerPosition(to);
            var group = new CutsceneEnemyGroup();
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0f : (float)i / (count - 1);
                AddEnemy(group, spawner.Spawn(type, Vector3.Lerp(start, end, t), mode, hold), type);
            }

            return group;
        }

        /// <summary>
        /// 마커마다 적을 한 마리씩 소환합니다. hold가 true면 행동을 시작하지 않고 세워 둡니다.
        /// </summary>
        public CutsceneEnemyGroup SpawnEnemiesAt(EnemyType type, bool hold, EnemyStateMode mode, params string[] markers)
        {
            var spawner = RequireEnemySpawner();
            var group = new CutsceneEnemyGroup();
            foreach (string marker in markers)
            {
                AddEnemy(group, spawner.Spawn(type, MarkerPosition(marker), mode, hold), type);
            }

            return group;
        }

        /// <summary>
        /// 이름으로 등록된 적 무리를 찾습니다. (TutorialFlow 같은 진행 코드가 CutsceneEnemySpawner.Register로 등록)
        /// </summary>
        public CutsceneEnemyGroup EnemyGroup(string key)
        {
            var spawner = RequireEnemySpawner();
            if (!spawner.TryGetGroup(key, out var group))
            {
                throw new InvalidOperationException($"'{key}' 적 무리가 등록되어 있지 않습니다. 컷씬을 시작하기 전에 소환해 등록해야 합니다.");
            }

            return group;
        }

        /// <summary>
        /// 대기시켜 둔(hold) 적 무리를 걸어서 옮깁니다. AI는 시작하지 않고 걷기 애니메이션만 쓰며,
        /// 적들은 서로의 간격을 유지한 채 첫 번째 적이 마커에 닿는 위치까지 이동합니다. 핸들은 모두 도착하면 끝납니다.
        /// </summary>
        public CutsceneHandle MoveEnemies(CutsceneEnemyGroup group, string marker, float speed)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));
            if (speed <= 0f) throw new ArgumentOutOfRangeException(nameof(speed), "이동 속도는 0보다 커야 합니다.");

            Vector3 destination = MarkerPosition(marker);
            return Run(MoveEnemiesRoutine(group, destination, speed), $"enemies:{group.GetHashCode()}");
        }

        private static IEnumerator MoveEnemiesRoutine(CutsceneEnemyGroup group, Vector3 destination, float speed)
        {
            var enemies = new List<EnemyComponent>(group.Enemies);
            if (enemies.Count == 0 || enemies[0] == null) yield break;

            // 첫 번째 적을 기준으로 각자의 간격을 유지한다
            Vector3 anchor = enemies[0].transform.position;
            var targets = new List<Vector3>();
            foreach (var enemy in enemies)
            {
                targets.Add(enemy == null ? Vector3.zero : destination + (enemy.transform.position - anchor));
            }

            try
            {
                for (int i = 0; i < enemies.Count; i++)
                {
                    SetEnemyMoving(enemies[i], true, targets[i] - enemies[i].transform.position);
                }

                bool moving = true;
                while (moving)
                {
                    moving = false;
                    for (int i = 0; i < enemies.Count; i++)
                    {
                        if (enemies[i] == null) continue;

                        Vector3 target = targets[i];
                        target.z = enemies[i].transform.position.z;
                        var next = Vector3.MoveTowards(enemies[i].transform.position, target, speed * Time.deltaTime);
                        enemies[i].transform.position = next;

                        var body = enemies[i].GetComponent<Rigidbody2D>();
                        if (body != null) body.position = next;

                        if ((target - next).sqrMagnitude > 0.0001f) moving = true;
                        else SetEnemyMoving(enemies[i], false, Vector3.zero);
                    }

                    yield return null;
                }
            }
            finally
            {
                foreach (var enemy in enemies)
                {
                    SetEnemyMoving(enemy, false, Vector3.zero);
                }
            }
        }

        private static void SetEnemyMoving(EnemyComponent enemy, bool moving, Vector3 direction)
        {
            if (enemy == null || enemy.EnemyAnimation == null) return;

            enemy.EnemyAnimation.PlayMove(moving);
            if (moving)
            {
                enemy.EnemyAnimation.SetSpriteFlipImmediately(direction);
            }
        }

        /// <summary>
        /// 무리의 적이 모두 쓰러질 때까지 기다립니다.
        /// </summary>
        public CutsceneHandle WaitUntilCleared(CutsceneEnemyGroup group)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));
            return WaitUntil(() => group.IsCleared);
        }

        private CutsceneEnemySpawner RequireEnemySpawner()
        {
            if (_enemySpawner == null)
            {
                throw new InvalidOperationException("적 명령은 DeclareRequirements에서 r.EnemySpawner()를 선언해야 쓸 수 있습니다.");
            }

            return _enemySpawner;
        }

        private static void AddEnemy(CutsceneEnemyGroup group, EnemyComponent enemy, EnemyType type)
        {
            if (enemy == null)
            {
                throw new InvalidOperationException($"'{type}' 적을 소환하지 못했습니다. 콘솔의 CutsceneEnemySpawner 오류를 확인하세요.");
            }

            group.Add(enemy);
        }

        private void ResolveEnemySpawner(List<string> errors)
        {
            if (!_requirements.NeedsEnemySpawner) return;

            var spawners = UnityEngine.Object.FindObjectsByType<CutsceneEnemySpawner>(FindObjectsSortMode.None);
            if (spawners.Length == 0)
            {
                errors.Add("씬에 CutsceneEnemySpawner가 없습니다.");
                return;
            }

            if (spawners.Length > 1)
            {
                errors.Add($"CutsceneEnemySpawner가 씬에 {spawners.Length}개 있습니다. 하나만 두세요.");
                return;
            }

            spawners[0].Validate(errors);
            _enemySpawner = spawners[0];
        }
    }
}
