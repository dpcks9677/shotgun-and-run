using System;
using System.Collections;
using Consumable;
using InventorySystem;
using ShotgunAndRun.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShotgunAndRun.UI
{
    /// <summary>
    /// 개별 소모품 퀵슬롯 UI를 제어하는 컴포넌트입니다.
    /// 아이템 아이콘, 보유 수량, 단축키 번호, 선택 하이라이트 상태를 표현하며,
    /// 창고 UI(ShelterStorageUI)와의 실시간 원클릭 및 드래그 앤 드롭 상호작용을 지원합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ConsumableQuickSlotItemUI : MonoBehaviour,
        IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI Components")]
        [Tooltip("슬롯 클릭을 감지할 버튼 (없으면 자식 컴포넌트 자동 탐색)")]
        [SerializeField] private Button _slotButton;

        [Tooltip("소모품 아이콘을 표시할 이미지")]
        [SerializeField] private Image _iconImage;

        [Tooltip("보유 수량을 표시할 텍스트")]
        [SerializeField] private TextMeshProUGUI _quantityText;

        [Tooltip("단축키 번호 텍스트 (예: '1', '2', '3')")]
        [SerializeField] private TextMeshProUGUI _keyText;

        [Tooltip("현재 선택된 슬롯임을 강조하는 하이라이트 오브젝트/테두리")]
        [SerializeField] private GameObject _highlightObject;
        [SerializeField] private Sprite _highlightBorderSprite;
        [SerializeField] private GameObject _emptyGraphic;
        [SerializeField] private Vector3 _selectedScale = Vector3.one;

        private static readonly Color ErrorHighlightColor = new Color(1f, 0.25f, 0.25f, 1f);

        private int _slotIndex = -1;
        private Action<int> _onClickCallback;
        private Vector3 _normalScale = Vector3.one;

        private ConsumableData _currentData;
        private int _currentQuantity;
        private bool _isSelected;
        private bool _isPointerOver;

        private RectTransform _rectTransform;
        private Vector2 _originalAnchoredPosition;
        private Image _highlightImage;
        private Coroutine _shakeCoroutine;

        public int SlotIndex => _slotIndex;
        public ConsumableData CurrentData => _currentData;
        public int CurrentQuantity => _currentQuantity;
        public bool IsEmpty => _currentData == null || _currentQuantity <= 0;

        private void Awake()
        {
            _normalScale = Vector3.one;
            _selectedScale = Vector3.one;
            transform.localScale = Vector3.one;

            _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform != null)
            {
                _originalAnchoredPosition = _rectTransform.anchoredPosition;
            }

            if (_slotButton == null)
            {
                _slotButton = GetComponent<Button>() ?? GetComponentInChildren<Button>();
            }

            if (_slotButton != null)
            {
                _slotButton.onClick.AddListener(OnButtonClicked);
            }

            // 아이콘 도트 PPU 최적화 (16px 원본의 4배 정수 배율인 64x64 및 종횡비 유지)
            if (_iconImage != null)
            {
                _iconImage.preserveAspect = true;
                _iconImage.rectTransform.sizeDelta = new Vector2(64f, 64f);
            }

            SetupSlotBorderHighlight();
        }

        private void OnDestroy()
        {
            if (_slotButton != null)
            {
                _slotButton.onClick.RemoveListener(OnButtonClicked);
            }
        }

        private void OnDisable()
        {
            ResetRejectFeedback();

            if (_isPointerOver)
            {
                _isPointerOver = false;
                ItemTooltipUI.Instance?.Hide();
                if (!_isSelected && _highlightObject != null)
                {
                    _highlightObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 슬롯의 인덱스와 클릭 콜백을 초기화합니다.
        /// </summary>
        public void Initialize(int slotIndex, Action<int> onClickCallback, string keyLabel = null)
        {
            _slotIndex = slotIndex;
            _onClickCallback = onClickCallback;

            if (_keyText != null)
            {
                _keyText.text = string.IsNullOrEmpty(keyLabel) ? (slotIndex + 1).ToString() : keyLabel;
            }

            SetSelected(false);
            SetEmpty();
        }

        /// <summary>
        /// 슬롯의 데이터(아이콘, 수량)를 갱신합니다.
        /// </summary>
        public void UpdateData(ConsumableData data, int quantity)
        {
            _currentData = data;
            _currentQuantity = quantity;

            if (data == null || quantity <= 0)
            {
                SetEmpty();
                return;
            }

            // 아이콘 설정
            if (_iconImage != null)
            {
                _iconImage.gameObject.SetActive(true);
                _iconImage.sprite = data.Icon;
                _iconImage.color = Color.white;
            }

            // 수량 텍스트 설정
            if (_quantityText != null)
            {
                _quantityText.gameObject.SetActive(true);
                _quantityText.text = quantity > 1 ? quantity.ToString() : string.Empty;
            }

            if (_emptyGraphic != null)
            {
                _emptyGraphic.SetActive(false);
            }

            if (_isPointerOver)
            {
                ItemTooltipUI.Instance?.Show(_currentData, Input.mousePosition);
            }
        }

        /// <summary>
        /// 슬롯을 빈 상태로 변경합니다.
        /// </summary>
        public void SetEmpty()
        {
            _currentData = null;
            _currentQuantity = 0;

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

            if (_emptyGraphic != null)
            {
                _emptyGraphic.SetActive(true);
            }

            if (_isPointerOver)
            {
                ItemTooltipUI.Instance?.Hide();
            }
        }

        /// <summary>
        /// 현재 슬롯의 선택/하이라이트 상태를 설정합니다.
        /// </summary>
        public void SetSelected(bool isSelected)
        {
            _isSelected = isSelected;

            if (_shakeCoroutine == null && _highlightObject != null)
            {
                _highlightObject.SetActive(isSelected || _isPointerOver);
            }

            transform.localScale = isSelected ? _selectedScale : _normalScale;
        }

        private void OnButtonClicked()
        {
            // UI 내비게이션/버튼 클릭 시 슬롯 선택/장착 토글 (창고 입고는 마우스 더블 클릭으로만 수행)
            _onClickCallback?.Invoke(_slotIndex);
        }

        #region Pointer & Hover Handlers

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (ItemDragHandler.Instance != null && ItemDragHandler.Instance.IsDragging &&
                ItemDragHandler.Instance.DraggedQuickSlotItem == this)
            {
                return;
            }

            _isPointerOver = true;
            if (_shakeCoroutine == null && _highlightObject != null)
            {
                _highlightObject.SetActive(true);
            }

            if (!IsEmpty)
            {
                ItemTooltipUI.Instance?.Show(_currentData, eventData.position);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isPointerOver = false;
            ItemTooltipUI.Instance?.Hide();

            if (_shakeCoroutine == null && !_isSelected && _highlightObject != null)
            {
                _highlightObject.SetActive(false);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.dragging) return;
            if (eventData.button != PointerEventData.InputButton.Left) return;

            // 창고 UI가 열려 있는 경우
            if (ShelterStorageUI.Instance != null && ShelterStorageUI.Instance.IsOpen)
            {
                // 좌클릭 더블 클릭 시 소모품 보관함으로 입고
                if (eventData.clickCount >= 2 && !IsEmpty)
                {
                    // 1. 소모품 보관함 뷰가 아니라면 먼저 전환하여 아이템이 들어가는 모습을 즉시 확인
                    if (ShelterStorageUI.Instance.CurrentView != StorageViewMode.Consumable)
                    {
                        ShelterStorageUI.Instance.SetView(StorageViewMode.Consumable);
                    }

                    // 2. 보관함으로 입고 시도
                    bool success = ShelterStorageUI.Instance.TryDepositFromQuickSlot(_slotIndex);
                    if (!success)
                    {
                        // 보관함이 가득 차서 입고 실패 시 빨간색 하이라이트 + 좌우 흔들림 피드백
                        PlayRejectFeedback();
                    }
                    return;
                }

                // 단일 클릭인 경우: 슬롯 선택/토글 수행
                _onClickCallback?.Invoke(_slotIndex);
                return;
            }

            // 평상시: 기존 소모품 선택/장착 토글
            _onClickCallback?.Invoke(_slotIndex);
        }

        #endregion

        #region Drag and Drop Handlers

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (IsEmpty) return;
            if (ItemDragHandler.Instance == null) return;

            _isPointerOver = false;
            ItemTooltipUI.Instance?.Hide();

            if (_iconImage != null)
            {
                _iconImage.color = new Color(1f, 1f, 1f, 0.5f);
            }

            ItemDragHandler.Instance.StartDragQuickSlot(this, _currentData, _currentQuantity, eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (ItemDragHandler.Instance == null || ItemDragHandler.Instance.DraggedQuickSlotItem != this) return;
            ItemDragHandler.Instance.UpdatePosition(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            ResetVisualAfterDrag();

            if (!_isSelected && _highlightObject != null)
            {
                _highlightObject.SetActive(false);
            }

            if (ItemDragHandler.Instance != null && ItemDragHandler.Instance.DraggedQuickSlotItem == this)
            {
                ItemDragHandler.Instance.EndDrag();
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (ItemDragHandler.Instance == null) return;

            // 1. 창고 슬롯 -> 실제 퀵슬롯 드롭 (인출)
            var storageSource = ItemDragHandler.Instance.DraggedConsumableSlot;
            if (storageSource != null && ShelterStorageUI.Instance != null)
            {
                ShelterStorageUI.Instance.TryWithdrawToQuickSlot(storageSource.SlotIndex, _slotIndex);
                return;
            }

            // 2. 다른 실제 퀵슬롯 -> 실제 퀵슬롯 드롭 (스왑)
            var quickSource = ItemDragHandler.Instance.DraggedQuickSlotItem;
            if (quickSource != null && quickSource != this)
            {
                SwapWithQuickSlot(quickSource.SlotIndex, _slotIndex);
                return;
            }
        }

        public void ResetVisualAfterDrag()
        {
            if (_iconImage != null)
            {
                _iconImage.color = Color.white;
            }
        }

        private void SwapWithQuickSlot(int sourceIndex, int targetIndex)
        {
            var player = ShotgunAndRun.Player.Player.PlayerInstance;
            var playerInv = player != null ? (player.ConsumableInventory ?? player.GetComponent<Player.PlayerConsumableInventory>()) : FindAnyObjectByType<Player.PlayerConsumableInventory>();
            if (playerInv == null) return;

            var sSlot = playerInv.GetSlot(sourceIndex);
            var tSlot = playerInv.GetSlot(targetIndex);
            if (sSlot == null || tSlot == null) return;

            if (tSlot.IsEmpty)
            {
                playerInv.SetSlot(targetIndex, sSlot.Data, sSlot.Amount);
                playerInv.SetSlot(sourceIndex, null, 0);
            }
            else if (sSlot.Data == tSlot.Data)
            {
                int space = tSlot.Data.MaxStack - tSlot.Amount;
                int add = Mathf.Min(space, sSlot.Amount);
                tSlot.AddAmount(add);
                sSlot.AddAmount(-add);
                playerInv.SetSlot(targetIndex, tSlot.Data, tSlot.Amount);
                playerInv.SetSlot(sourceIndex, sSlot.Amount > 0 ? sSlot.Data : null, sSlot.Amount);
            }
            else
            {
                var tempD = sSlot.Data;
                int tempA = sSlot.Amount;
                playerInv.SetSlot(sourceIndex, tSlot.Data, tSlot.Amount);
                playerInv.SetSlot(targetIndex, tempD, tempA);
            }

            GameManager.Instance?.SaveProgress();
        }

        #endregion

        /// <summary>
        /// QuickSlot_1, 2, 3의 슬롯 스프라이트(hud_slot)의 깎인 가장자리 형태와
        /// 1:1로 정확히 일치하는 골드 테두리 오브젝트를 구성합니다.
        /// </summary>
        private void SetupSlotBorderHighlight()
        {
            var legacyOutline = transform.Find("Icon_Outline_Root");
            if (legacyOutline != null)
            {
                Destroy(legacyOutline.gameObject);
            }

            var legacyBorder = transform.Find("Slot_Border_Highlight");
            if (legacyBorder != null)
            {
                // 이전 4변 선 오브젝트(자식으로 Border_Top 등이 있는 경우) 정리
                if (legacyBorder.Find("Border_Top") != null)
                {
                    Destroy(legacyBorder.gameObject);
                }
                else
                {
                    _highlightObject = legacyBorder.gameObject;
                }
            }

#if UNITY_EDITOR
            if (_highlightBorderSprite == null)
            {
                _highlightBorderSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/04.Sprites/UI/hud_slot_border.png");
            }
#endif

            if (_highlightObject != null)
            {
                var img = _highlightObject.GetComponent<Image>();
                if (img != null)
                {
                    if (_highlightBorderSprite != null)
                    {
                        img.sprite = _highlightBorderSprite;
                    }
                    img.color = Color.white;
                    img.raycastTarget = false;
                }
                _highlightImage = img;
                return;
            }

            GameObject borderObj = new GameObject("Slot_Border_Highlight", typeof(RectTransform), typeof(Image));
            borderObj.transform.SetParent(transform, false);

            RectTransform rect = borderObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero; // 88x88 슬롯과 100% 동일한 크기로 모서리 깎임까지 일치

            Image borderImg = borderObj.GetComponent<Image>();
            borderImg.sprite = _highlightBorderSprite;
            borderImg.color = Color.white;
            borderImg.raycastTarget = false;

            borderObj.SetActive(false);
            _highlightObject = borderObj;
            _highlightImage = borderImg;
        }

        #region Error / Rejection Feedback (Red Highlight & Shake)

        /// <summary>
        /// 창고 보관함이 가득 차서 입고에 실패했을 때 빨간색 하이라이트와 좌우 흔들림(Shake) 피드백을 재생합니다.
        /// </summary>
        public void PlayRejectFeedback()
        {
            if (!gameObject.activeInHierarchy) return;

            if (_rectTransform == null)
            {
                _rectTransform = GetComponent<RectTransform>();
            }

            if (_shakeCoroutine != null)
            {
                StopCoroutine(_shakeCoroutine);
                if (_rectTransform != null)
                {
                    _rectTransform.anchoredPosition = _originalAnchoredPosition;
                }
            }
            else if (_rectTransform != null)
            {
                _originalAnchoredPosition = _rectTransform.anchoredPosition;
            }

            _shakeCoroutine = StartCoroutine(RejectShakeRoutine());
        }

        private IEnumerator RejectShakeRoutine()
        {
            const float duration = 0.25f;
            const float shakeMagnitude = 6f;
            const float shakeSpeed = 50f;
            float elapsed = 0f;

            if (_highlightImage == null && _highlightObject != null)
            {
                _highlightImage = _highlightObject.GetComponent<Image>();
            }

            if (_highlightImage != null)
            {
                _highlightImage.color = ErrorHighlightColor;
            }

            if (_highlightObject != null)
            {
                _highlightObject.SetActive(true);
            }

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float decay = 1f - Mathf.Clamp01(elapsed / duration);
                float offsetX = Mathf.Sin(elapsed * shakeSpeed) * shakeMagnitude * decay;

                if (_rectTransform != null)
                {
                    _rectTransform.anchoredPosition = _originalAnchoredPosition + new Vector2(offsetX, 0f);
                }

                yield return null;
            }

            ResetRejectFeedback();
        }

        private void ResetRejectFeedback()
        {
            if (_shakeCoroutine != null)
            {
                StopCoroutine(_shakeCoroutine);
                _shakeCoroutine = null;
            }

            if (_rectTransform != null)
            {
                _rectTransform.anchoredPosition = _originalAnchoredPosition;
            }

            if (_highlightImage != null)
            {
                _highlightImage.color = Color.white;
            }

            if (_highlightObject != null)
            {
                _highlightObject.SetActive(_isSelected || _isPointerOver);
            }
        }

        #endregion
    }
}
