using ShotgunAndRun.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShotgunAndRun.UI
{
    /// <summary>
    /// Tab 키를 누르고 있는 동안(Hold) 전체 맵 이미지를 표시하고,
    /// 키를 떼면(Release) 맵 이미지를 숨기는 UI 컨트롤러입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class FullMapUI : MonoBehaviour
    {
        [Header("UI Target")]
        [Tooltip("켜고 끌 맵 패널 오브젝트 (비워둘 경우 자식 오브젝트를 자동으로 탐색합니다)")]
        [SerializeField] private GameObject _mapPanel;

        [Tooltip("선택 사항: 부드러운 투명도 조절 또는 CanvasGroup을 쓸 경우 연결")]
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Key Binding")]
        [Tooltip("맵을 열기 위한 키 바인딩 (기본값: Tab)")]
        [SerializeField] private string _keyBinding = "<Keyboard>/tab";

        [Header("Options")]
        [Tooltip("맵을 보고 있는 동안 게임 시간을 일시정지할지 여부 (기본값: false - 실시간 유지)")]
        [SerializeField] private bool _pauseTimeWhileOpen = false;

        private InputAction _mapAction;
        private bool _isMapOpen;

        private void Awake()
        {
            // 타겟 패널이 지정되지 않았다면 자식 오브젝트 자동 탐색
            if (_mapPanel == null)
            {
                if (transform.childCount > 0)
                {
                    _mapPanel = transform.GetChild(0).gameObject;
                }
                else
                {
                    _mapPanel = gameObject;
                }
            }

            if (_canvasGroup == null && _mapPanel != null)
            {
                _canvasGroup = _mapPanel.GetComponent<CanvasGroup>();
            }

            CreateMapAction();
        }

        private void OnEnable()
        {
            _mapAction?.Enable();
            HideMap();
        }

        private void OnDisable()
        {
            _mapAction?.Disable();
            HideMap();
        }

        private void CreateMapAction()
        {
            _mapAction = new InputAction(
                name: "HoldMap",
                type: InputActionType.Button);

            _mapAction.AddBinding(_keyBinding);

            // Tab 키를 눌렀을 때 (Hold 시작)
            _mapAction.started += _ => ShowMap();

            // Tab 키를 뗐을 때 (Hold 종료)
            _mapAction.canceled += _ => HideMap();
        }

        /// <summary>
        /// 맵을 화면에 표시합니다.
        /// </summary>
        public void ShowMap()
        {
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Run)
            {
                return;
            }

            _isMapOpen = true;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = true;
            }

            if (_mapPanel != null && _mapPanel != gameObject)
            {
                _mapPanel.SetActive(true);
            }

            if (_pauseTimeWhileOpen)
            {
                Time.timeScale = 0f;
            }
        }

        /// <summary>
        /// 맵을 화면에서 숨깁니다.
        /// </summary>
        public void HideMap()
        {
            _isMapOpen = false;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
            }

            if (_mapPanel != null && _mapPanel != gameObject)
            {
                _mapPanel.SetActive(false);
            }

            if (_pauseTimeWhileOpen)
            {
                Time.timeScale = 1f;
            }
        }

        private void OnDestroy()
        {
            if (_mapAction != null)
            {
                _mapAction.Dispose();
                _mapAction = null;
            }
        }
    }
}
