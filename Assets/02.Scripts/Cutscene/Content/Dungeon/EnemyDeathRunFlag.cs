using ShotgunAndRun.Core;
using ShotgunAndRun.Quest;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 같은 오브젝트의 스포너가 만든 적 중 지정 타입이 죽으면 런 플래그를 올립니다. (퀘스트 SQ_ELITE_Z5: Z5 엘리트 스나이퍼 처치)
    /// 수비대 적은 씬 로드 때 스포너가 생성하므로, 적이 아니라 Z5 수비대 스포너에 붙입니다.
    /// 플래그는 퀘스트 수주와 관계없이 올리고, 완료 판정은 탈출 때 수주한 퀘스트만 받습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyDeathRunFlag : MonoBehaviour
    {
        [SerializeField] private EnemyType _enemyType = EnemyType.EliteSniper;
        [SerializeField] private string _runFlag = QuestFlags.Z5EliteSniperKilled;

        private EnemySpawner _spawner;

        private void Awake()
        {
            _spawner = GetComponent<EnemySpawner>();
            if (_spawner == null)
            {
                Debug.LogError("[EnemyDeathRunFlag] 같은 오브젝트에 EnemySpawner가 없습니다.", this);
            }
        }

        // 수비대 스포너는 Start에서 적을 만들므로 OnEnable 구독이 먼저 끝납니다.
        private void OnEnable()
        {
            if (_spawner != null) _spawner.EnemySpawned += HandleEnemySpawned;
        }

        private void OnDisable()
        {
            if (_spawner != null) _spawner.EnemySpawned -= HandleEnemySpawned;
        }

        private void HandleEnemySpawned(global::Enemy enemy)
        {
            if (enemy == null || enemy.EnemyType != _enemyType || enemy.EnemyHealth == null) return;

            enemy.EnemyHealth.DeathEvent += RaiseFlag;
        }

        private void RaiseFlag()
        {
            GameManager.Instance?.Quests?.RaiseRunFlag(_runFlag);
        }
    }
}
