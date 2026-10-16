using System;
using System.Collections.Generic;
using Consumable;
using InventorySystem;
using ShotgunAndRun.Core;
using ShotgunAndRun.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PlayerComponent = ShotgunAndRun.Player.Player;
using PlayerConsumableSlot = ShotgunAndRun.Player.ConsumableSlot;

namespace ShotgunAndRun.UI
{
    public enum StorageViewMode
    {
        Material,
        Consumable
    }

    /// <summary>
    /// 쉘터 내부에서 수집한 아이템들을 확인하고 보관하는 창고 UI입니다.
    /// 1. 재료 창고(Material View): 마스터 카탈로그(ItemCatalogSO) 기반의 고정 도감형 슬롯 표시.
    /// 2. 소모품 보관함(Consumable View): 인벤토리/루트 상자 스타일의 10칸 슬롯 그리드 및 퀵슬롯(3칸) 연동 바 지원.
    /// 우측 상단의 전환 버튼([소모품 ->] / [<- 재료 창고])을 통해 자유롭게 뷰를 전환할 수 있습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ShelterStorageUI : MonoBehaviour, IPopupUI
    {
        [Header("Item Catalog Master")]
        [Tooltip("창고에 고정 배치할 아이템 목록이 담긴 마스터 카탈로그 ScriptableObject")]
        [SerializeField] private ItemCatalogSO _itemCatalog;

        [SerializeField] private bool _hideQuestItems = true;

        [Header("UI Panels")]
        [Tooltip("창고 UI의 루트 패널 오브젝트")]
        [SerializeField] private GameObject _storagePanel;

        [Tooltip("고정 슬롯 배경 패널 (미지정 시 SlotBackdrop 자동 탐색)")]
        [SerializeField] private RectTransform _slotBackdrop;

        [Tooltip("고정 슬롯들이 배치될 그리드 컨테이너 (GridLayoutGroup 권장)")]
        [SerializeField] private Transform _slotContainer;

        [Tooltip("생성할 고정 슬롯 프리팹")]
        [SerializeField] private StorageFixedSlotUI _slotPrefab;

        [Header("View Switching & Headers")]
        [Tooltip("창고 타이틀 텍스트 ('창고' ↔ '소모품 보관함')")]
        [SerializeField] private TextMeshProUGUI _titleText;

        [Tooltip("뷰 전환 버튼 (재료 창고 ↔ 소모품 보관함 토글)")]
        [SerializeField] private Button _viewSwitchButton;

        [Tooltip("뷰 전환 버튼 라벨 텍스트")]
        [SerializeField] private TextMeshProUGUI _viewSwitchButtonText;

        [Tooltip("재료 도감 뷰 루트 오브젝트")]
        [SerializeField] private GameObject _materialViewRoot;

        [Tooltip("소모품 보관함 뷰 루트 오브젝트")]
        [SerializeField] private GameObject _consumableViewRoot;

        [Header("Consumable Storage System")]
        [Tooltip("소모품 마스터 카탈로그")]
        [SerializeField] private ConsumableCatalog _consumableCatalog;

        [Tooltip("소모품 슬롯 프리팹 (ConsumableStorageSlotUI)")]
        [SerializeField] private ConsumableStorageSlotUI _consumableSlotPrefab;

        [Tooltip("소모품 창고 슬롯(10칸) 그리드 컨테이너")]
        [SerializeField] private Transform _consumableSlotContainer;

        [Header("Window Controls")]
        [Tooltip("닫기 버튼")]
        [SerializeField] private Button _closeButton;

        [Tooltip("ESC 키로 창고를 닫을 수 있는지 여부")]
        [SerializeField] private bool _closeOnEscape = true;

        private readonly List<StorageFixedSlotUI> _spawnedSlots = new List<StorageFixedSlotUI>();
        private readonly List<ConsumableStorageSlotUI> _storageConsumableSlots = new List<ConsumableStorageSlotUI>();

        private StorageViewMode _currentView = StorageViewMode.Material;
        private bool _isInitialized;
        private bool _isConsumablesInitialized;

        public static ShelterStorageUI Instance { get; private set; }

        /// <summary>
        /// 외부 UI(강화, 제작 등)에서 창고 데이터 변경 시 실시간 갱신을 요청하는 이벤트입니다.
        /// </summary>
        public static event Action StorageUpdated;

        /// <summary>
        /// 창고 데이터가 변경되었음을 알리고 열려 있는 창고 UI의 슬롯 갱신을 요청합니다.
        /// </summary>
        public static void RequestRefresh()
        {
            StorageUpdated?.Invoke();
        }

        public bool IsOpen => _storagePanel != null && _storagePanel.activeSelf;
        public StorageViewMode CurrentView => _currentView;

        public bool HideQuestItems
        {
            get => _hideQuestItems;
            set => _hideQuestItems = value;
        }

        public IReadOnlyList<StorageFixedSlotUI> SpawnedSlots => _spawnedSlots;
        public IReadOnlyList<ConsumableStorageSlotUI> StorageConsumableSlots => _storageConsumableSlots;

        private void Awake()
        {
            Instance = this;
            StorageUpdated += HandleStorageUpdated;

            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(Close);
            }

            EnsureConsumableCatalog();

            if (_viewSwitchButton != null)
            {
                _viewSwitchButton.onClick.AddListener(ToggleView);
            }
        }

        private void Start()
        {
            InitializeSlots();
            InitializeConsumableSlots();

            if (IsOpen)
            {
                SetView(_currentView);
            }
        }

        private void OnDisable()
        {
            PopupManager.Pop(this);
            ItemTooltipUI.Instance?.Hide();
        }

        private void OnDestroy()
        {
            PopupManager.Pop(this);
            StorageUpdated -= HandleStorageUpdated;

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(Close);
            }

            if (_viewSwitchButton != null)
            {
                _viewSwitchButton.onClick.RemoveListener(ToggleView);
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void HandleStorageUpdated()
        {
            if (IsOpen)
            {
                if (_currentView == StorageViewMode.Material)
                {
                    RefreshStorage();
                }
                else
                {
                    RefreshConsumableStorage();
                }

                Debug.Log("[ShelterStorageUI] 외부 갱신 요청을 수신하여 창고 슬롯을 실시간으로 갱신했습니다.");
            }
        }

        /// <summary>
        /// 재료 창고와 소모품 보관함 간 뷰를 토글합니다.
        /// </summary>
        public void ToggleView()
        {
            SetView(_currentView == StorageViewMode.Material ? StorageViewMode.Consumable : StorageViewMode.Material);
        }

        /// <summary>
        /// 지정한 뷰 모드로 전환하고 해당 뷰의 슬롯들을 갱신합니다.
        /// </summary>
        public void SetView(StorageViewMode mode)
        {
            _currentView = mode;

            bool isMaterial = _currentView == StorageViewMode.Material;

            if (_materialViewRoot != null)
            {
                _materialViewRoot.SetActive(isMaterial);
            }
            else
            {
                if (_slotBackdrop != null) _slotBackdrop.gameObject.SetActive(isMaterial);
                if (_slotContainer != null) _slotContainer.gameObject.SetActive(isMaterial);
            }

            if (_consumableViewRoot != null)
            {
                _consumableViewRoot.SetActive(!isMaterial);
            }

            if (_titleText != null)
            {
                _titleText.text = isMaterial ? "창고" : "소모품 보관함";
            }

            if (_viewSwitchButtonText != null)
            {
                _viewSwitchButtonText.text = isMaterial ? "소모품 ->" : "<- 재료";
            }

            if (isMaterial)
            {
                RefreshStorage();
            }
            else
            {
                RefreshConsumableStorage();
            }

            AdjustLayoutSize();
        }

        #region Hierarchy & Catalog Safe Guard

        private void EnsureConsumableCatalog()
        {
            if (_consumableCatalog == null)
            {
#if UNITY_EDITOR
                _consumableCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<ConsumableCatalog>("Assets/07.Datas/ConsumableCatalog.asset");
#endif
                if (_consumableCatalog == null)
                {
                    _consumableCatalog = Resources.Load<ConsumableCatalog>("ConsumableCatalog");
                }
            }
        }

        #endregion

        #region Slot Initialization & Layout

        /// <summary>
        /// 카탈로그의 아이템들을 바탕으로 고정 슬롯들을 1회 생성합니다.
        /// </summary>
        public void InitializeSlots()
        {
            if (_isInitialized)
            {
                return;
            }

            if (_itemCatalog == null || _slotContainer == null || _slotPrefab == null)
            {
                return;
            }

            // 기존 자식 오브젝트 정리
            for (int i = _slotContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(_slotContainer.GetChild(i).gameObject);
            }
            _spawnedSlots.Clear();

            IReadOnlyList<ItemData> items = _itemCatalog.Items;
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    ItemData item = items[i];
                    if (item == null)
                    {
                        continue;
                    }

                    if (_hideQuestItems && item.ItemType == ItemType.Quest)
                    {
                        continue;
                    }

                    StorageFixedSlotUI slot = Instantiate(_slotPrefab, _slotContainer);
                    slot.Bind(item);
                    _spawnedSlots.Add(slot);
                }
            }

            AdjustLayoutSize();
            _isInitialized = true;
        }

        /// <summary>
        /// 소모품 창고(10칸) 슬롯들을 1회 생성하고 이벤트를 바인딩합니다.
        /// </summary>
        public void InitializeConsumableSlots()
        {
            if (_isConsumablesInitialized)
            {
                return;
            }

            if (_consumableSlotPrefab == null || _consumableSlotContainer == null)
            {
                return;
            }

            for (int i = _consumableSlotContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(_consumableSlotContainer.GetChild(i).gameObject);
            }
            _storageConsumableSlots.Clear();

            for (int i = 0; i < ProgressData.DefaultConsumableStorageSlotCount; i++)
            {
                ConsumableStorageSlotUI slot = Instantiate(_consumableSlotPrefab, _consumableSlotContainer);
                slot.name = $"StorageSlot_{i}";
                slot.Setup(ConsumableSlotType.Storage, i, HandleConsumableSlotClicked, HandleConsumableSlotDropped);
                _storageConsumableSlots.Add(slot);
            }

            _isConsumablesInitialized = true;
        }

        /// <summary>
        /// 현재 활성화된 뷰(재료/소모품)에 맞춰 패널 높이를 동적으로 조절합니다.
        /// </summary>
        public void AdjustLayoutSize()
        {
            if (_storagePanel == null)
            {
                return;
            }

            RectTransform panelRect = _storagePanel.transform as RectTransform;
            if (panelRect == null)
            {
                return;
            }

            float panelHeight;

            if (_currentView == StorageViewMode.Consumable)
            {
                // 헤더(80) + 창고 2행(184) + 여백(20) = 284px (재료 창고 기본 규격과 일치)
                panelHeight = 284f;
            }
            else
            {
                if (_slotBackdrop == null)
                {
                    _slotBackdrop = _storagePanel.transform.Find("SlotBackdrop") as RectTransform;
                }

                int count = _spawnedSlots.Count;
                int columns = 5;
                float cellHeight = 88f;
                float spacingY = 8f;

                if (_slotContainer != null)
                {
                    GridLayoutGroup grid = _slotContainer.GetComponent<GridLayoutGroup>();
                    if (grid != null)
                    {
                        if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount && grid.constraintCount > 0)
                        {
                            columns = grid.constraintCount;
                        }
                        cellHeight = grid.cellSize.y;
                        spacingY = grid.spacing.y;
                    }
                }

                int rows = Mathf.Max(1, Mathf.CeilToInt((float)count / columns));
                float backdropHeight = (rows * cellHeight) + ((rows - 1) * spacingY);
                panelHeight = 80f + backdropHeight + 20f;

                if (_slotBackdrop != null)
                {
                    Vector2 backdropSize = _slotBackdrop.sizeDelta;
                    backdropSize.y = backdropHeight;
                    _slotBackdrop.sizeDelta = backdropSize;
                }
            }

            float currentTop = panelRect.anchoredPosition.y + (panelRect.sizeDelta.y * (1f - panelRect.pivot.y));
            panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, panelHeight);
            panelRect.anchoredPosition = new Vector2(
                panelRect.anchoredPosition.x,
                currentTop - (panelHeight * (1f - panelRect.pivot.y)));
        }

        #endregion

        #region Public Window Controls

        /// <summary>
        /// 창고 창을 열고 최신 데이터로 슬롯 상태를 갱신합니다.
        /// </summary>
        public void Open()
        {
            InitializeSlots();
            InitializeConsumableSlots();

            if (_storagePanel != null)
            {
                _storagePanel.SetActive(true);
                if (_closeOnEscape)
                {
                    PopupManager.Push(this);
                }
            }

            SetView(_currentView);
        }

        /// <summary>
        /// 창고 창을 닫습니다.
        /// </summary>
        public void Close()
        {
            PopupManager.Pop(this);
            ItemTooltipUI.Instance?.Hide();
            ItemDragHandler.Instance?.CancelDrag();

            if (_storagePanel != null)
            {
                _storagePanel.SetActive(false);
            }

            if (WeaponUpgradeUI.Instance != null && WeaponUpgradeUI.Instance.IsOpen)
            {
                WeaponUpgradeUI.Instance.Close();
            }

            if (ConsumableCraftingUI.Instance != null && ConsumableCraftingUI.Instance.IsOpen)
            {
                ConsumableCraftingUI.Instance.Close();
            }
        }

        /// <summary>
        /// 창고 창 열림/닫힘 상태를 전환합니다.
        /// </summary>
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

        #region Refresh Data Logic

        /// <summary>
        /// GameManager.Progress.Storage의 실제 보유 수량을 조회하여 모든 고정 슬롯의 뷰를 갱신합니다.
        /// </summary>
        public void RefreshStorage()
        {
            ProgressData progress = GameManager.Instance != null ? GameManager.Instance.Progress : null;

            for (int i = 0; i < _spawnedSlots.Count; i++)
            {
                StorageFixedSlotUI slot = _spawnedSlots[i];
                if (slot == null || slot.AssignedItem == null)
                {
                    continue;
                }

                int storedAmount = progress != null ? progress.GetStoredAmount(slot.AssignedItem.Id) : 0;
                slot.UpdateView(storedAmount);
            }
        }

        /// <summary>
        /// 소모품 창고 데이터(Progress.ConsumableStorage)를 조회하여 10개 창고 슬롯 뷰를 갱신합니다.
        /// </summary>
        public void RefreshConsumableStorage()
        {
            InitializeConsumableSlots();

            ProgressData progress = GameManager.Instance != null ? GameManager.Instance.Progress : null;
            if (progress != null)
            {
                progress.EnsureConsumableStorageCapacity();
            }

            for (int i = 0; i < _storageConsumableSlots.Count; i++)
            {
                ConsumableStorageSlotUI slotUI = _storageConsumableSlots[i];
                if (slotUI == null) continue;

                if (progress != null && i < progress.ConsumableStorage.Count)
                {
                    QuickSlotRecord record = progress.ConsumableStorage[i];
                    ConsumableData data = FindConsumableData(record.ConsumableTypeName);
                    slotUI.UpdateView(data, record.Amount);
                }
                else
                {
                    slotUI.UpdateView(null, 0);
                }
            }
        }

        public ConsumableData FindConsumableData(string nameOrType)
        {
            if (string.IsNullOrEmpty(nameOrType)) return null;

            EnsureConsumableCatalog();
            return _consumableCatalog != null ? _consumableCatalog.FindConsumableDataByName(nameOrType) : null;
        }

        public PlayerConsumableInventory GetPlayerConsumableInventory()
        {
            if (PlayerComponent.PlayerInstance != null && PlayerComponent.PlayerInstance.ConsumableInventory != null)
            {
                return PlayerComponent.PlayerInstance.ConsumableInventory;
            }

            return FindAnyObjectByType<PlayerConsumableInventory>();
        }

        #endregion

        #region Public Real QuickSlot Interaction API

        /// <summary>
        /// 실제 화면 퀵슬롯의 아이템을 창고 보관함으로 입고합니다.
        /// targetStorageSlotIndex가 -1이면 빈 슬롯이나 동일 소모품 슬롯으로 자동 입고됩니다.
        /// </summary>
        public bool TryDepositFromQuickSlot(int quickSlotIndex, int targetStorageSlotIndex = -1)
        {
            ProgressData progress = GameManager.Instance != null ? GameManager.Instance.Progress : null;
            PlayerConsumableInventory playerInv = GetPlayerConsumableInventory();
            if (progress == null || playerInv == null || quickSlotIndex < 0 || quickSlotIndex >= playerInv.SlotCount)
            {
                return false;
            }

            PlayerConsumableSlot qSlot = playerInv.GetSlot(quickSlotIndex);
            if (qSlot == null || qSlot.IsEmpty || qSlot.Data == null || qSlot.Amount <= 0)
            {
                return false;
            }

            progress.EnsureConsumableStorageCapacity();

            // 특정 창고 슬롯으로 드롭된 경우
            if (targetStorageSlotIndex >= 0 && targetStorageSlotIndex < progress.ConsumableStorage.Count)
            {
                QuickSlotRecord tRec = progress.ConsumableStorage[targetStorageSlotIndex];
                ConsumableData tData = FindConsumableData(tRec.ConsumableTypeName);

                if (string.IsNullOrEmpty(tRec.ConsumableTypeName) || tRec.Amount <= 0)
                {
                    progress.ConsumableStorage[targetStorageSlotIndex] = new QuickSlotRecord(qSlot.Data.ConsumableType.ToString(), qSlot.Amount);
                    playerInv.SetSlot(quickSlotIndex, null, 0);
                }
                else if (tData == qSlot.Data)
                {
                    int space = qSlot.Data.MaxStack - tRec.Amount;
                    int add = Mathf.Min(space, qSlot.Amount);
                    tRec.Amount += add;
                    progress.ConsumableStorage[targetStorageSlotIndex] = tRec;
                    int remaining = qSlot.Amount - add;
                    playerInv.SetSlot(quickSlotIndex, remaining > 0 ? qSlot.Data : null, remaining);
                }
                else
                {
                    // 맞교환 (Swap)
                    progress.ConsumableStorage[targetStorageSlotIndex] = new QuickSlotRecord(qSlot.Data.ConsumableType.ToString(), qSlot.Amount);
                    playerInv.SetSlot(quickSlotIndex, tData, tRec.Amount);
                }
            }
            else
            {
                // 원클릭 입고: 빈 슬롯이나 동일 소모품 슬롯으로 자동 이동
                if (!TryTransferToStorage(progress, qSlot.Data, qSlot.Amount, out int transferred))
                {
                    return false;
                }

                int remaining = qSlot.Amount - transferred;
                playerInv.SetSlot(quickSlotIndex, remaining > 0 ? qSlot.Data : null, remaining);
            }

            GameManager.Instance?.SaveProgress();
            RefreshConsumableStorage();
            return true;
        }

        /// <summary>
        /// 창고 보관함의 소모품을 실제 퀵슬롯으로 인출합니다.
        /// targetQuickSlotIndex가 -1이면 빈 퀵슬롯이나 동일 소모품 퀵슬롯으로 자동 인출됩니다.
        /// </summary>
        public bool TryWithdrawToQuickSlot(int storageSlotIndex, int targetQuickSlotIndex = -1)
        {
            ProgressData progress = GameManager.Instance != null ? GameManager.Instance.Progress : null;
            PlayerConsumableInventory playerInv = GetPlayerConsumableInventory();
            if (progress == null || playerInv == null || storageSlotIndex < 0 || storageSlotIndex >= progress.ConsumableStorage.Count)
            {
                return false;
            }

            QuickSlotRecord sRec = progress.ConsumableStorage[storageSlotIndex];
            ConsumableData sData = FindConsumableData(sRec.ConsumableTypeName);
            if (sData == null || sRec.Amount <= 0)
            {
                return false;
            }

            // 특정 퀵슬롯으로 드롭된 경우
            if (targetQuickSlotIndex >= 0 && targetQuickSlotIndex < playerInv.SlotCount)
            {
                PlayerConsumableSlot tSlot = playerInv.GetSlot(targetQuickSlotIndex);
                if (tSlot == null || tSlot.IsEmpty)
                {
                    playerInv.SetSlot(targetQuickSlotIndex, sData, sRec.Amount);
                    progress.ConsumableStorage[storageSlotIndex] = new QuickSlotRecord(string.Empty, 0);
                }
                else if (tSlot.Data == sData)
                {
                    int space = sData.MaxStack - tSlot.Amount;
                    int add = Mathf.Min(space, sRec.Amount);
                    tSlot.AddAmount(add);
                    sRec.Amount -= add;
                    playerInv.SetSlot(targetQuickSlotIndex, tSlot.Data, tSlot.Amount);
                    progress.ConsumableStorage[storageSlotIndex] = sRec.Amount > 0 ? sRec : new QuickSlotRecord(string.Empty, 0);
                }
                else
                {
                    // 맞교환 (Swap)
                    ConsumableData tData = tSlot.Data;
                    int tAmount = tSlot.Amount;
                    playerInv.SetSlot(targetQuickSlotIndex, sData, sRec.Amount);
                    progress.ConsumableStorage[storageSlotIndex] = new QuickSlotRecord(tData.ConsumableType.ToString(), tAmount);
                }
            }
            else
            {
                // 원클릭 인출: 빈 퀵슬롯이나 동일 소모품 퀵슬롯으로 자동 이동
                if (!TryTransferToQuickSlot(playerInv, sData, sRec.Amount, out int transferred))
                {
                    return false;
                }

                int remaining = sRec.Amount - transferred;
                progress.ConsumableStorage[storageSlotIndex] = remaining > 0
                    ? new QuickSlotRecord(sData.ConsumableType.ToString(), remaining)
                    : new QuickSlotRecord(string.Empty, 0);
            }

            GameManager.Instance?.SaveProgress();
            RefreshConsumableStorage();
            return true;
        }

        #endregion

        #region Consumable Interactions (Click & Drag-Drop)

        private void HandleConsumableSlotClicked(ConsumableStorageSlotUI slot, PointerEventData eventData)
        {
            if (slot == null || slot.IsEmpty) return;
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
            if (eventData.clickCount < 2) return;

            // 창고 슬롯 좌클릭 더블 클릭 -> 실제 퀵슬롯으로 인출
            TryWithdrawToQuickSlot(slot.SlotIndex);
        }

        private bool TryTransferToQuickSlot(PlayerConsumableInventory playerInv, ConsumableData data, int amount, out int transferred)
        {
            transferred = 0;
            if (playerInv == null || data == null || amount <= 0) return false;

            // 1. 기존 동일 아이템 슬롯에 누적
            for (int i = 0; i < playerInv.SlotCount && amount > 0; i++)
            {
                PlayerConsumableSlot qSlot = playerInv.GetSlot(i);
                if (qSlot != null && !qSlot.IsEmpty && qSlot.Data == data)
                {
                    int space = data.MaxStack - qSlot.Amount;
                    if (space > 0)
                    {
                        int add = Mathf.Min(space, amount);
                        playerInv.SetSlot(i, data, qSlot.Amount + add);
                        amount -= add;
                        transferred += add;
                    }
                }
            }

            // 2. 비어있는 슬롯에 배치
            for (int i = 0; i < playerInv.SlotCount && amount > 0; i++)
            {
                PlayerConsumableSlot qSlot = playerInv.GetSlot(i);
                if (qSlot != null && qSlot.IsEmpty)
                {
                    int add = Mathf.Min(data.MaxStack, amount);
                    playerInv.SetSlot(i, data, add);
                    amount -= add;
                    transferred += add;
                }
            }

            return transferred > 0;
        }

        private bool TryTransferToStorage(ProgressData progress, ConsumableData data, int amount, out int transferred)
        {
            transferred = 0;
            if (progress == null || data == null || amount <= 0) return false;

            progress.EnsureConsumableStorageCapacity();

            // 1. 기존 동일 아이템 슬롯에 누적
            for (int i = 0; i < progress.ConsumableStorage.Count && amount > 0; i++)
            {
                QuickSlotRecord record = progress.ConsumableStorage[i];
                if (!string.IsNullOrEmpty(record.ConsumableTypeName) &&
                    string.Equals(record.ConsumableTypeName, data.ConsumableType.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    int space = data.MaxStack - record.Amount;
                    if (space > 0)
                    {
                        int add = Mathf.Min(space, amount);
                        record.Amount += add;
                        progress.ConsumableStorage[i] = record;
                        amount -= add;
                        transferred += add;
                    }
                }
            }

            // 2. 비어있는 슬롯에 배치
            for (int i = 0; i < progress.ConsumableStorage.Count && amount > 0; i++)
            {
                QuickSlotRecord record = progress.ConsumableStorage[i];
                if (string.IsNullOrEmpty(record.ConsumableTypeName) || record.Amount <= 0)
                {
                    int add = Mathf.Min(data.MaxStack, amount);
                    progress.ConsumableStorage[i] = new QuickSlotRecord(data.ConsumableType.ToString(), add);
                    amount -= add;
                    transferred += add;
                }
            }

            return transferred > 0;
        }

        private void HandleConsumableSlotDropped(ConsumableStorageSlotUI source, ConsumableStorageSlotUI target)
        {
            if (source == null || target == null || source == target || source.IsEmpty) return;

            ProgressData progress = GameManager.Instance != null ? GameManager.Instance.Progress : null;
            if (progress == null) return;
            progress.EnsureConsumableStorageCapacity();

            QuickSlotRecord sRec = progress.ConsumableStorage[source.SlotIndex];
            QuickSlotRecord tRec = progress.ConsumableStorage[target.SlotIndex];

            if (target.IsEmpty)
            {
                progress.ConsumableStorage[target.SlotIndex] = sRec;
                progress.ConsumableStorage[source.SlotIndex] = new QuickSlotRecord(string.Empty, 0);
            }
            else if (source.CurrentData == target.CurrentData)
            {
                int space = target.CurrentData.MaxStack - tRec.Amount;
                int add = Mathf.Min(space, sRec.Amount);
                tRec.Amount += add;
                sRec.Amount -= add;

                progress.ConsumableStorage[target.SlotIndex] = tRec;
                progress.ConsumableStorage[source.SlotIndex] = sRec.Amount > 0 ? sRec : new QuickSlotRecord(string.Empty, 0);
            }
            else
            {
                // 맞교환 (Swap)
                progress.ConsumableStorage[target.SlotIndex] = sRec;
                progress.ConsumableStorage[source.SlotIndex] = tRec;
            }

            GameManager.Instance?.SaveProgress();
            RefreshConsumableStorage();
        }

        #endregion
    }
}
