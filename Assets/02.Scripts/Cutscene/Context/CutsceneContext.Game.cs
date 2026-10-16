using System;
using ShotgunAndRun.Core;
using ShotgunAndRun.Quest;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    // 게임 명령: 씬 신호 · 영구 플래그. 완료 플래그는 Director가 자동으로 추가하므로 여기서 넣지 않습니다.
    public sealed partial class CutsceneContext
    {
        /// <summary>
        /// 씬의 CutsceneSignal 중 key가 같은 것을 모두 실행합니다. DeclareRequirements에 선언한 키만 쓸 수 있습니다.
        /// </summary>
        public void Signal(string key)
        {
            if (!_signals.TryGetValue(key, out var receivers))
            {
                throw new InvalidOperationException($"'{key}' 신호는 DeclareRequirements에 선언되지 않았습니다.");
            }

            foreach (var receiver in receivers)
            {
                receiver.Raise();
            }
        }

        /// <summary>
        /// 플래그를 추가합니다. (컷씬 도중 분기 결과 저장 등)
        /// 런 중이면 보류했다가 탈출할 때 영구 플래그로 저장하고, 사망하면 버립니다.
        /// </summary>
        public void AddFlag(string flag)
        {
            var quests = GameManager.Instance != null ? GameManager.Instance.Progress?.Quests : null;
            if (quests == null)
            {
                Debug.LogWarning($"[Cutscene] 진행 데이터(GameManager)가 없어 '{flag}' 플래그를 추가하지 못했습니다.");
                return;
            }

            _director.AddFlag(flag);
        }

        /// <summary>
        /// 영구 플래그 또는 이번 런에서 보류 중인 플래그를 가지고 있는지 확인합니다.
        /// </summary>
        public bool HasFlag(string flag)
        {
            var quests = GameManager.Instance != null ? GameManager.Instance.Progress?.Quests : null;
            return quests != null && (quests.HasFlag(flag) || _director.HasPendingFlag(flag));
        }

        /// <summary>
        /// 퀘스트를 수주합니다. 퀘스트 ID는 QuestCatalog에 있어야 하고, 수주 조건(해금 플래그 · 슬롯)을 만족해야 합니다.
        /// 실패하면 경고만 남기고 컷씬은 계속 진행합니다.
        /// </summary>
        public void AcceptQuest(string questId)
        {
            var quests = GameManager.Instance != null ? GameManager.Instance.Quests : null;
            if (quests == null)
            {
                Debug.LogWarning($"[Cutscene] 퀘스트 서비스(GameManager)가 없어 '{questId}' 퀘스트를 수주하지 못했습니다.");
                return;
            }

            if (!quests.TryGetQuest(questId, out var quest))
            {
                Debug.LogWarning($"[Cutscene] 퀘스트 목록에 '{questId}'가 없어 수주하지 못했습니다.");
                return;
            }

            if (!quests.TryAccept(quest))
            {
                Debug.LogWarning($"[Cutscene] '{questId}' 퀘스트를 수주하지 못했습니다. (이미 수주 · 완료했거나 조건 · 슬롯이 맞지 않음)");
            }
        }

        /// <summary>
        /// 완료 가능 상태인 퀘스트를 보고(완료 처리)하고 보상을 지급합니다. 이미 완료한 퀘스트는 조용히 넘어갑니다.
        /// 실패하면 경고만 남기고 컷씬은 계속 진행합니다.
        /// </summary>
        public void CompleteQuest(string questId)
        {
            var quests = GameManager.Instance != null ? GameManager.Instance.Quests : null;
            if (quests == null)
            {
                Debug.LogWarning($"[Cutscene] 퀘스트 서비스(GameManager)가 없어 '{questId}' 퀘스트를 완료하지 못했습니다.");
                return;
            }

            if (!quests.TryGetQuest(questId, out var quest))
            {
                Debug.LogWarning($"[Cutscene] 퀘스트 목록에 '{questId}'가 없어 완료하지 못했습니다.");
                return;
            }

            if (quests.GetState(quest) == QuestState.Completed)
            {
                return;
            }

            if (!GameManager.Instance.TryTurnInQuest(quest))
            {
                Debug.LogWarning($"[Cutscene] '{questId}' 퀘스트를 완료하지 못했습니다. (완료 가능 상태가 아니거나 런 중임)");
            }
        }
    }
}
