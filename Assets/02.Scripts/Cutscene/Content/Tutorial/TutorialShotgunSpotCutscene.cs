using System.Collections;
using ShotgunAndRun.UI.Dialogue;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// (프로토타입) CS_TUTORIAL_SHOTGUN_SPOT: 초록 구간 진입. 카메라가 샷건에 한 번 포커스했다가 플레이어로 돌아오고 "저게 뭐지?"를 말한다.
    /// 대본: Docs/quests/CS_TUTORIAL.md 3-3. 끝나면 플레이어가 샷건에 다가가 E키로 줍는 단계로 넘어간다 (TutorialFlow).
    /// </summary>
    [CreateAssetMenu(fileName = "CS_TUTORIAL_SHOTGUN_SPOT", menuName = "ShotgunAndRun/Cutscene/Tutorial/Shotgun Spot")]
    public sealed class TutorialShotgunSpotCutscene : CutsceneAsset
    {
        private const string GunFocus = "GunFocus";

        [Header("대사")]
        [Tooltip("1줄: '저게 뭐지?' (화자: 플레이어)")]
        [SerializeField] private DialogueDataSO _line;

        [Header("연출 수치")]
        [SerializeField, Min(0f)] private float _focusBlend = 1f;
        [SerializeField, Min(0f)] private float _focusHold = 0.8f;
        [SerializeField, Min(0f)] private float _returnBlend = 0.8f;

        protected override string DefaultDebugSpawnMarker => "OctagonEntry";

        protected override void DeclareRequirements(CutsceneRequirements r)
        {
            r.Dialogue(_line, nameof(_line));
            r.Marker(GunFocus);
        }

        protected override IEnumerator Play(CutsceneContext c)
        {
            // 카메라가 샷건을 비추는 동안 플레이어도 그쪽을 본다
            c.FaceTo(c.Player, GunFocus);
            yield return c.CameraTo(GunFocus, _focusBlend);
            yield return c.Wait(_focusHold);
            yield return c.CameraToPlayer(_returnBlend);
            yield return c.Say(_line);
        }
    }
}
