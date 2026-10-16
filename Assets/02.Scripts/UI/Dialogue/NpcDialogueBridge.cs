using System;
using ShotgunAndRun.NPC;
using ShotgunAndRun.Player;
using UnityEngine;
using PlayerComponent = ShotgunAndRun.Player.Player;

namespace ShotgunAndRun.UI.Dialogue
{
    /// <summary>
    /// NpcInteractable의 대화 단계(INpcDialogue)를 대화 시스템(IDialogueService)에 연결하는 브리지 컴포넌트입니다.
    /// 선택지가 없는 대화는 끝나면 NPC에 연결된 UI를 열고, 선택지가 있으면 지정한 선택지를 골랐을 때만 엽니다.
    /// NpcInteractable과 같은 오브젝트에 붙이고 NpcInteractable의 Dialogue 칸에 연결합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class NpcDialogueBridge : MonoBehaviour, INpcDialogue
    {
        [Header("서비스 의존성 (선택 사항, 비어있으면 DialogueManager.Instance 사용)")]
        [Tooltip("대화 서비스를 제공하는 컴포넌트 (IDialogueService)")]
        [SerializeField] private MonoBehaviour _dialogueServiceComponent;

        [Header("대화 데이터")]
        [Tooltip("출력할 대화 ScriptableObject 에셋")]
        [SerializeField] private DialogueDataSO _dialogueData;

        [Tooltip("이 ID의 선택지를 골랐을 때 NPC에 연결된 UI를 엽니다. (선택지가 없는 대화는 끝나면 항상 엽니다)")]
        [SerializeField] private string _proceedChoiceId = "QUEST";

        [Header("플레이어 제어")]
        [Tooltip("대화 중 플레이어의 이동 · 대시 · 사격 · 재장전 입력을 막을지 여부")]
        [SerializeField] private bool _lockPlayerActions = true;

        private IDialogueService _service;
        private PlayerInputReader _lockedInput;
        private Action _onFinished;
        private DialogueDataSO _activeData;
        private bool _isPlaying;
        private int _endedFrame = -1;

        // 대화를 닫은 E 입력이 같은 프레임에 NpcInteractable로 다시 들어가 대화가 재시작되지 않도록 종료 프레임까지 재생 중으로 취급
        public bool IsPlaying => _isPlaying || Time.frameCount == _endedFrame;

        private void Awake()
        {
            if (_dialogueServiceComponent is IDialogueService service)
            {
                _service = service;
            }
        }

        private void OnDisable()
        {
            UnlockPlayer();
        }

        public void Play(NpcInteractable speaker, Action onFinished)
        {
            Play(speaker, onFinished, null);
        }

        /// <summary>dialogueOverride가 있으면 기본 대화 대신 그 대화를 재생합니다. (null이면 기본 대화)</summary>
        public void Play(NpcInteractable speaker, Action onFinished, DialogueDataSO dialogueOverride)
        {
            if (IsPlaying)
                return;

            var data = dialogueOverride != null ? dialogueOverride : _dialogueData;
            if (data == null)
            {
                Debug.LogWarning($"[NpcDialogueBridge] '{gameObject.name}'에 DialogueDataSO가 설정되지 않았습니다.", this);
                return;
            }

            var service = GetService();
            if (service == null)
            {
                Debug.LogError("[NpcDialogueBridge] IDialogueService(DialogueManager)를 찾을 수 없습니다.", this);
                return;
            }

            if (service.IsDialogueActive)
                return;

            _isPlaying = true;
            _onFinished = onFinished;
            _activeData = data;
            LockPlayer();

            service.StartDialogue(data, HandleChoiceSelected, HandleComplete);

            // 뷰가 없는 등의 이유로 대화가 열리지 않았으면 상태를 되돌린다
            if (!service.IsDialogueActive)
            {
                EndPlaying();
                _onFinished = null;
            }
        }

        private IDialogueService GetService()
        {
            if (_service != null) return _service;
            return DialogueManager.Instance;
        }

        private void HandleChoiceSelected(string choiceId)
        {
            var onFinished = _onFinished;
            _onFinished = null;

            if (choiceId == _proceedChoiceId)
            {
                onFinished?.Invoke();
            }
        }

        private void HandleComplete()
        {
            EndPlaying();

            // 선택지가 있는 대화는 HandleChoiceSelected에서 진행 여부를 결정한다
            if (_activeData != null && _activeData.Choices.Count > 0)
                return;

            var onFinished = _onFinished;
            _onFinished = null;
            onFinished?.Invoke();
        }

        private void EndPlaying()
        {
            _isPlaying = false;
            _endedFrame = Time.frameCount;
            UnlockPlayer();
        }

        private void LockPlayer()
        {
            if (!_lockPlayerActions)
                return;

            var player = PlayerComponent.PlayerInstance;
            if (player == null)
                return;

            _lockedInput = player.GetComponent<PlayerInputReader>();
            if (_lockedInput != null)
            {
                _lockedInput.SetActionsEnabled(false);
            }
        }

        private void UnlockPlayer()
        {
            if (_lockedInput != null)
            {
                // 사망 처리는 SetGameplayEnabled로 입력 전체를 막으므로, 여기서 행동 입력을 켜도 되살아나지 않습니다.
                _lockedInput.SetActionsEnabled(true);
                _lockedInput = null;
            }
        }
    }
}
