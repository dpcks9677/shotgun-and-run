using System.Collections;
using ShotgunAndRun.UI.Dialogue;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// CS_Q2_BRIEFING: 로나의 의뢰. 대본: Docs/quests/CS_Q2_MAGE_RESCUE.md 4장
    /// 그룬의 보고(CS_Q1_DEBRIEF) 뒤 로나에게 말을 걸면, 로나가 플레이어와 마주 서서 엘레나 구출을 의뢰하고, 페이드 아웃 뒤 SQ_MAIN_002를 수주합니다.
    /// 에셋 설정: Required FLAG_Q1_DEBRIEF_DONE / Blocked FLAG_Q2_BRIEFING_DONE /
    /// Completion FLAG_Q2_BRIEFING_DONE, FLAG_Q2_ACCEPTED / AutoPlay 끄기 (완료 플래그 즉시 저장은 쓰지 않음)
    /// 로나의 CutsceneNpcDialogue에 연결합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "CS_Q2_BRIEFING", menuName = "ShotgunAndRun/Cutscene/Shelter/Q2 Briefing")]
    public sealed class Q2BriefingShelterCutscene : CutsceneAsset
    {
        private const string QuestId = "SQ_MAIN_002";

        [Header("등장인물")]
        [SerializeField] private CharacterProfileSO _lona;

        [Header("대사")]
        [Tooltip("1~4비트: 대본 01~06 (로나 · 플레이어 · 로나 · 로나 · 로나 · 로나). 줄 수는 자유. 말풍선은 대사 데이터에 있음")]
        [SerializeField] private DialogueDataSO _brief;

        [Header("연출 수치")]
        [SerializeField, Min(0f)] private float _fadeSeconds = 1f;

        protected override void DeclareRequirements(CutsceneRequirements r)
        {
            r.Actor(_lona, nameof(_lona));
            r.Dialogue(_brief, nameof(_brief));
        }

        protected override IEnumerator Play(CutsceneContext c)
        {
            var lona = c.Actor(_lona);

            // 1비트: 말을 건 자리에서 로나와 마주 선다
            c.FaceEachOther(c.Player, lona);

            // 2~4비트: 엘레나 소개 · 진입 경로 · 당부. 카메라는 움직이지 않는다 (대사별 말풍선은 대사 데이터에 있음)
            yield return c.Say(_brief);

            // 마무리: 페이드 아웃 뒤 SQ_MAIN_002 수주, 카메라를 플레이어에게 돌린 뒤 페이드 인
            yield return c.FadeOut(_fadeSeconds);
            c.AcceptQuest(QuestId);
            yield return c.CameraToPlayer(0f);
            yield return c.FadeIn(_fadeSeconds);
        }
    }
}
