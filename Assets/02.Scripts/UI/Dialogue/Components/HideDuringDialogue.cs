using UnityEngine;

namespace ShotgunAndRun.UI.Dialogue
{
    /// <summary>
    /// 대화창이 열려 있는 동안 이 UI를 숨기는 컴포넌트입니다. (퀵슬롯처럼 대화창과 겹치는 HUD용)
    /// SetActive 대신 CanvasGroup으로 숨겨서, 대상 UI의 OnEnable/OnDisable 이벤트 구독이 끊기지 않게 합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public class HideDuringDialogue : MonoBehaviour
    {
        [Header("서비스 의존성 (선택 사항, 비어있으면 DialogueManager.Instance 사용)")]
        [Tooltip("대화 서비스를 제공하는 컴포넌트 (IDialogueService)")]
        [SerializeField] private MonoBehaviour _dialogueServiceComponent;

        private CanvasGroup _canvasGroup;
        private IDialogueService _service;
        private bool _isHidden;

        // 숨기기 전 CanvasGroup 상태 (복원용)
        private float _savedAlpha;
        private bool _savedInteractable;
        private bool _savedBlocksRaycasts;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();

            if (_dialogueServiceComponent is IDialogueService service)
            {
                _service = service;
            }
        }

        // 대화는 다른 컴포넌트의 Update에서 열리므로, 같은 프레임 안에 반영되도록 LateUpdate에서 확인한다
        private void LateUpdate()
        {
            var service = GetService();
            bool shouldHide = service != null && service.IsDialogueActive;

            if (shouldHide != _isHidden)
            {
                SetHidden(shouldHide);
            }
        }

        private void OnDisable()
        {
            if (_isHidden)
            {
                SetHidden(false);
            }
        }

        private IDialogueService GetService()
        {
            if (_service != null) return _service;
            return DialogueManager.Instance;
        }

        private void SetHidden(bool hidden)
        {
            _isHidden = hidden;

            if (hidden)
            {
                _savedAlpha = _canvasGroup.alpha;
                _savedInteractable = _canvasGroup.interactable;
                _savedBlocksRaycasts = _canvasGroup.blocksRaycasts;

                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }
            else
            {
                _canvasGroup.alpha = _savedAlpha;
                _canvasGroup.interactable = _savedInteractable;
                _canvasGroup.blocksRaycasts = _savedBlocksRaycasts;
            }
        }
    }
}
