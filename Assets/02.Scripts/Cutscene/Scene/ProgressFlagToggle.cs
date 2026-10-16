using System.Collections.Generic;
using ShotgunAndRun.Core;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 영구 플래그에 따라 대상 오브젝트를 켜고 끕니다. (예: 쉘터 출격 입구 4단계 상태)
    /// requiredFlags가 모두 있고 blockedFlags가 하나도 없을 때만 대상이 켜집니다.
    /// 이 컴포넌트는 항상 켜져 있는 오브젝트에 두고, 대상은 꺼진 상태여도 됩니다.
    /// 컷씬이 끝나면(완료 플래그 저장 후) 자동으로 다시 계산합니다. 컷씬 도중 전환은 CutsceneSignal의 UnityEvent로 ForceActive(bool)를 부릅니다.
    /// </summary>
    public sealed class ProgressFlagToggle : MonoBehaviour
    {
        [Tooltip("모두 있어야 켜지는 영구 플래그")]
        [SerializeField] private List<string> _requiredFlags = new List<string>();

        [Tooltip("하나라도 있으면 꺼지는 영구 플래그")]
        [SerializeField] private List<string> _blockedFlags = new List<string>();

        [Tooltip("조건에 따라 켜고 끌 오브젝트")]
        [SerializeField] private List<GameObject> _targets = new List<GameObject>();

        private void OnEnable()
        {
            CutsceneDirector.CutsceneFinished += HandleCutsceneFinished;
        }

        private void OnDisable()
        {
            CutsceneDirector.CutsceneFinished -= HandleCutsceneFinished;
        }

        private void Start()
        {
            Refresh();
        }

        // 컷씬이 끝나면 완료 플래그가 저장된 뒤이므로 다시 계산한다
        private void HandleCutsceneFinished(CutsceneAsset asset, bool completed)
        {
            if (completed) Refresh();
        }

        /// <summary>현재 영구 플래그로 대상 상태를 다시 계산합니다. GameManager가 없으면 배치된 상태를 유지합니다.</summary>
        public void Refresh()
        {
            var quests = GameManager.Instance != null ? GameManager.Instance.Progress?.Quests : null;
            if (quests == null)
            {
                Debug.LogWarning("ProgressFlagToggle: GameManager progress is not available. Targets are left as authored.", this);
                return;
            }

            bool active = true;
            foreach (string flag in _requiredFlags)
            {
                if (!quests.HasFlag(flag))
                {
                    active = false;
                    break;
                }
            }

            if (active)
            {
                foreach (string flag in _blockedFlags)
                {
                    if (quests.HasFlag(flag))
                    {
                        active = false;
                        break;
                    }
                }
            }

            ForceActive(active);
        }

        /// <summary>플래그와 무관하게 대상을 켜거나 끕니다. 컷씬 도중 전환용입니다.</summary>
        public void ForceActive(bool active)
        {
            foreach (GameObject target in _targets)
            {
                if (target != null)
                {
                    target.SetActive(active);
                }
            }
        }
    }
}
