using System.Collections;
using ShotgunAndRun.UI.Dialogue;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// (프로토타입) CS_TUTORIAL_ARENA_INTRO: 복도 출구에서 보라 방의 적 무리를 보여 준다.
    /// 위쪽(일반 3 · 돌진 2)을 비추고 대사, 아래쪽(일반 2 · 돌진 1)을 비추고 대사. 이후 전투가 시작된다. (적 배치는 TutorialFlow)
    /// </summary>
    [CreateAssetMenu(fileName = "CS_TUTORIAL_ARENA_INTRO", menuName = "ShotgunAndRun/Cutscene/Tutorial/Arena Intro")]
    public sealed class TutorialArenaIntroCutscene : CutsceneAsset
    {
        private const string UpperFocus = "ArenaUpperFocus";
        private const string LowerFocus = "ArenaLowerFocus";

        [Header("대사")]
        [Tooltip("1줄: '또 다른 고블린이잖아?' (위쪽 무리를 비춘 뒤)")]
        [SerializeField] private DialogueDataSO _upperLine;

        [Tooltip("2줄: '생각보다 무리가 큰데...' · '이 무기라면 어떻게든 해볼 수 있을 것 같아.' (아래쪽 무리를 비춘 뒤)")]
        [SerializeField] private DialogueDataSO _lowerLines;

        [Header("효과음 (임시, 비워 두면 소리 없이 진행)")]
        [Tooltip("컷씬이 끝나 적이 풀리기 직전의 포효 · 경보음")]
        [SerializeField] private AudioClip _alertSfx;

        [Header("연출 수치")]
        [SerializeField, Min(0f)] private float _focusBlend = 1.2f;
        [SerializeField, Min(0f)] private float _focusHold = 0.7f;
        [SerializeField, Min(0f)] private float _returnBlend = 0.8f;

        protected override string DefaultDebugSpawnMarker => "ArenaEntry";

        protected override void DeclareRequirements(CutsceneRequirements r)
        {
            r.Dialogue(_upperLine, nameof(_upperLine));
            r.Dialogue(_lowerLines, nameof(_lowerLines));
            r.Marker(UpperFocus, LowerFocus);
        }

        protected override IEnumerator Play(CutsceneContext c)
        {
            c.FaceTo(c.Player, UpperFocus);
            yield return c.CameraTo(UpperFocus, _focusBlend);
            yield return c.Wait(_focusHold);
            yield return c.Say(_upperLine);

            c.FaceTo(c.Player, LowerFocus);
            yield return c.CameraTo(LowerFocus, _focusBlend);
            yield return c.Wait(_focusHold);
            yield return c.Say(_lowerLines);

            yield return c.CameraToPlayer(_returnBlend);

            // 이 컷씬이 끝나면 적이 풀린다
            if (_alertSfx != null)
            {
                c.PlaySfx(_alertSfx);
            }

            c.CameraShake(0.08f, 0.4f);
        }
    }
}
