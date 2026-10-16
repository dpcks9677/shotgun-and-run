using System;
using System.Collections;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    // 흐름 명령: 대기 · 여러 명령 모두 기다리기 · 조건 기다리기
    public sealed partial class CutsceneContext
    {
        /// <summary>
        /// seconds초 기다립니다. (게임 시간 기준)
        /// </summary>
        public CutsceneHandle Wait(float seconds)
        {
            return Run(WaitRoutine(seconds));
        }

        /// <summary>
        /// 넘긴 명령이 모두 끝날 때까지 기다립니다.
        /// </summary>
        public CutsceneHandle WaitAll(params CutsceneHandle[] handles)
        {
            return Run(WaitUntilRoutine(() => Array.TrueForAll(handles, h => h == null || h.IsDone)));
        }

        /// <summary>
        /// condition이 true가 될 때까지 기다립니다.
        /// </summary>
        public CutsceneHandle WaitUntil(Func<bool> condition)
        {
            return Run(WaitUntilRoutine(condition));
        }

        private static IEnumerator WaitRoutine(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
        }

        private static IEnumerator WaitUntilRoutine(Func<bool> condition)
        {
            while (!condition())
            {
                yield return null;
            }
        }
    }
}
