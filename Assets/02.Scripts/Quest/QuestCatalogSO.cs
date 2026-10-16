using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShotgunAndRun.Quest
{
    // 게임에 있는 퀘스트 정의 목록입니다. 세이브에 저장된 퀘스트 ID로 정의를 찾을 때 사용합니다.
    [CreateAssetMenu(fileName = "QuestCatalog", menuName = "ShotgunAndRun/Quest/Quest Catalog")]
    public sealed class QuestCatalogSO : ScriptableObject
    {
        [SerializeField] private List<QuestDefinitionSO> _quests = new();

        private readonly Dictionary<string, QuestDefinitionSO> _questsById =
            new Dictionary<string, QuestDefinitionSO>(StringComparer.Ordinal);

        private bool _lookupBuilt;

        public IReadOnlyList<QuestDefinitionSO> Quests => _quests;

        private void OnEnable()
        {
            _lookupBuilt = false;
        }

        private void OnValidate()
        {
            _lookupBuilt = false;
        }

        public bool TryGetById(string id, out QuestDefinitionSO quest)
        {
            if (!_lookupBuilt)
            {
                RebuildLookup(logErrors: true);
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                quest = null;
                return false;
            }

            return _questsById.TryGetValue(id, out quest);
        }

        // ID 중복 · 빈 항목과 각 퀘스트의 설정 오류를 검사합니다.
        [ContextMenu("Validate Catalog")]
        public bool ValidateCatalog()
        {
            bool isValid = RebuildLookup(logErrors: true);

            foreach (QuestDefinitionSO quest in _quests)
            {
                if (quest == null)
                {
                    continue;
                }

                foreach (string problem in quest.FindProblems())
                {
                    isValid = false;
                    Debug.LogError($"퀘스트 '{quest.name}': {problem}", quest);
                }
            }

            return isValid;
        }

        private bool RebuildLookup(bool logErrors)
        {
            _questsById.Clear();
            _lookupBuilt = true;
            bool isValid = true;

            for (int i = 0; i < _quests.Count; i++)
            {
                QuestDefinitionSO quest = _quests[i];
                if (quest == null)
                {
                    isValid = false;
                    if (logErrors)
                    {
                        Debug.LogError($"{name}의 퀘스트 목록 {i}번 항목이 비어 있습니다.", this);
                    }

                    continue;
                }

                if (string.IsNullOrWhiteSpace(quest.Id))
                {
                    isValid = false;
                    if (logErrors)
                    {
                        Debug.LogError($"퀘스트 '{quest.name}'의 ID가 비어 있습니다.", quest);
                    }

                    continue;
                }

                if (!_questsById.TryAdd(quest.Id, quest))
                {
                    isValid = false;
                    if (logErrors)
                    {
                        Debug.LogError($"퀘스트 ID가 중복되었습니다: '{quest.Id}'.", quest);
                    }
                }
            }

            return isValid;
        }
    }
}
