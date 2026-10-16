using System.Collections.Generic;
using ShotgunAndRun.Player;
using ShotgunAndRun.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace InventorySystem
{
    /// <summary>
    /// 인벤토리 전체 창 및 슬롯 그리드를 제어하는 UI 컴포넌트
    /// (CanvasGroup을 사용하여 스크립트 비활성화 없이 안전하게 열기/닫기 토글)
    /// </summary>
    public class InventoryUI : MonoBehaviour, IPopupUI
    {
        public static InventoryUI Instance
        {
            get;
            private set;
        }

        [Header("References")]
        [SerializeField] private Inventory _inventory;
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Transform _slotContainer;
        [SerializeField] private SlotUI _slotPrefab;

        [Header("Settings")]
        [SerializeField] private bool _enableKeyToggle = true;
        [SerializeField] private bool _startOpen = false;

        private readonly List<SlotUI> _slotUIList = new();
        private InputAction _toggleAction;
        private InputAction _closeAction;
        private Inventory _targetInventory;

        public bool IsOpen => _canvasGroup != null && _canvasGroup.alpha > 0.5f;
        public Inventory PlayerInventory => _inventory;
        public Inventory TargetInventory => _targetInventory;

        /// <summary>
        /// 현재 상호작용 중인 외부 컨테이너(루트 상자, 창고, 상점 등) 인벤토리를 등록합니다.
        /// </summary>
        public void SetTargetInventory(Inventory target)
        {
            _targetInventory = target;
        }

        /// <summary>
        /// 외부 컨테이너 상호작용 종료 시 대상을 해제합니다.
        /// </summary>
        public void ClearTargetInventory()
        {
            _targetInventory = null;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            if (_panelRoot == null)
            {
                _panelRoot = gameObject;
            }

            // GameObject는 항상 활성화 상태를 유지해야 스크립트와 입력이 꺼지지 않음
            _panelRoot.SetActive(true);

            if (_canvasGroup == null)
            {
                _canvasGroup = _panelRoot.GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                {
                    _canvasGroup = _panelRoot.AddComponent<CanvasGroup>();
                }
            }

            // 인스펙터 미할당 시 최소한의 Fallback (Tag 기반 1회 탐색)
            if (_inventory == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    _inventory = player.GetComponent<Inventory>();
                }
            }

            CreateActions();
        }

        private void Start()
        {
            InitializeSlotsUI();

            if (_startOpen)
            {
                Open();
            }
            else
            {
                Close();
            }
        }

        private void OnEnable()
        {
            if (_enableKeyToggle)
            {
                _toggleAction?.Enable();
                _closeAction?.Enable();
            }

            SubscribeToInventory();
        }

        private void OnDisable()
        {
            PopupManager.Pop(this);
            _toggleAction?.Disable();
            _closeAction?.Disable();

            UnsubscribeFromInventory();
        }

        private void OnDestroy()
        {
            PopupManager.Pop(this);
            _toggleAction?.Dispose();
            _closeAction?.Dispose();
        }

        private void CreateActions()
        {
            _toggleAction = new InputAction(
                name: "ToggleInventory",
                type: InputActionType.Button);
            _toggleAction.AddBinding("<Keyboard>/i");
            _toggleAction.performed += _ => Toggle();

            _closeAction = new InputAction(
                name: "CloseInventory",
                type: InputActionType.Button);
            _closeAction.AddBinding("<Keyboard>/escape");
            _closeAction.performed += _ =>
            {
                if (IsOpen)
                {
                    Close();
                }
            };
        }

        private void SubscribeToInventory()
        {
            if (_inventory == null) return;

            _inventory.OnSlotChanged += HandleSlotChanged;
            _inventory.OnCapacityChanged += HandleCapacityChanged;
        }

        private void UnsubscribeFromInventory()
        {
            if (_inventory == null) return;

            _inventory.OnSlotChanged -= HandleSlotChanged;
            _inventory.OnCapacityChanged -= HandleCapacityChanged;
        }

        private void InitializeSlotsUI()
        {
            if (_inventory == null || _slotContainer == null || _slotPrefab == null) return;

            foreach (Transform child in _slotContainer)
            {
                Destroy(child.gameObject);
            }

            _slotUIList.Clear();

            for (int i = 0; i < _inventory.MaxCapacity; i++)
            {
                var slotUI = Instantiate(_slotPrefab, _slotContainer);
                slotUI.Setup(_inventory, i);
                slotUI.OnSlotClicked += HandleSlotClicked;

                if (i < _inventory.Slots.Count)
                {
                    slotUI.UpdateView(_inventory.Slots[i]);
                }

                _slotUIList.Add(slotUI);
            }
        }

        private void HandleSlotChanged(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < _slotUIList.Count && slotIndex < _inventory.Slots.Count)
            {
                _slotUIList[slotIndex].UpdateView(_inventory.Slots[slotIndex]);
            }
        }

        private void HandleCapacityChanged(int newUnlockedCapacity)
        {
            RefreshAllSlots();
        }

        public void RefreshAllSlots()
        {
            if (_inventory == null) return;

            for (int i = 0; i < _slotUIList.Count && i < _inventory.Slots.Count; i++)
            {
                _slotUIList[i].UpdateView(_inventory.Slots[i]);
            }
        }

        private void HandleSlotClicked(int clickedIndex, PointerEventData eventData)
        {
            if (_inventory == null || clickedIndex < 0 || clickedIndex >= _inventory.Slots.Count) return;

            var clickedSlot = _inventory.Slots[clickedIndex];
            if (clickedSlot.IsLocked || clickedSlot.IsEmpty) return;

            // 좌클릭 입력 처리
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                // 상호작용 중인 외부 인벤토리(상자, 창고 등)가 있다면 더블 클릭 시 아이템 빠른 전송 (Quick Transfer)
                if (_targetInventory != null)
                {
                    if (eventData.clickCount >= 2)
                    {
                        bool success = _inventory.TransferItem(clickedIndex, _targetInventory);
                        if (success)
                        {
                            Debug.Log($"[InventoryUI] 슬롯 {clickedIndex}의 아이템을 대상 인벤토리로 전송했습니다.");
                        }
                        else
                        {
                            Debug.LogWarning("[InventoryUI] 대상 인벤토리가 가득 차서 아이템을 전송할 수 없습니다.");
                        }
                    }

                    return;
                }

                // 외부 컨테이너가 열려있지 않을 때: 향후 아이템 정보 툴팁 표시나 사용(소모품 등) 확장 포인트
                // 아이템 툴팁 / 상세 정보 표시 확장 포인트
            }
        }

        #region Open / Close / Toggle (CanvasGroup 기반)

        public void Open()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }

            PopupManager.Push(this);
            RefreshAllSlots();
        }

        public void Close()
        {
            PopupManager.Pop(this);
            ItemTooltipUI.Instance?.Hide();
            ItemDragHandler.Instance?.CancelDrag();
            ClearTargetInventory();
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }

            if (LootChestUI.Instance != null && LootChestUI.Instance.IsContentsOpen)
            {
                LootChestUI.Instance.CloseChest();
            }
        }

        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        #endregion

        #region Inventory Binding API (DI 지원)

        /// <summary>
        /// 외부 시스템(매니저, UI 컨트롤러 등)에서 명시적으로 인벤토리를 의존성 주입(DI)할 때 사용합니다.
        /// </summary>
        public void SetInventory(Inventory inventory)
        {
            if (_inventory == inventory) return;

            UnsubscribeFromInventory();
            _inventory = inventory;
            SubscribeToInventory();
            InitializeSlotsUI();
            RefreshAllSlots();
        }

        #endregion
    }
}
