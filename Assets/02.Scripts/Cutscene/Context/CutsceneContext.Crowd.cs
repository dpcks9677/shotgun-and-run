using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    // 무리 명령: 연출로만 움직이는 무리(CutsceneCrowd)를 보이고 · 걷게 합니다. 쓰려면 DeclareRequirements에서 r.Crowd("키")를 선언합니다.
    // 전투하는 적은 SpawnEnemy... 명령을 쓰세요.
    public sealed partial class CutsceneContext
    {
        private readonly Dictionary<string, CutsceneCrowd> _crowds = new Dictionary<string, CutsceneCrowd>();

        /// <summary>
        /// 키로 무리를 찾습니다. DeclareRequirements에 선언한 무리만 찾을 수 있습니다.
        /// </summary>
        public CutsceneCrowd Crowd(string key)
        {
            if (key == null || !_crowds.TryGetValue(key, out var crowd))
            {
                throw new InvalidOperationException($"'{key}' 무리는 DeclareRequirements에 선언되지 않았습니다.");
            }

            return crowd;
        }

        /// <summary>
        /// 무리를 보이거나 숨깁니다.
        /// </summary>
        public void SetVisible(CutsceneCrowd crowd, bool visible)
        {
            if (crowd == null) throw new ArgumentNullException(nameof(crowd));
            crowd.SetVisible(visible);
        }

        /// <summary>
        /// 무리 전원이 마커들을 차례로 거쳐 걷습니다. 기본(keepFormation)은 출발할 때의 대형을 유지한 채 무리 중심이 마커를 지나갑니다(복도를 채운 무리가 한 줄로 뭉치지 않음).
        /// false면 각자 마커 한 점으로 모입니다. startDelayStep초씩 순서대로 늦게 출발시킬 수도 있습니다.
        /// 핸들은 마지막 개체가 마지막 마커에 닿으면 끝납니다. 기다리지 않으면 무리는 컷씬이 진행되는 동안 계속 움직입니다.
        /// </summary>
        public CutsceneHandle CrowdMovePath(CutsceneCrowd crowd, float speed, float startDelayStep, params string[] markers)
        {
            return CrowdMovePath(crowd, speed, startDelayStep, true, markers);
        }

        /// <summary>
        /// CrowdMovePath의 대형 유지 여부를 고를 수 있는 판입니다.
        /// </summary>
        public CutsceneHandle CrowdMovePath(CutsceneCrowd crowd, float speed, float startDelayStep, bool keepFormation, params string[] markers)
        {
            if (crowd == null) throw new ArgumentNullException(nameof(crowd));
            if (speed <= 0f) throw new ArgumentOutOfRangeException(nameof(speed), "이동 속도는 0보다 커야 합니다.");

            var points = new Vector3[markers.Length];
            for (int i = 0; i < markers.Length; i++)
            {
                points[i] = MarkerPosition(markers[i]);
            }

            return Run(CrowdRoutine(crowd, points, speed, startDelayStep, keepFormation), $"crowd:{crowd.Key}");
        }

        /// <summary>
        /// 무리가 인물 바로 뒤를 따라 달립니다. 무리의 맨 앞 개체가 인물에게서 gap(유닛) 뒤에 붙도록 멀면 catchUpSpeed로 따라잡고, 붙으면 followSpeed로 간격을 유지합니다.
        /// 인물이 오른쪽으로 달리는 장면용입니다. 이전 무리 이동 명령은 취소되고, 핸들은 끝나지 않으며 컷씬이 끝날 때 멈춥니다.
        /// </summary>
        public CutsceneHandle CrowdFollow(CutsceneCrowd crowd, CutsceneActor actor, float gap, float followSpeed, float catchUpSpeed)
        {
            if (crowd == null) throw new ArgumentNullException(nameof(crowd));
            RequireActor(actor);

            return Run(FollowRoutine(crowd, actor, gap, followSpeed, catchUpSpeed), $"crowd:{crowd.Key}");
        }

        /// <summary>
        /// 무리에서 인물에게 가장 가까운 개체가 쓰러지는 애니메이션을 재생합니다. 쓰러진 개체는 그 자리에 남고, 따라오던 무리는 다음 개체가 앞으로 붙습니다.
        /// </summary>
        public void CrowdKill(CutsceneCrowd crowd, CutsceneActor nearestTo)
        {
            if (crowd == null) throw new ArgumentNullException(nameof(crowd));
            RequireActor(nearestTo);

            crowd.KillNearest(nearestTo.Position);
        }

        /// <summary>
        /// 무리에서 인물에게 가장 가까운, 아직 쓰러지지 않은 개체의 위치를 찾습니다. (CrowdKill이 쓰러뜨릴 개체) 모두 쓰러졌으면 false입니다.
        /// </summary>
        public bool CrowdNearest(CutsceneCrowd crowd, CutsceneActor nearestTo, out Vector3 position)
        {
            if (crowd == null) throw new ArgumentNullException(nameof(crowd));
            RequireActor(nearestTo);

            return crowd.TryGetNearestAlive(nearestTo.Position, out position);
        }

        private static IEnumerator FollowRoutine(CutsceneCrowd crowd, CutsceneActor actor, float gap, float followSpeed, float catchUpSpeed)
        {
            try
            {
                yield return crowd.Follow(() => actor.Position, gap, followSpeed, catchUpSpeed);
            }
            finally
            {
                crowd.StopAll();
            }
        }

        private static IEnumerator CrowdRoutine(CutsceneCrowd crowd, Vector3[] points, float speed, float startDelayStep, bool keepFormation)
        {
            try
            {
                yield return crowd.MovePath(points, speed, startDelayStep, keepFormation);
            }
            finally
            {
                crowd.StopAll();
            }
        }

        private void ResolveCrowds(List<string> errors)
        {
            if (_requirements.Crowds.Count == 0) return;

            var all = UnityEngine.Object.FindObjectsByType<CutsceneCrowd>(FindObjectsSortMode.None);
            foreach (string key in _requirements.Crowds)
            {
                var matches = all.Where(c => c.Key == key).ToArray();
                if (matches.Length == 0)
                {
                    errors.Add($"씬에 '{key}' 무리(CutsceneCrowd)가 없습니다.");
                }
                else if (matches.Length > 1)
                {
                    errors.Add($"'{key}' 무리가 씬에 {matches.Length}개 있습니다: {string.Join(", ", matches.Select(c => c.name))}");
                }
                else
                {
                    matches[0].Validate(errors);
                    _crowds[key] = matches[0];
                }
            }
        }
    }
}
