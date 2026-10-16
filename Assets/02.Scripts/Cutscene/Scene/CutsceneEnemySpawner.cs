using System.Collections.Generic;
using UnityEngine;
using EnemyComponent = global::Enemy;
using PlayerComponent = ShotgunAndRun.Player.Player;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 컷씬 · 튜토리얼이 적을 소환할 때 쓰는 씬 오브젝트입니다. 씬에 하나만 둡니다.
    /// 기존 EnemySpawner의 소환 절차(데이터 초기화 → 목표 연결 → FSM 시작)를 그대로 따르되, 웨이브 · 청크 규칙 없이 한 마리씩 소환합니다.
    /// hold로 소환하면 FSM을 시작하지 않고 제자리에 세워 두며, CutsceneEnemyGroup.Release로 출발시킵니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CutsceneEnemySpawner : MonoBehaviour
    {
        [Tooltip("적 종류별 데이터 (Assets/07.Datas/EnemyDataTableSO)")]
        [SerializeField] private EnemyDataTableSO _enemyDataTable;

        private readonly Dictionary<string, CutsceneEnemyGroup> _groups = new Dictionary<string, CutsceneEnemyGroup>();

        /// <summary>
        /// 소환한 무리를 이름으로 등록합니다. 컷씬이 EnemyGroup("이름")으로 같은 무리를 찾아 움직일 수 있습니다.
        /// </summary>
        public void Register(string key, CutsceneEnemyGroup group)
        {
            _groups[key] = group;
        }

        /// <summary>
        /// 등록해 둔 무리를 이름으로 지웁니다. 무리의 적은 그대로 두므로 필요하면 Clear를 먼저 부르세요.
        /// </summary>
        public void Unregister(string key)
        {
            _groups.Remove(key);
        }

        public bool TryGetGroup(string key, out CutsceneEnemyGroup group)
        {
            return _groups.TryGetValue(key, out group);
        }

        /// <summary>
        /// 사전 검사. 문제가 있으면 errors에 추가합니다.
        /// </summary>
        internal void Validate(List<string> errors)
        {
            if (_enemyDataTable == null)
            {
                errors.Add($"'{name}'의 CutsceneEnemySpawner에 Enemy Data Table이 비어 있습니다.");
            }
        }

        /// <summary>
        /// 적 한 마리를 소환합니다. 실패하면 null입니다.
        /// mode: Destruction = 소환 즉시 플레이어를 곧장 추격 / Standard = 정찰하다가 플레이어를 발견하면 추격
        /// </summary>
        public EnemyComponent Spawn(EnemyType type, Vector2 position, EnemyStateMode mode = EnemyStateMode.Destruction, bool hold = false)
        {
            var player = PlayerComponent.PlayerInstance;
            if (player == null)
            {
                Debug.LogError("[CutsceneEnemySpawner] 씬에 플레이어가 없어 적을 소환할 수 없습니다.", this);
                return null;
            }

            EnemyData data = _enemyDataTable != null ? _enemyDataTable.ReadEnemyData(type) : null;
            if (data == null || data.prefab == null)
            {
                Debug.LogError($"[CutsceneEnemySpawner] '{type}' 적 데이터 또는 프리팹이 없습니다.", this);
                return null;
            }

            EnemyComponent enemy = global::EnemyLayer.Acquire(data.prefab, position);
            enemy.Initialize(data);
            enemy.SpawnPosition = position;
            enemy.EnemySensor.SetTargetTransform(player.transform);

            var machine = enemy.GetComponent<EnemyStateMachine>();
            machine.SetMode(mode);
            if (!hold)
            {
                machine.Begin();
            }

            return enemy;
        }
    }
}
