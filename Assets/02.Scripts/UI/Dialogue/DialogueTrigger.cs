using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ShotgunAndRun.UI.Dialogue
{
    /// <summary>
    /// NPC, 튜토리얼 영역, 상호작용 오브젝트에 부착하여 대화를 발동시키는 트리거 컴포넌트 (DIP 적용)
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class DialogueTrigger : MonoBehaviour
    {
        public enum TriggerMode
        {
            [Tooltip("범위 내에서 상호작용 키(기본 E)를 누르면 대화 시작")]
            OnInteractKey,

            [Tooltip("범위에 진입하면 즉시 대화 시작 (튜토리얼 구역 안내 등)")]
            OnTriggerEnterAuto,

            [Tooltip("트리거 감지 없이 외부 스크립트나 이벤트에서 직접 호출할 때만 시작")]
            ManualOnly
        }

        [Serializable]
        public struct ChoiceEventBinding
        {
            [Tooltip("DialogueDataSO에 정의된 선택지 ID (예: 'ACCEPT', 'CONFIRM')")]
            public string ChoiceId;

            [Tooltip("해당 선택지를 클릭했을 때 발동할 인스펙터 이벤트")]
            public UnityEvent OnSelected;
        }

        [Header("서비스 의존성 (선택 사항, 비어있으면 DialogueManager.Instance 사용)")]
        [Tooltip("대화 서비스를 제공하는 컴포넌트 (IDialogueService)")]
        [SerializeField] private MonoBehaviour _dialogueServiceComponent;

        [Header("대화 데이터")]
        [Tooltip("출력할 대화/튜토리얼 ScriptableObject 에셋")]
        [SerializeField] private DialogueDataSO _dialogueData;

        [Header("발동 조건")]
        [Tooltip("대화가 시작되는 방식")]
        [SerializeField] private TriggerMode _triggerMode = TriggerMode.OnInteractKey;

        [Tooltip("상호작용 키 (OnInteractKey 모드일 때 사용)")]
        [SerializeField] private KeyCode _interactKey = KeyCode.E;

        [Tooltip("트리거를 1회만 발동시키고 비활성화할지 여부 (일회성 튜토리얼 안내용)")]
        [SerializeField] private bool _oneTimeOnly = false;

        [Header("상호작용 안내 UI (선택사항)")]
        [Tooltip("플레이어가 다가왔을 때 머리 위에 띄울 'E 키로 대화' 아이콘/프롬프트")]
        [SerializeField] private GameObject _interactionPromptUI;

        [Header("선택지 및 완료 이벤트")]
        [Tooltip("선택지 ID별로 실행할 이벤트 목록 (인스펙터에서 퀘스트 수락/강화창 오픈 등 연결 가능)")]
        [SerializeField] private List<ChoiceEventBinding> _choiceEvents = new List<ChoiceEventBinding>();

        [Tooltip("선택지와 상관없이 대화가 완전히 끝났을 때 호출되는 이벤트")]
        [SerializeField] private UnityEvent _onDialogueComplete;

        private IDialogueService _service;
        private bool _isPlayerInRange = false;
        private bool _hasTriggered = false;

        private void Awake()
        {
            if (_dialogueServiceComponent is IDialogueService service)
            {
                _service = service;
            }

            if (_interactionPromptUI != null)
                _interactionPromptUI.SetActive(false);
        }

        private void Update()
        {
            if (_triggerMode != TriggerMode.OnInteractKey)
                return;

            if (_isPlayerInRange && !_hasTriggered)
            {
                if (Input.GetKeyDown(_interactKey))
                {
                    var service = GetService();
                    if (service != null && !service.IsDialogueActive)
                    {
                        TriggerDialogue();
                    }
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player"))
                return;

            if (_oneTimeOnly && _hasTriggered)
                return;

            _isPlayerInRange = true;

            if (_interactionPromptUI != null && _triggerMode == TriggerMode.OnInteractKey)
                _interactionPromptUI.SetActive(true);

            if (_triggerMode == TriggerMode.OnTriggerEnterAuto)
            {
                TriggerDialogue();
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player"))
                return;

            _isPlayerInRange = false;

            if (_interactionPromptUI != null)
                _interactionPromptUI.SetActive(false);
        }

        /// <summary>
        /// 대화를 수동으로 즉시 발동합니다.
        /// </summary>
        public void TriggerDialogue()
        {
            if (_dialogueData == null)
            {
                Debug.LogWarning($"[DialogueTrigger] '{gameObject.name}'에 DialogueDataSO가 설정되지 않았습니다.");
                return;
            }

            var service = GetService();
            if (service == null)
            {
                Debug.LogError("[DialogueTrigger] IDialogueService(DialogueManager)를 찾을 수 없습니다.");
                return;
            }

            if (_interactionPromptUI != null)
                _interactionPromptUI.SetActive(false);

            if (_oneTimeOnly)
                _hasTriggered = true;

            service.StartDialogue(_dialogueData, HandleChoiceSelected, HandleComplete);
        }

        private IDialogueService GetService()
        {
            if (_service != null) return _service;
            return DialogueManager.Instance;
        }

        private void HandleChoiceSelected(string choiceId)
        {
            for (int i = 0; i < _choiceEvents.Count; i++)
            {
                if (_choiceEvents[i].ChoiceId == choiceId)
                {
                    _choiceEvents[i].OnSelected?.Invoke();
                }
            }
        }

        private void HandleComplete()
        {
            _onDialogueComplete?.Invoke();

            if (_isPlayerInRange && (!_oneTimeOnly || !_hasTriggered) && _interactionPromptUI != null)
            {
                _interactionPromptUI.SetActive(true);
            }
        }
    }
}
