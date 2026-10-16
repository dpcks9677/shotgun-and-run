using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShotgunAndRun.UI
{
    /// <summary>
    /// 퀘스트 게시판 버튼 글자 연출. 누르는 동안 글자를 아래로 내리고, 마우스를 올리면 밑줄을 긋습니다(선택).
    /// </summary>
    [DisallowMultipleComponent]
    public class QuestButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler,
        IPointerEnterHandler
    {
        [SerializeField] private TextMeshProUGUI _label;
        [Tooltip("눌렀을 때 글자를 내리는 거리 (캔버스 단위)")]
        [SerializeField] private float _pressOffset = 3f;
        [SerializeField] private bool _underlineOnHover;

        private Vector2 _restPosition;
        private bool _hasRestPosition;
        private bool _pressed;

        private Selectable Target => GetComponent<Selectable>();

        private void OnDisable()
        {
            Release();
            SetUnderline(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_label == null || (Target != null && !Target.IsInteractable()) || _pressed)
            {
                return;
            }

            _restPosition = _label.rectTransform.anchoredPosition;
            _hasRestPosition = true;
            _pressed = true;
            _label.rectTransform.anchoredPosition = _restPosition + Vector2.down * _pressOffset;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Release();
            SetUnderline(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            SetUnderline(true);
        }

        private void Release()
        {
            if (!_pressed || _label == null)
            {
                _pressed = false;
                return;
            }

            _pressed = false;
            if (_hasRestPosition)
            {
                _label.rectTransform.anchoredPosition = _restPosition;
            }
        }

        private void SetUnderline(bool value)
        {
            if (!_underlineOnHover || _label == null)
            {
                return;
            }

            _label.fontStyle = value ? FontStyles.Underline : FontStyles.Normal;
        }
    }
}
