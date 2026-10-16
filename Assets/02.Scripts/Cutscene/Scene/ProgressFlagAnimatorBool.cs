using System.Collections.Generic;
using ShotgunAndRun.Core;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 영구 플래그에 따라 Animator bool 파라미터를 정합니다. (예: 쉘터 포탈 — 점등 전 검은 포탈, 점등 후 파란 포탈)
    /// 씬을 시작할 때는 전환 연출 없이 해당 상태로 바로 들어가고, 컷씬 도중 SetValue로 바꾸면 Animator의 전환 연출이 재생됩니다.
    /// 컷씬이 끝나면(완료 플래그 저장 후) 다시 계산합니다.
    /// </summary>
    public sealed class ProgressFlagAnimatorBool : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private string _parameter = "Active";

        [Tooltip("모두 있어야 켜지는 영구 플래그")]
        [SerializeField] private List<string> _requiredFlags = new List<string>();

        [Tooltip("하나라도 있으면 꺼지는 영구 플래그")]
        [SerializeField] private List<string> _blockedFlags = new List<string>();

        [Tooltip("씬 시작 시 켜진 상태라면 바로 재생할 상태 이름 (비우면 Animator 전환을 따른다)")]
        [SerializeField] private string _onState = "Idle_Active";

        [Tooltip("씬 시작 시 꺼진 상태라면 바로 재생할 상태 이름 (비우면 Animator 전환을 따른다)")]
        [SerializeField] private string _offState = "Idle_Disabled";

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
            if (!TryEvaluate(out bool value)) return;

            SetValue(value);
            string state = value ? _onState : _offState;
            if (_animator != null && !string.IsNullOrEmpty(state))
            {
                _animator.Play(state, 0, 0f);
            }
        }

        /// <summary>파라미터를 바꿉니다. 컷씬 신호(UnityEvent)에서 부르면 Animator 전환 연출이 재생됩니다.</summary>
        public void SetValue(bool value)
        {
            if (_animator == null) return;

            _animator.SetBool(_parameter, value);
        }

        private void HandleCutsceneFinished(CutsceneAsset asset, bool completed)
        {
            if (completed && TryEvaluate(out bool value))
            {
                SetValue(value);
            }
        }

        private bool TryEvaluate(out bool value)
        {
            value = false;
            var quests = GameManager.Instance != null ? GameManager.Instance.Progress?.Quests : null;
            if (quests == null)
            {
                Debug.LogWarning("[ProgressFlagAnimatorBool] 진행 데이터(GameManager)가 없어 배치된 상태를 유지합니다.", this);
                return false;
            }

            foreach (string flag in _requiredFlags)
            {
                if (!quests.HasFlag(flag)) return true;
            }

            foreach (string flag in _blockedFlags)
            {
                if (quests.HasFlag(flag)) return true;
            }

            value = true;
            return true;
        }
    }
}
