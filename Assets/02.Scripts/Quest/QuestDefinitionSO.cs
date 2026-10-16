using System.Collections.Generic;
using InventorySystem;
using UnityEngine;

namespace ShotgunAndRun.Quest
{
    // 퀘스트 1개의 정의입니다. 진행 상태는 QuestProgress에 두고 여기에는 쓰지 않습니다.
    // 완료 조건은 "퀘스트 아이템을 들고 탈출 + 런 중 플래그 달성"으로 모든 타입을 표현합니다 (game-spec 9.3).
    [CreateAssetMenu(fileName = "QuestDefinition", menuName = "ShotgunAndRun/Quest/Quest Definition")]
    public sealed class QuestDefinitionSO : ScriptableObject
    {
        [Header("Info")]
        [Tooltip("세이브 파일에 저장되는 고유 ID입니다. 변경하면 기존 세이브의 진행이 끊깁니다. (예: SQ_MAIN_001)")]
        [SerializeField] private string _id;
        [SerializeField] private string _title;
        [Tooltip("의뢰인 NPC 이름 (게시판 표시용)")]
        [SerializeField] private string _giver;
        [TextArea(2, 5)]
        [SerializeField] private string _description;

        [Header("Type")]
        [SerializeField] private QuestType _type;
        [Tooltip("관련 존 (Z1 ~ Z10)")]
        [SerializeField] private string _zoneA;
        [Tooltip("보물찾기만 사용하는 인접한 두 번째 존")]
        [SerializeField] private string _zoneB;
        [Tooltip("비워 두면 일반 스폰 규칙을 따릅니다. (예: E1)")]
        [SerializeField] private string _fixedSpawnId;

        [Header("Unlock")]
        [SerializeField] private List<QuestDefinitionSO> _prerequisiteQuests = new();
        [Tooltip("선행 퀘스트 중 완료해야 하는 개수입니다. 0이면 전부 완료해야 합니다.")]
        [SerializeField, Min(0)] private int _prerequisiteMinCount;
        [Tooltip("해금에 필요한 영구 플래그")]
        [SerializeField] private List<string> _requiredFlags = new();
        [Tooltip("해금 조건을 채운 뒤 관련 존(Zone A)을 방문하고 탈출해야 수주 가능 (구역 정화)")]
        [SerializeField] private bool _requiresZoneVisitAfterUnlock;

        [Header("Completion")]
        [Tooltip("들고 탈출해야 하는 퀘스트 아이템. 비워 두면 플래그만으로 완료합니다.")]
        [SerializeField] private ItemData _questItem;
        [SerializeField] private QuestItemSource _itemSource;
        [Tooltip("이번 런 안에 달성해야 하는 플래그 (예: H2_ELENA_FREED)")]
        [SerializeField] private List<string> _requiredRunFlags = new();
        [Tooltip("켜면 게시판 [퀘스트 완료] 버튼 대신 NPC 보고 컷씬(CutsceneContext.CompleteQuest)에서 완료합니다. (예: Q1 그룬 보고)")]
        [SerializeField] private bool _turnInByNpc;

        [Header("Rewards")]
        [SerializeField, Min(0)] private int _rewardXp;
        [Tooltip("탈출 후 창고로 바로 지급")]
        [SerializeField] private List<QuestRewardItem> _rewardItems = new();
        [Tooltip("완료 시 켜지는 영구 플래그 (예: RandomSpawnUnlocked)")]
        [SerializeField] private List<string> _unlockFlags = new();

        public string Id => _id;
        public string Title => _title;
        public string Giver => _giver;
        public string Description => _description;
        public QuestType Type => _type;
        public QuestSlot Slot => _type == QuestType.MainStory ? QuestSlot.Story : QuestSlot.Request;
        public string ZoneA => _zoneA;
        public string ZoneB => _zoneB;
        public string FixedSpawnId => _fixedSpawnId;
        public IReadOnlyList<QuestDefinitionSO> PrerequisiteQuests => _prerequisiteQuests;
        public int PrerequisiteMinCount => _prerequisiteMinCount;
        public IReadOnlyList<string> RequiredFlags => _requiredFlags;
        public bool RequiresZoneVisitAfterUnlock => _requiresZoneVisitAfterUnlock;
        public ItemData QuestItem => _questItem;
        public QuestItemSource ItemSource => _itemSource;
        public IReadOnlyList<string> RequiredRunFlags => _requiredRunFlags;
        public bool TurnInByNpc => _turnInByNpc;
        public int RewardXp => _rewardXp;
        public IReadOnlyList<QuestRewardItem> RewardItems => _rewardItems;
        public IReadOnlyList<string> UnlockFlags => _unlockFlags;

        // 설정 오류를 찾습니다. QuestCatalogSO의 Validate Catalog에서 함께 검사합니다.
        public List<string> FindProblems()
        {
            List<string> problems = new List<string>();

            if (string.IsNullOrWhiteSpace(_id))
            {
                problems.Add("ID가 비어 있습니다.");
            }

            if (_questItem != null && _questItem.ItemType != ItemType.Quest)
            {
                problems.Add($"퀘스트 아이템 '{_questItem.name}'의 ItemType이 Quest가 아닙니다.");
            }

            if (_questItem != null && _itemSource == QuestItemSource.None)
            {
                problems.Add("퀘스트 아이템이 있지만 등장 위치(Item Source)가 None입니다.");
            }

            if (_questItem == null && _itemSource != QuestItemSource.None)
            {
                problems.Add("등장 위치(Item Source)가 있지만 퀘스트 아이템이 비어 있습니다.");
            }

            if ((_itemSource != QuestItemSource.None || _requiresZoneVisitAfterUnlock) &&
                string.IsNullOrWhiteSpace(_zoneA))
            {
                problems.Add("Zone A가 비어 있습니다.");
            }

            if (_itemSource == QuestItemSource.Treasure && string.IsNullOrWhiteSpace(_zoneB))
            {
                problems.Add("보물찾기는 Zone B가 필요합니다.");
            }

            if (_questItem == null && _requiredRunFlags.Count == 0)
            {
                problems.Add("완료 조건(퀘스트 아이템 또는 런 플래그)이 없습니다.");
            }

            // 플래그 오타는 조용히 "없음"으로 판정되므로 QuestFlags에 등록된 이름인지 확인한다
            foreach (List<string> flags in new[] { _requiredFlags, _requiredRunFlags, _unlockFlags })
            {
                foreach (string flag in flags)
                {
                    if (!QuestFlags.IsKnown(flag))
                    {
                        problems.Add($"QuestFlags에 없는 플래그입니다: '{flag}'");
                    }
                }
            }

            if (_prerequisiteQuests.Contains(this))
            {
                problems.Add("자기 자신을 선행 퀘스트로 가지고 있습니다.");
            }

            if (_prerequisiteMinCount > _prerequisiteQuests.Count)
            {
                problems.Add("선행 퀘스트 완료 개수가 선행 퀘스트 수보다 많습니다.");
            }

            foreach (QuestRewardItem reward in _rewardItems)
            {
                if (!reward.IsValid)
                {
                    problems.Add("비어 있거나 수량이 0인 보상 아이템이 있습니다.");
                    break;
                }
            }

            return problems;
        }
    }
}
