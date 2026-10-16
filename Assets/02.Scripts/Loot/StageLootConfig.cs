using UnityEngine;

namespace ShotgunAndRun.Loot
{
    // 스테이지 하나의 상자 생성 규칙 에셋. 수치는 game-spec 7.3 기준입니다.
    [CreateAssetMenu(fileName = "StageLootConfig", menuName = "ShotgunAndRun/Loot/Stage Loot Config")]
    public sealed class StageLootConfig : ScriptableObject
    {
        [SerializeField] private StageLootRules _rules = new StageLootRules();

        public StageLootRules Rules => _rules;

        [ContextMenu("스테이지 1 기본값으로 채우기")]
        private void FillStage1Defaults()
        {
            _rules = StageLootDefaults.CreateStage1();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        private void Reset()
        {
            _rules = StageLootDefaults.CreateStage1();
        }
    }
}
