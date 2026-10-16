using System.Collections;
using ShotgunAndRun.UI.Dialogue;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// (프로토타입) CS_TUTORIAL_CHEST_FIRST: 상자를 열지 않고 보라 방 입구에 닿으면 플레이어가 상자부터 열자고 말하며 뒤로 물러난다.
    /// </summary>
    [CreateAssetMenu(fileName = "CS_TUTORIAL_CHEST_FIRST", menuName = "ShotgunAndRun/Cutscene/Tutorial/Chest First")]
    public sealed class TutorialChestFirstCutscene : CutsceneAsset
    {
        [Header("대사")]
        [Tooltip("1줄: '상자부터 열어보자.' (화자: 플레이어)")]
        [SerializeField] private DialogueDataSO _line;

        [Tooltip("대사 뒤에 뒷걸음질로 물러나는 거리(u)")]
        [SerializeField] private float _stepBack = 1f;

        protected override string DefaultDebugSpawnMarker => "ArenaEntry";

        protected override void DeclareRequirements(CutsceneRequirements r)
        {
            r.Dialogue(_line, nameof(_line));
        }

        protected override IEnumerator Play(CutsceneContext c)
        {
            // 복도는 오른쪽으로 이어지므로 앞(오른쪽)을 본 채 왼쪽으로 뒷걸음질한다
            c.Face(c.Player, left: false);
            yield return c.Say(_line);
            yield return c.MoveBackwardToPosition(c.Player, c.Player.Position + Vector3.left * _stepBack);
        }
    }
}
