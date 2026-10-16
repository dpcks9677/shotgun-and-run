using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InventorySystem
{
    /// <summary>
    /// 인벤토리 및 상자 슬롯 아이템 드래그 앤 드롭 시 마우스를 따라다니는 고스트 아이콘을 제어하는 매니저
    /// </summary>
    public class ItemDragHandler : MonoBehaviour
    {
        private static ItemDragHandler _instance;
        private static bool _isApplicationQuitting;

        public static ItemDragHandler Instance
        {
            get
            {
                if (_isApplicationQuitting) return null;

                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<ItemDragHandler>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[ItemDragHandler]");
                        _instance = go.AddComponent<ItemDragHandler>();
                        if (Application.isPlaying)
                        {
                            DontDestroyOnLoad(go);
                        }
                    }
                }

                return _instance;
            }
        }

        [SerializeField] private Canvas _rootCanvas;
        [SerializeField] private GameObject _ghostRoot;
        [SerializeField] private Image _ghostIcon;
        [SerializeField] private TextMeshProUGUI _ghostQuantityText;

        public SlotUI DraggedSlot
        {
            get;
            private set;
        }

        public ShotgunAndRun.UI.ConsumableStorageSlotUI DraggedConsumableSlot
        {
            get;
            private set;
        }

        public ShotgunAndRun.UI.ConsumableQuickSlotItemUI DraggedQuickSlotItem
        {
            get;
            private set;
        }

        public bool IsDragging => DraggedSlot != null || DraggedConsumableSlot != null || DraggedQuickSlotItem != null;

        private RectTransform _ghostRectTransform;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                transform.SetParent(null);
                if (Application.isPlaying)
                {
                    DontDestroyOnLoad(gameObject);
                }
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            EnsureGhostHierarchy();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            _isApplicationQuitting = true;
        }

        private void EnsureGhostHierarchy()
        {
            if (_rootCanvas == null)
            {
                _rootCanvas = GetComponentInParent<Canvas>();
                if (_rootCanvas == null)
                {
                    var canvas = FindFirstObjectByType<Canvas>();
                    if (canvas != null)
                    {
                        _rootCanvas = canvas.rootCanvas;
                    }
                }
                else
                {
                    _rootCanvas = _rootCanvas.rootCanvas;
                }
            }

            if (_ghostRoot == null)
            {
                CreateDefaultGhostObject();
            }
            else
            {
                _ghostRectTransform = _ghostRoot.GetComponent<RectTransform>();
                if (_ghostIcon != null) _ghostIcon.raycastTarget = false;
                if (_ghostQuantityText != null) _ghostQuantityText.raycastTarget = false;
            }

            HideGhost();
        }

        private void CreateDefaultGhostObject()
        {
            Transform parent = _rootCanvas != null ? _rootCanvas.transform : transform;

            var ghostObj = new GameObject("DragGhostIcon", typeof(RectTransform), typeof(CanvasGroup));
            ghostObj.transform.SetParent(parent, false);
            _ghostRectTransform = ghostObj.GetComponent<RectTransform>();
            _ghostRectTransform.sizeDelta = new Vector2(64f, 64f);

            var canvasGroup = ghostObj.GetComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            var iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(ghostObj.transform, false);
            var iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.sizeDelta = Vector2.zero;

            _ghostIcon = iconObj.GetComponent<Image>();
            _ghostIcon.raycastTarget = false;
            _ghostIcon.preserveAspect = true;

            var textObj = new GameObject("Quantity", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(ghostObj.transform, false);
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.2f, 0f);
            textRect.anchorMax = new Vector2(1f, 0.45f);
            textRect.sizeDelta = Vector2.zero;

            _ghostQuantityText = textObj.GetComponent<TextMeshProUGUI>();
            _ghostQuantityText.raycastTarget = false;
            _ghostQuantityText.alignment = TextAlignmentOptions.BottomRight;
            _ghostQuantityText.fontSize = 20;

            _ghostRoot = ghostObj;
        }

        public void StartDrag(SlotUI slot, ItemData item, int quantity, Vector2 screenPosition)
        {
            if (slot == null || item == null) return;

            DraggedSlot = slot;

            // 드래그를 시작한 슬롯이 속한 실제 UI 캔버스의 루트를 우선 사용하여 WorldSpace 캔버스 오작동 방지
            var slotCanvas = slot.GetComponentInParent<Canvas>();
            if (slotCanvas != null)
            {
                _rootCanvas = slotCanvas.rootCanvas;
            }

            if (_ghostRoot == null || _ghostIcon == null)
            {
                EnsureGhostHierarchy();
            }

            if (_rootCanvas != null && _ghostRoot.transform.parent != _rootCanvas.transform)
            {
                _ghostRoot.transform.SetParent(_rootCanvas.transform, false);
            }

            _ghostRoot.transform.SetAsLastSibling(); // 맨 위 레이어에 표시
            _ghostRoot.SetActive(true);

            if (_ghostIcon != null)
            {
                _ghostIcon.sprite = item.Icon;
                _ghostIcon.color = new Color(1f, 1f, 1f, 0.95f);
            }

            if (_ghostQuantityText != null)
            {
                bool showQuantity = quantity > 1;
                _ghostQuantityText.gameObject.SetActive(showQuantity);
                if (showQuantity)
                {
                    _ghostQuantityText.text = quantity.ToString();
                }
            }

            UpdatePosition(screenPosition);
        }

        public void StartDragConsumable(
            ShotgunAndRun.UI.ConsumableStorageSlotUI slot,
            Consumable.ConsumableData item,
            int quantity,
            Vector2 screenPosition)
        {
            if (slot == null || item == null) return;

            DraggedConsumableSlot = slot;

            var slotCanvas = slot.GetComponentInParent<Canvas>();
            if (slotCanvas != null)
            {
                _rootCanvas = slotCanvas.rootCanvas;
            }

            if (_ghostRoot == null || _ghostIcon == null)
            {
                EnsureGhostHierarchy();
            }

            if (_rootCanvas != null && _ghostRoot.transform.parent != _rootCanvas.transform)
            {
                _ghostRoot.transform.SetParent(_rootCanvas.transform, false);
            }

            _ghostRoot.transform.SetAsLastSibling();
            _ghostRoot.SetActive(true);

            if (_ghostIcon != null)
            {
                _ghostIcon.sprite = item.Icon;
                _ghostIcon.color = new Color(1f, 1f, 1f, 0.95f);
            }

            if (_ghostQuantityText != null)
            {
                bool showQuantity = quantity > 1;
                _ghostQuantityText.gameObject.SetActive(showQuantity);
                if (showQuantity)
                {
                    _ghostQuantityText.text = quantity.ToString();
                }
            }

            UpdatePosition(screenPosition);
        }

        public void StartDragQuickSlot(
            ShotgunAndRun.UI.ConsumableQuickSlotItemUI slot,
            Consumable.ConsumableData item,
            int quantity,
            Vector2 screenPosition)
        {
            if (slot == null || item == null) return;

            DraggedQuickSlotItem = slot;

            var slotCanvas = slot.GetComponentInParent<Canvas>();
            if (slotCanvas != null)
            {
                _rootCanvas = slotCanvas.rootCanvas;
            }

            if (_ghostRoot == null || _ghostIcon == null)
            {
                EnsureGhostHierarchy();
            }

            if (_rootCanvas != null && _ghostRoot.transform.parent != _rootCanvas.transform)
            {
                _ghostRoot.transform.SetParent(_rootCanvas.transform, false);
            }

            _ghostRoot.transform.SetAsLastSibling();
            _ghostRoot.SetActive(true);

            if (_ghostIcon != null)
            {
                _ghostIcon.sprite = item.Icon;
                _ghostIcon.color = new Color(1f, 1f, 1f, 0.95f);
            }

            if (_ghostQuantityText != null)
            {
                bool showQuantity = quantity > 1;
                _ghostQuantityText.gameObject.SetActive(showQuantity);
                if (showQuantity)
                {
                    _ghostQuantityText.text = quantity.ToString();
                }
            }

            UpdatePosition(screenPosition);
        }

        public void UpdatePosition(Vector2 screenPosition)
        {
            if (_ghostRectTransform == null) return;

            if (_rootCanvas == null || _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                _ghostRectTransform.position = screenPosition;
            }
            else
            {
                RectTransform parentRect = _ghostRectTransform.parent as RectTransform;
                if (parentRect != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(
                        parentRect,
                        screenPosition,
                        _rootCanvas.worldCamera,
                        out Vector3 worldPos))
                {
                    _ghostRectTransform.position = worldPos;
                }
                else
                {
                    _ghostRectTransform.position = screenPosition;
                }
            }
        }

        public void EndDrag()
        {
            DraggedSlot = null;
            DraggedConsumableSlot = null;
            DraggedQuickSlotItem = null;
            HideGhost();
        }

        public void CancelDrag()
        {
            if (DraggedSlot != null)
            {
                DraggedSlot.ResetVisualAfterDrag();
            }

            if (DraggedConsumableSlot != null)
            {
                DraggedConsumableSlot.ResetVisualAfterDrag();
            }

            if (DraggedQuickSlotItem != null)
            {
                DraggedQuickSlotItem.ResetVisualAfterDrag();
            }

            EndDrag();
        }

        private void HideGhost()
        {
            if (_ghostRoot != null)
            {
                _ghostRoot.SetActive(false);
            }
        }
    }
}
