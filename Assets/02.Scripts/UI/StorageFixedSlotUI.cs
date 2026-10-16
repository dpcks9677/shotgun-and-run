using InventorySystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShotgunAndRun.UI
{
    /// <summary>
    /// 쉘터 창고의 고정형 디스플레이 슬롯입니다.
    /// 특정 ItemData에 영구 귀속되며, 미보유 시에는 반투명(알파 조절) 처리되어 자리를 나타내고
    /// 보유 시에는 선명한 색상과 수량을 표시합니다.
    /// 마우스 호버 시 아이템 정보 팝업(ItemTooltipUI)을 표시합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class StorageFixedSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Item Binding")]
        [SerializeField] private ItemData _assignedItem;

        [Header("UI Elements")]
        [SerializeField] private Image _iconImage;

        [SerializeField] private TextMeshProUGUI _amountText;

        [Header("Alpha Settings (미보유/보유 투명도)")]
        [SerializeField, Range(0.05f, 0.5f)] private float _emptyAlpha = 0.25f;

        [SerializeField, Range(0.5f, 1f)] private float _possessedAlpha = 1.0f;

        [SerializeField] private bool _showZeroAmountText = false;

        public ItemData AssignedItem => _assignedItem;
        public int CurrentAmount
        {
            get;
            private set;
        }
        public bool IsPossessed => CurrentAmount > 0;

        private bool _isPointerOver;

        private void Awake()
        {
            if (_amountText != null)
            {
                _amountText.raycastTarget = false;
            }

            if (_iconImage != null)
            {
                _iconImage.raycastTarget = false;
            }
        }

        private void OnDisable()
        {
            if (_isPointerOver)
            {
                _isPointerOver = false;
                ItemTooltipUI.Instance?.Hide();
            }
        }

        /// <summary>
        /// 슬롯에 특정 아이템 정의를 고정 할당합니다.
        /// </summary>
        public void Bind(ItemData item)
        {
            _assignedItem = item;

            if (_iconImage != null)
            {
                _iconImage.sprite = _assignedItem != null ? _assignedItem.Icon : null;
                _iconImage.enabled = _assignedItem != null && _assignedItem.Icon != null;
            }

            UpdateView(0);
        }

        /// <summary>
        /// 현재 창고의 보유 수량을 바탕으로 슬롯의 알파값과 수량 텍스트를 갱신합니다.
        /// </summary>
        public void UpdateView(int amount)
        {
            CurrentAmount = Mathf.Max(0, amount);

            if (_iconImage != null && _assignedItem != null)
            {
                Color color = Color.white;
                color.a = IsPossessed ? _possessedAlpha : _emptyAlpha;
                _iconImage.color = color;
            }

            if (_amountText != null)
            {
                if (IsPossessed)
                {
                    _amountText.gameObject.SetActive(true);
                    _amountText.text = $"x{CurrentAmount}";
                    _amountText.color = Color.white;
                }
                else
                {
                    if (_showZeroAmountText)
                    {
                        _amountText.gameObject.SetActive(true);
                        _amountText.text = "0";
                        _amountText.color = new Color(1f, 1f, 1f, _emptyAlpha);
                    }
                    else
                    {
                        _amountText.gameObject.SetActive(false);
                    }
                }
            }

            if (_isPointerOver && _assignedItem != null)
            {
                ItemTooltipUI.Instance?.Show(_assignedItem, Input.mousePosition);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isPointerOver = true;

            if (_assignedItem != null)
            {
                ItemTooltipUI.Instance?.Show(_assignedItem, eventData.position);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isPointerOver = false;
            ItemTooltipUI.Instance?.Hide();
        }
    }
}
