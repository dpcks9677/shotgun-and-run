using System;
using System.Collections.Generic;
using ShotgunAndRun.Core;
using ShotgunAndRun.NPC;
using ShotgunAndRun.UI.Dialogue;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 영구 플래그에 따라 NPC 평상시 대사를 바꿉니다. 조건이 맞는 첫 항목의 대사를 같은 오브젝트의 NpcDialogueBridge로 재생하고,
    /// 맞는 항목이 없으면 브리지의 기본 대사를 재생합니다. 항목마다 대사 뒤 NPC UI(OnInteract)를 열지 정합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlagDialogueSelector : MonoBehaviour, INpcDialogue
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("모두 있어야 이 항목을 쓰는 영구 플래그")]
            public List<string> RequiredFlags = new List<string>();

            [Tooltip("하나라도 있으면 이 항목을 쓰지 않는 영구 플래그")]
            public List<string> BlockedFlags = new List<string>();

            [Tooltip("재생할 대사")]
            public DialogueDataSO Dialogue;

            [Tooltip("대사가 끝난 뒤 NPC UI를 열지 여부. 끄면 대사만 하고 닫음")]
            public bool OpenUi = true;
        }

        [Tooltip("대사를 재생할 브리지 (비워 두면 같은 오브젝트에서 찾음)")]
        [SerializeField] private NpcDialogueBridge _bridge;

        [Tooltip("위에서부터 조건이 맞는 첫 항목의 대사를 재생. 맞는 항목이 없으면 브리지의 기본 대사 재생")]
        [SerializeField] private List<Entry> _entries = new List<Entry>();

        public bool IsPlaying => _bridge != null && _bridge.IsPlaying;

        private void OnValidate()
        {
            if (_bridge == null)
            {
                _bridge = GetComponent<NpcDialogueBridge>();
            }
        }

        public void Play(NpcInteractable speaker, Action onFinished)
        {
            if (_bridge == null)
            {
                Debug.LogError($"[FlagDialogueSelector] '{name}'에 NpcDialogueBridge가 연결되지 않았습니다.", this);
                return;
            }

            var entry = FindEntry();
            if (entry != null)
            {
                _bridge.Play(speaker, entry.OpenUi ? onFinished : null, entry.Dialogue);
            }
            else
            {
                _bridge.Play(speaker, onFinished);
            }
        }

        private Entry FindEntry()
        {
            var quests = GameManager.Instance != null ? GameManager.Instance.Progress?.Quests : null;
            if (quests == null) return null;

            foreach (var entry in _entries)
            {
                if (entry == null) continue;

                bool match = true;
                foreach (string flag in entry.RequiredFlags)
                {
                    if (!quests.HasFlag(flag))
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    foreach (string flag in entry.BlockedFlags)
                    {
                        if (quests.HasFlag(flag))
                        {
                            match = false;
                            break;
                        }
                    }
                }

                if (match) return entry;
            }

            return null;
        }
    }
}
