using System.Collections;
using ShotgunAndRun.UI.Dialogue;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// CS_Q1_REQUEST: 그룬의 의뢰. 대본: Docs/quests/CS_Q1_WHETSTONE.md 4장
    /// 로나의 안내 뒤 그룬에게 말을 걸면 재생합니다. 그룬이 샷건을 감정하고 정밀 숫돌을 부탁한 뒤 SQ_MAIN_001을 수주합니다.
    /// 에셋 설정: Required FLAG_Q1_INTRO_DONE / Blocked FLAG_Q1_ACCEPTED / Completion FLAG_Q1_ACCEPTED / AutoPlay 끄기
    /// 그룬의 CutsceneNpcDialogue에 연결합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "CS_Q1_REQUEST", menuName = "ShotgunAndRun/Cutscene/Shelter/Q1 Request")]
    public sealed class Q1RequestShelterCutscene : CutsceneAsset
    {
        private const string QuestId = "SQ_MAIN_001";

        [Header("등장인물")]
        [SerializeField] private CharacterProfileSO _grun;

        [Header("대사")]
        [Tooltip("대본 01~05: 샷건 감정과 정밀 숫돌 의뢰. 말풍선은 대사 데이터에 있음")]
        [SerializeField] private DialogueDataSO _request;

        protected override void DeclareRequirements(CutsceneRequirements r)
        {
            r.Actor(_grun, nameof(_grun));
            r.Dialogue(_request, nameof(_request));
        }

        protected override IEnumerator Play(CutsceneContext c)
        {
            var grun = c.Actor(_grun);

            c.FaceEachOther(c.Player, grun);
            yield return c.Say(_request);

            c.AcceptQuest(QuestId);
        }
    }
}
