using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InventorySystem
{
    /// <summary>
    /// 개별 인벤토리 슬롯 UI 뷰 컴포넌트 (클릭 및 드래그 앤 드롭 지원)
    /// </summary>
    public class SlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI Elements")]
        [SerializeField] private Image _iconImage;
        [SerializeField] private TextMeshProUGUI _quantityText;
        [SerializeField] private GameObject _lockOverlay;
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private GameObject _highlightOverlay;
        [SerializeField] private Sprite _highlightBorderSprite;

        public int SlotIndex
        {
            get;
            private set;
        } = -1;
        public Inventory OwnerInventory
        {
            get;
            private set;
        }
        public InventorySlot CurrentSlot
        {
            get;
            private set;
        }

        public event Action<int, PointerEventData> OnSlotClicked;
        public event Action<SlotUI, SlotUI> OnItemDropped;

        private bool _isBorderInitialized;
        private bool _isPointerOver;

        private void OnDisable()
        {
            if (_isPointerOver)
            {
                _isPointerOver = false;
                ItemTooltipUI.Instance?.Hide();
            }
        }

        private void Awake()
        {
            if (_quantityText != null) _quantityText.raycastTarget = false;

            if (_lockOverlay != null)
            {
                var img = _lockOverlay.GetComponent<Image>();
                if (img != null) img.raycastTarget = false;
            }

            if (_iconImage != null)
            {
                _iconImage.preserveAspect = true;
            }

            SetupHighlightBorder();
        }

        /// <summary>
        /// 슬롯 스프라이트(hud_slot)의 모서리 깎임 형태와 1:1로 일치하는 골드 테두리 오버레이를 구축합니다.
        /// </summary>
        private void SetupHighlightBorder()
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

            if (_highlightOverlay != null)
            {
                // 이전 4변 선 오브젝트(Border_Top 등)가 자식으로 남아있다면 제거
                for (int i = _highlightOverlay.transform.childCount - 1; i >= 0; i--)
                {
                    var child = _highlightOverlay.transform.GetChild(i);
                    if (child.name.StartsWith("Border_"))
                    {
                        Destroy(child.gameObject);
                    }
                }

                RectTransform rect = _highlightOverlay.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = Vector2.zero;
                }

                var img = _highlightOverlay.GetComponent<Image>();
                if (img != null)
                {
                    if (_highlightBorderSprite != null)
                    {
                        img.sprite = _highlightBorderSprite;
                    }
                    img.color = Color.white;
                    img.raycastTarget = false;
                    img.enabled = true;
                }

                _highlightOverlay.SetActive(false);
            }
        }

        public void Setup(Inventory owner, int index)
        {
            OwnerInventory = owner;
            SlotIndex = index;
        }

        public void SetIndex(int index)
        {
            SlotIndex = index;
        }

        public void UpdateView(InventorySlot slot)
        {
            CurrentSlot = slot;

            if (slot == null)
            {
                SetEmpty();
                return;
            }

            if (slot.IsLocked)
            {
                SetLocked();
                return;
            }

            if (slot.IsEmpty)
            {
                SetEmpty();
                return;
            }

            SetFilled(slot.ItemData, slot.Quantity);

            // 마우스가 현재 슬롯 위에 올라와 있는 상태에서 데이터가 갱신된 경우 툴팁 동기화
            if (_isPointerOver)
            {
                if (slot == null || slot.IsEmpty || slot.IsLocked || slot.ItemData == null)
                {
                    ItemTooltipUI.Instance?.Hide();
                }
                else
                {
                    ItemTooltipUI.Instance?.Show(slot.ItemData, Input.mousePosition);
                }
            }
        }

        private void SetLocked()
        {
            if (_lockOverlay != null)
            {
                _lockOverlay.SetActive(true);
            }

            if (_iconImage != null)
            {
                _iconImage.gameObject.SetActive(false);
            }

            if (_quantityText != null)
            {
                _quantityText.gameObject.SetActive(false);
            }

            if (_highlightOverlay != null)
            {
                _highlightOverlay.SetActive(false);
            }
        }

        private void SetEmpty()
        {
            if (_lockOverlay != null)
            {
                _lockOverlay.SetActive(false);
            }

            if (_iconImage != null)
            {
                _iconImage.gameObject.SetActive(false);
            }

            if (_quantityText != null)
            {
                _quantityText.gameObject.SetActive(false);
            }

            if (_highlightOverlay != null)
            {
                _highlightOverlay.SetActive(false);
            }
        }

        private void SetFilled(ItemData itemData, int quantity)
        {
            if (_lockOverlay != null) _lockOverlay.SetActive(false);

            if (_iconImage != null)
            {
                _iconImage.gameObject.SetActive(true);
                _iconImage.sprite = itemData != null ? itemData.Icon : null;
                _iconImage.enabled = _iconImage.sprite != null;
                _iconImage.color = Color.white;
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

        #region Hover Handlers

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (CurrentSlot != null && CurrentSlot.IsLocked) return;
            // 드래그 중인 자기 자신 슬롯에는 호버를 켜지 않음
            if (ItemDragHandler.Instance != null && ItemDragHandler.Instance.IsDragging &&
                ItemDragHandler.Instance.DraggedSlot == this)
            {
                return;
            }

            _isPointerOver = true;
            SetHighlight(true);

            if (CurrentSlot != null && !CurrentSlot.IsEmpty && !CurrentSlot.IsLocked && CurrentSlot.ItemData != null)
            {
                ItemTooltipUI.Instance?.Show(CurrentSlot.ItemData, eventData.position);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isPointerOver = false;
            SetHighlight(false);
            ItemTooltipUI.Instance?.Hide();
        }

        #endregion

        #region Drag and Drop Handlers

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (CurrentSlot == null || CurrentSlot.IsLocked || CurrentSlot.IsEmpty) return;
            if (ItemDragHandler.Instance == null) return;

            _isPointerOver = false;
            SetHighlight(false);
            ItemTooltipUI.Instance?.Hide();

            if (_iconImage != null)
            {
                _iconImage.color = new Color(1f, 1f, 1f, 0.6f);
                _iconImage.raycastTarget = false;
            }

            ItemDragHandler.Instance.StartDrag(this, CurrentSlot.ItemData, CurrentSlot.Quantity, eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (ItemDragHandler.Instance == null || ItemDragHandler.Instance.DraggedSlot != this) return;
            ItemDragHandler.Instance.UpdatePosition(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            ResetVisualAfterDrag();
            SetHighlight(false);

            if (ItemDragHandler.Instance != null && ItemDragHandler.Instance.DraggedSlot == this)
            {
                ItemDragHandler.Instance.EndDrag();
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            SetHighlight(false);

            if (ItemDragHandler.Instance == null) return;

            var sourceSlot = ItemDragHandler.Instance.DraggedSlot;

            // 1. sourceSlot의 null 검사 및 파괴 여부, 씬 전환 및 비활성화 방어
            if (sourceSlot == null || sourceSlot.gameObject == null || !sourceSlot.gameObject.activeInHierarchy ||
                sourceSlot == this)
            {
                return;
            }

            // 2. 소스 슬롯 데이터 상태 검사 (잠금 또는 빈 슬롯 방어)
            if (sourceSlot.CurrentSlot == null || sourceSlot.CurrentSlot.IsLocked ||
                sourceSlot.CurrentSlot.IsEmpty)
            {
                return;
            }

            // 3. 대상 슬롯(현재 슬롯) 데이터 상태 검사 (잠금 슬롯 방어)
            if (CurrentSlot != null && CurrentSlot.IsLocked) return;

            // 4. 인벤토리 소유자 null 검사
            if (sourceSlot.OwnerInventory == null || OwnerInventory == null) return;

            // 5. 슬롯 인덱스 유효 범위 검사
            if (sourceSlot.SlotIndex < 0 || SlotIndex < 0) return;

            // 6. 정상 맞교환(Swap) 또는 동일 인벤토리 슬롯 교환 수행
            if (sourceSlot.OwnerInventory == OwnerInventory)
            {
                OwnerInventory.SwapSlots(sourceSlot.SlotIndex, SlotIndex);
            }
            else
            {
                sourceSlot.OwnerInventory.SwapBetweenInventories(
                    sourceSlot.SlotIndex,
                    OwnerInventory,
                    SlotIndex);
            }

            OnItemDropped?.Invoke(sourceSlot, this);
        }

        #endregion

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.dragging) return;
            OnSlotClicked?.Invoke(SlotIndex, eventData);
        }
    }
}
