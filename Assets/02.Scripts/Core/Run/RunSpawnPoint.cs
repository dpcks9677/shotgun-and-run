using System.Collections.Generic;
using UnityEngine;

namespace ShotgunAndRun.Core
{
    // 스폰 포인트 (game-spec 4.1)
    // 쉘터에서 출격해 스테이지 씬이 열리면 RunPlayerBinder가 활성화된 스폰 포인트 중 1곳을 랜덤으로 골라 플레이어를 옮깁니다.
    // 스테이지 씬에서 바로 Play했거나 씬에 스폰 포인트가 없으면 플레이어는 씬에 배치된 위치에서 시작합니다.
    public sealed class RunSpawnPoint : MonoBehaviour
    {
        private static readonly List<RunSpawnPoint> ActivePoints = new List<RunSpawnPoint>();

        [Tooltip("레벨 기획 문서의 스폰 이름 (E1, E2, E3)")]
        [SerializeField] private string _spawnId;

        public string SpawnId => _spawnId;

        private void OnEnable()
        {
            ActivePoints.Add(this);
        }

        private void OnDisable()
        {
            ActivePoints.Remove(this);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 1f);
        }

        // 활성화된 스폰 포인트가 없으면 null을 반환합니다.
        public static RunSpawnPoint PickRandom()
        {
            if (ActivePoints.Count == 0)
            {
                return null;
            }

            return ActivePoints[Random.Range(0, ActivePoints.Count)];
        }

        // 스폰 ID가 같은 활성화된 스폰 포인트를 반환합니다. 없으면 null입니다.
        public static RunSpawnPoint FindById(string spawnId)
        {
            foreach (RunSpawnPoint point in ActivePoints)
            {
                if (point._spawnId == spawnId)
                {
                    return point;
                }
            }

            return null;
        }
    }
}
