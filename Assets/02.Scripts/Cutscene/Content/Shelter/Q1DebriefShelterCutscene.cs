using System.Collections;
using ShotgunAndRun.UI.Dialogue;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// CS_Q1_DEBRIEF: 그룬의 보고. 대본: Docs/quests/CS_Q1_WHETSTONE.md 6장
    /// 붕괴 연출 뒤 그룬에게 말을 걸면 재생합니다. 숫돌을 받은 그룬이 무기 강화를 해금하고 로나에게 보냅니다. 대사 뒤 SQ_MAIN_001을 완료(보고) 처리합니다.
    /// 완료 플래그 FLAG_Q1_DEBRIEF_DONE이 강화 창(WeaponUpgradeUI)과 CS_Q2_BRIEFING의 조건입니다.
    /// 에셋 설정: Required FLAG_SHELTER_COLLAPSE_DONE / Blocked FLAG_Q1_DEBRIEF_DONE / Completion FLAG_Q1_DEBRIEF_DONE / AutoPlay 끄기
    /// 그룬의 CutsceneNpcDialogue에 연결합니다. 강화 창은 다음 상호작용부터 열립니다.
    /// </summary>
    [CreateAssetMenu(fileName = "CS_Q1_DEBRIEF", menuName = "ShotgunAndRun/Cutscene/Shelter/Q1 Debrief")]
    public sealed class Q1DebriefShelterCutscene : CutsceneAsset
    {
        private const string QuestId = "SQ_MAIN_001";

        [Header("등장인물")]
        [SerializeField] private CharacterProfileSO _grun;

        [Header("대사")]
        [Tooltip("대본 01~03: 숫돌 감사 · 강화 해금 · 로나 안내. 말풍선은 대사 데이터에 있음")]
        [SerializeField] private DialogueDataSO _debrief;

        protected override void DeclareRequirements(CutsceneRequirements r)
        {
            r.Actor(_grun, nameof(_grun));
            r.Dialogue(_debrief, nameof(_debrief));
        }

        protected override IEnumerator Play(CutsceneContext c)
        {
            var grun = c.Actor(_grun);

            c.FaceEachOther(c.Player, grun);
            yield return c.Say(_debrief);

            c.CompleteQuest(QuestId);
        }
    }
}
