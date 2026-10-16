using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShotgunAndRun.UI.Dialogue
{
    /// <summary>
    /// 대화창의 선택지 버튼 단일 항목을 제어하는 UI 컴포넌트입니다.
    /// 선택 상태는 버튼 Transition 대신 글자 색과 커서 표시로 직접 표현합니다.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class DialogueChoiceButtonUI : MonoBehaviour, IPointerEnterHandler
    {
        [SerializeField] private Button _button;
        [SerializeField] private TextMeshProUGUI _choiceText;

        [Header("선택 표시")]
        [Tooltip("선택되었을 때만 켜지는 커서 오브젝트 (예: >, 선택 사항). 켜고 꺼질 때 글자가 밀리지 않도록 레이아웃에서 제외해 둔다")]
        [SerializeField] private GameObject _selectedMarker;
        [SerializeField] private Color _normalTextColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        [SerializeField] private Color _selectedTextColor = new Color(1f, 0.9f, 0.25f, 1f);

        private Action _onClickCallback;
        private Action _onHoverCallback;

        private void Awake()
        {
            if (_button == null)
            {
                _button = GetComponent<Button>();
            }

            // 방향키 선택은 DialogueChoiceGroupUI가 직접 관리하므로 EventSystem 내비게이션은 끈다
            _button.navigation = new Navigation { mode = Navigation.Mode.None };
            _button.onClick.AddListener(HandleClicked);
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(HandleClicked);
            }
        }

        /// <summary>
        /// 버튼의 텍스트와 클릭 · 마우스 진입 시 실행될 콜백을 설정합니다.
        /// </summary>
        public void Setup(string text, Action onClickCallback, Action onHoverCallback = null)
        {
            if (_choiceText != null)
            {
                _choiceText.text = text;
            }

            _onClickCallback = onClickCallback;
            _onHoverCallback = onHoverCallback;
            gameObject.SetActive(true);
            SetSelected(false);
        }

        /// <summary>
        /// 선택 상태에 맞춰 글자 색과 커서 표시를 갱신합니다.
        /// </summary>
        public void SetSelected(bool selected)
        {
            if (_choiceText != null)
            {
                _choiceText.color = selected ? _selectedTextColor : _normalTextColor;
            }

            if (_selectedMarker != null)
            {
                _selectedMarker.SetActive(selected);
            }
        }

        /// <summary>
        /// 키보드 확정 입력으로 클릭과 동일하게 선택지를 실행합니다.
        /// </summary>
        public void Submit()
        {
            HandleClicked();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _onHoverCallback?.Invoke();
        }

        private void HandleClicked()
        {
            _onClickCallback?.Invoke();
        }
    }
}
