using System;
using Consumable;
using InventorySystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShotgunAndRun.UI
{
    public enum ConsumableSlotType
    {
        Storage,
        QuickSlot
    }

    /// <summary>
    /// 쉘터 소모품 보관함 및 퀵슬롯 연동 바에서 개별 슬롯을 제어하는 UI 컴포넌트입니다.
    /// 마우스 호버(1px 골드 테두리 및 툴팁), 원클릭 보관/인출, 드래그 앤 드롭 이동 및 스왑을 지원합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ConsumableStorageSlotUI : MonoBehaviour,
        IPointerClickHandler,
        IPointerEnterHandler,
        IPointerExitHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IDropHandler
    {
        [Header("UI Elements")]
        [SerializeField] private Image _iconImage;
        [SerializeField] private TextMeshProUGUI _quantityText;
        [SerializeField] private GameObject _highlightOverlay;
        [SerializeField] private Sprite _highlightBorderSprite;
        [SerializeField] private Image _backgroundImage;

        private ConsumableSlotType _slotType;
        private int _slotIndex = -1;
        private ConsumableData _consumableData;
        private int _quantity;
        private bool _isBorderInitialized;
        private bool _isPointerOver;

        public ConsumableSlotType SlotType => _slotType;
        public int SlotIndex => _slotIndex;
        public ConsumableData CurrentData => _consumableData;
        public int CurrentAmount => _quantity;
        public bool IsEmpty => _consumableData == null || _quantity <= 0;

        public event Action<ConsumableStorageSlotUI, PointerEventData> OnSlotClicked;
        public event Action<ConsumableStorageSlotUI, ConsumableStorageSlotUI> OnSlotDropped;

        private void Awake()
        {
            if (_quantityText != null)
            {
                _quantityText.raycastTarget = false;
            }

            if (_iconImage != null)
            {
                _iconImage.preserveAspect = true;
            }

            SetupHighlightBorder();
        }

        private void OnDisable()
        {
            if (_isPointerOver)
            {
                _isPointerOver = false;
                ItemTooltipUI.Instance?.Hide();
            }

            SetHighlight(false);
        }

        public void Setup(
            ConsumableSlotType slotType,
            int index,
            Action<ConsumableStorageSlotUI, PointerEventData> onClickCallback,
            Action<ConsumableStorageSlotUI, ConsumableStorageSlotUI> onDropCallback)
        {
            _slotType = slotType;
            _slotIndex = index;

            OnSlotClicked = null;
            if (onClickCallback != null)
            {
                OnSlotClicked += onClickCallback;
            }

            OnSlotDropped = null;
            if (onDropCallback != null)
            {
                OnSlotDropped += onDropCallback;
            }

            SetupHighlightBorder();
        }

        public void UpdateView(ConsumableData data, int quantity)
        {
            _consumableData = data;
            _quantity = quantity;

            if (data == null || quantity <= 0)
            {
                SetEmpty();
            }
            else
            {
                SetFilled(data, quantity);
            }

            if (_isPointerOver)
            {
                if (IsEmpty)
                {
                    ItemTooltipUI.Instance?.Hide();
                }
                else
                {
                    ItemTooltipUI.Instance?.Show(_consumableData, Input.mousePosition);
                }
            }
        }

        private void SetEmpty()
        {
            if (_iconImage != null)
            {
                _iconImage.sprite = null;
                _iconImage.gameObject.SetActive(false);
            }

            if (_quantityText != null)
            {
                _quantityText.text = string.Empty;
                _quantityText.gameObject.SetActive(false);
            }

            SetHighlight(false);
        }

        private void SetFilled(ConsumableData data, int quantity)
        {
            if (_iconImage != null)
            {
                _iconImage.gameObject.SetActive(true);
                _iconImage.sprite = data.Icon;
                _iconImage.color = Color.white;
                _iconImage.enabled = data.Icon != null;
            }

            if (_quantityText != null)
            {
                bool showQuantity = quantity > 1;
                _quantityText.gameObject.SetActive(showQuantity);
                if (showQuantity)
                {
                    _quantityText.text = quantity.ToString();
                }
            }
        }

        public void SetHighlight(bool active)
        {
            if (_highlightOverlay != null)
            {
                _highlightOverlay.SetActive(active);
            }
        }

        public void ResetVisualAfterDrag()
        {
            if (_iconImage != null)
            {
                _iconImage.color = Color.white;
                _iconImage.raycastTarget = true;
            }
        }

        /// <summary>
        /// 퀵슬롯/인벤토리와 1:1로 일치하는 hud_slot_border 골드 테두리 오버레이를 구축합니다.
        /// </summary>
        public void SetupHighlightBorder()
        {
            if (_isBorderInitialized) return;
            _isBorderInitialized = true;

#if UNITY_EDITOR
            if (_highlightBorderSprite == null)
            {
                _highlightBorderSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/04.Sprites/UI/hud_slot_border.png");
            }
#endif

            if (_highlightOverlay == null)
            {
                var borderTransform = transform.Find("HighlightOverlay") ?? transform.Find("Slot_Border_Highlight");
                if (borderTransform != null)
                {
                    _highlightOverlay = borderTransform.gameObject;
                }
            }

            if (_highlightOverlay == null)
            {
                GameObject borderObj = new GameObject("HighlightOverlay", typeof(RectTransform), typeof(Image));
                borderObj.transform.SetParent(transform, false);

                RectTransform rect = borderObj.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;

                Image borderImg = borderObj.GetComponent<Image>();
                borderImg.sprite = _highlightBorderSprite;
                borderImg.color = Color.white;
                borderImg.raycastTarget = false;

                borderObj.SetActive(false);
                _highlightOverlay = borderObj;
            }
            else
            {
                RectTransform rect = _highlightOverlay.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = Vector2.zero;
                }

                Image img = _highlightOverlay.GetComponent<Image>();
                if (img != null)
                {
                    if (_highlightBorderSprite != null)
                    {
                        img.sprite = _highlightBorderSprite;
                    }
                    img.color = Color.white;
                    img.raycastTarget = false;
                }

                _highlightOverlay.SetActive(false);
            }
        }

        #region Pointer & Hover Handlers

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (ItemDragHandler.Instance != null && ItemDragHandler.Instance.IsDragging &&
                ItemDragHandler.Instance.DraggedConsumableSlot == this)
            {
                return;
            }

            _isPointerOver = true;
            SetHighlight(true);

            if (!IsEmpty)
            {
                ItemTooltipUI.Instance?.Show(_consumableData, eventData.position);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isPointerOver = false;
            SetHighlight(false);
            ItemTooltipUI.Instance?.Hide();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.dragging) return;
            OnSlotClicked?.Invoke(this, eventData);
        }

        #endregion

        #region Drag and Drop Handlers

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (IsEmpty) return;
            if (ItemDragHandler.Instance == null) return;

            _isPointerOver = false;
            SetHighlight(false);
            ItemTooltipUI.Instance?.Hide();

            if (_iconImage != null)
            {
                _iconImage.color = new Color(1f, 1f, 1f, 0.5f);
                _iconImage.raycastTarget = false;
            }

            ItemDragHandler.Instance.StartDragConsumable(this, _consumableData, _quantity, eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (ItemDragHandler.Instance == null || ItemDragHandler.Instance.DraggedConsumableSlot != this) return;
            ItemDragHandler.Instance.UpdatePosition(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            ResetVisualAfterDrag();
            SetHighlight(false);

            if (ItemDragHandler.Instance != null && ItemDragHandler.Instance.DraggedConsumableSlot == this)
            {
                ItemDragHandler.Instance.EndDrag();
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            SetHighlight(false);

            if (ItemDragHandler.Instance == null) return;

            // 1. 창고 슬롯 -> 창고 슬롯 드롭
            ConsumableStorageSlotUI source = ItemDragHandler.Instance.DraggedConsumableSlot;
            if (source != null && source != this)
            {
                OnSlotDropped?.Invoke(source, this);
                return;
            }

            // 2. 실제 화면 퀵슬롯 -> 창고 슬롯 드롭
            var sourceQuickSlot = ItemDragHandler.Instance.DraggedQuickSlotItem;
            if (sourceQuickSlot != null && ShelterStorageUI.Instance != null)
            {
                ShelterStorageUI.Instance.TryDepositFromQuickSlot(sourceQuickSlot.SlotIndex, this.SlotIndex);
                return;
            }
        }

        #endregion
    }
}
