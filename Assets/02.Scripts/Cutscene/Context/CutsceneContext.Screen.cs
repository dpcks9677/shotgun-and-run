using System.Collections;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    // 화면 명령: 페이드 · 점멸 · 레터박스. 컷씬이 끝나면 모두 원래대로 돌아갑니다.
    public sealed partial class CutsceneContext
    {
        private const string ScreenOwner = "screen";
        private const string LetterboxOwner = "letterbox";

        /// <summary>
        /// 화면을 color(기본 검정)로 seconds초 동안 덮습니다.
        /// </summary>
        public CutsceneHandle FadeOut(float seconds, Color? color = null)
        {
            var screen = _director.Screen;
            return Run(FadeRoutine(color ?? Color.black, screen.OverlayAlpha, 1f, seconds), ScreenOwner);
        }

        /// <summary>
        /// 덮인 화면을 seconds초 동안 걷어냅니다.
        /// </summary>
        public CutsceneHandle FadeIn(float seconds)
        {
            var screen = _director.Screen;
            return Run(FadeRoutine(screen.OverlayColor, screen.OverlayAlpha, 0f, seconds), ScreenOwner);
        }

        /// <summary>
        /// 화면을 color로 번쩍인 뒤 seconds초 동안 사라지게 합니다. (예: 각성 연출의 붉은 점멸)
        /// </summary>
        public CutsceneHandle Flash(Color color, float seconds)
        {
            return Run(FadeRoutine(color, color.a, 0f, seconds), ScreenOwner);
        }

        /// <summary>
        /// 위아래 레터박스(검은 띠)를 seconds초 동안 내리거나 올립니다.
        /// </summary>
        public CutsceneHandle Letterbox(bool show, float seconds = 0.4f)
        {
            return Run(LetterboxRoutine(show ? 1f : 0f, seconds), LetterboxOwner);
        }

        private IEnumerator FadeRoutine(Color color, float from, float to, float seconds)
        {
            var screen = _director.Screen;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                screen.SetOverlay(color, Mathf.Lerp(from, to, elapsed / seconds));
                yield return null;
                elapsed += Time.deltaTime;
            }
            screen.SetOverlay(color, to);
        }

        private IEnumerator LetterboxRoutine(float to, float seconds)
        {
            var screen = _director.Screen;
            float from = screen.LetterboxAmount;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                screen.SetLetterbox(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / seconds)));
                yield return null;
                elapsed += Time.deltaTime;
            }
            screen.SetLetterbox(to);
        }
    }
}
