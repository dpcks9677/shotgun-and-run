using System.Collections;
using ShotgunAndRun.UI.Dialogue;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// (프로토타입) CS_TUTORIAL_CHEST_SPOT: 남색 복도에서 상자가 보이는 위치에 닿으면 플레이어가 상자를 발견했다고 말한다.
    /// </summary>
    [CreateAssetMenu(fileName = "CS_TUTORIAL_CHEST_SPOT", menuName = "ShotgunAndRun/Cutscene/Tutorial/Chest Spot")]
    public sealed class TutorialChestSpotCutscene : CutsceneAsset
    {
        [Header("대사")]
        [Tooltip("2줄: '상자다!' · '쓸만한 게 있을지도 모르니 한 번 뒤져봐야겠어.' (화자: 플레이어)")]
        [SerializeField] private DialogueDataSO _lines;

        protected override string DefaultDebugSpawnMarker => "ChestSight";

        protected override void DeclareRequirements(CutsceneRequirements r)
        {
            r.Dialogue(_lines, nameof(_lines));
        }

        protected override IEnumerator Play(CutsceneContext c)
        {
            // 상자는 복도 위쪽 벽에 있고 복도는 오른쪽으로 이어진다
            c.Face(c.Player, left: false);
            yield return c.Say(_lines);
        }
    }
}
