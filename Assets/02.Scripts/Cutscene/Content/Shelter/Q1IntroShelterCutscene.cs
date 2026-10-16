using System.Collections;
using ShotgunAndRun.UI.Dialogue;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// CS_Q1_INTRO: 로나의 안내. 대본: Docs/quests/CS_Q1_WHETSTONE.md 3장
    /// 프롤로그 뒤 로나에게 말을 걸면 재생합니다. 던전 제한 시간을 일러 주고, 카메라가 대장간의 그룬을 비추는 동안 그룬에게 보냅니다.
    /// 에셋 설정: Required FLAG_PROLOGUE_COMPLETED / Blocked FLAG_Q1_INTRO_DONE / Completion FLAG_Q1_INTRO_DONE / AutoPlay 끄기
    /// 로나의 CutsceneNpcDialogue에 연결합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "CS_Q1_INTRO", menuName = "ShotgunAndRun/Cutscene/Shelter/Q1 Intro")]
    public sealed class Q1IntroShelterCutscene : CutsceneAsset
    {
        // 씬에 같은 키의 CutsceneMarker가 있어야 한다
        private const string GrunCamera = "GrunCamera";

        [Header("등장인물")]
        [SerializeField] private CharacterProfileSO _lona;

        [Header("대사")]
        [Tooltip("대본 01~02: 로나의 인사와 던전 제한 시간 조언")]
        [SerializeField] private DialogueDataSO _advice;

        [Tooltip("조언 끝의 선택지(\"제가 해볼게요.\")를 고른 뒤 로나의 대답")]
        [SerializeField] private DialogueDataSO _reply;

        [Tooltip("대본 03: 카메라가 그룬을 비추는 동안 로나가 그룬에게 보낸다")]
        [SerializeField] private DialogueDataSO _toGrun;

        [Header("연출 수치")]
        [SerializeField, Min(0f)] private float _grunCameraBlend = 0.8f;

        protected override void DeclareRequirements(CutsceneRequirements r)
        {
            r.Actor(_lona, nameof(_lona));
            r.Dialogue(_advice, nameof(_advice));
            r.Dialogue(_reply, nameof(_reply));
            r.Dialogue(_toGrun, nameof(_toGrun));
            r.Marker(GrunCamera);
        }

        protected override IEnumerator Play(CutsceneContext c)
        {
            var lona = c.Actor(_lona);

            // 1비트: 로나와 마주 서서 조언을 듣고 선택지를 고르면 로나가 대답한다
            c.FaceEachOther(c.Player, lona);
            yield return c.Say(_advice);
            yield return c.Say(_reply);

            // 2비트: 카메라가 대장간의 그룬을 비추고, 로나가 그룬에게 보낸다. 카메라 복귀는 Director가 한다
            c.CameraTo(GrunCamera, _grunCameraBlend);
            yield return c.Say(_toGrun);
        }
    }
}
