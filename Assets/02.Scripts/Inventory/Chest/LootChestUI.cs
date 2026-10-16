using System.Collections.Generic;
using ShotgunAndRun.Core;
using ShotgunAndRun.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace InventorySystem
{
    /// <summary>
    /// 상자 상호작용 진행도(루팅 게이지) 및 상자 내용물 팝업을 표시하는 전용 UI 컴포넌트
    /// </summary>
    public class LootChestUI : MonoBehaviour, IPopupUI
    {
        public static LootChestUI Instance
        {
            get;
            private set;
        }

        [Header("Player Inventory Reference")]
        [SerializeField] private Inventory _playerInventory;

        [Header("Chest Contents Window (상자 내용물 창)")]
        [SerializeField] private GameObject _contentsPanel;
        [SerializeField] private Transform _slotContainer;
        [SerializeField] private SlotUI _slotPrefab;
        [SerializeField] private Button _takeAllButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private AudioClip _openSfxClip;
        [SerializeField] private AudioClip _closeSfxClip;
        [SerializeField] private AudioSource _audioSource;

        private readonly List<SlotUI> _chestSlotUIList = new();
        private LootChest _currentChest;

        public bool IsContentsOpen => _contentsPanel != null && _contentsPanel.activeSelf;
        bool IPopupUI.IsOpen => IsContentsOpen;
        void IPopupUI.Close() => CloseChest();
        public LootChest CurrentChest => _currentChest;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>();
            }

            EnsurePlayerInventory();

            if (_contentsPanel != null) _contentsPanel.SetActive(false);

            if (_takeAllButton != null)
            {
                _takeAllButton.onClick.AddListener(OnTakeAllClicked);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(CloseChest);
            }
        }

        private void OnDisable()
        {
            PopupManager.Pop(this);
            if (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.Stop();
            }
        }

        private void OnDestroy()
        {
            PopupManager.Pop(this);
        }

        private void EnsurePlayerInventory()
        {
            if (_playerInventory != null) return;

            // 1. Player.PlayerInstance 싱글톤에서 직접 O(1) 캐싱
            if (ShotgunAndRun.Player.Player.PlayerInstance != null)
            {
                _playerInventory = ShotgunAndRun.Player.Player.PlayerInstance.GetComponent<Inventory>();
                if (_playerInventory != null) return;
            }

            // 2. 이미 초기화된 InventoryUI 인스턴스에서 O(1) 참조
            if (InventoryUI.Instance != null && InventoryUI.Instance.PlayerInventory != null)
            {
                _playerInventory = InventoryUI.Instance.PlayerInventory;
                if (_playerInventory != null) return;
            }

            // 3. Fallback: Tag 기반 1회 탐색
            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                _playerInventory = playerObj.GetComponent<Inventory>();
            }
        }

        private void Update()
        {
            if (!IsContentsOpen || Keyboard.current == null)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;

            // ESC 키 입력 시 상자 창 닫기
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                CloseChest();
                return;
            }

            // 상자 내용물 창이 열린 동안 E 키를 누르면 '모두 획득'과 동일하게 처리합니다.
            if (keyboard.eKey.wasPressedThisFrame)
            {
                OnTakeAllClicked();
            }
        }

        #region Chest Window API

        public void OpenChest(LootChest chest)
        {
            if (chest == null || chest.ChestInventory == null) return;

            EnsurePlayerInventory();

            if (_currentChest != null && _currentChest.ChestInventory != null)
            {
                _currentChest.ChestInventory.OnSlotChanged -= HandleChestSlotChanged;
            }

            _currentChest = chest;

            if (_contentsPanel != null)
            {
                _contentsPanel.SetActive(true);
                PopupManager.Push(this);
            }

            PlayChestSfx(_openSfxClip);

            // 상자 열람 시 플레이어 인벤토리에 대상 컨테이너로 등록하고 창을 엽니다.
            if (InventoryUI.Instance != null)
            {
                InventoryUI.Instance.SetTargetInventory(_currentChest.ChestInventory);
                if (!InventoryUI.Instance.IsOpen)
                {
                    InventoryUI.Instance.Open();
                }
            }

            // 상자 인벤토리 이벤트 구독
            _currentChest.ChestInventory.OnSlotChanged += HandleChestSlotChanged;

            InitializeChestSlotsUI();
        }

        public void CloseChest()
        {
            bool wasOpen = IsContentsOpen;

            PopupManager.Pop(this);
            ItemTooltipUI.Instance?.Hide();
            ItemDragHandler.Instance?.CancelDrag();

            if (_currentChest != null && _currentChest.ChestInventory != null)
            {
                _currentChest.ChestInventory.OnSlotChanged -= HandleChestSlotChanged;
            }

            _currentChest = null;

            if (_contentsPanel != null)
            {
                _contentsPanel.SetActive(false);
            }

            if (wasOpen)
            {
                PlayChestSfx(_closeSfxClip);
            }

            // 인벤토리 창의 대상 컨테이너 등록 해제 및 함께 열렸던 인벤토리 창 닫기
            if (InventoryUI.Instance != null)
            {
                InventoryUI.Instance.ClearTargetInventory();
                if (InventoryUI.Instance.IsOpen)
                {
                    InventoryUI.Instance.Close();
                }
            }
        }

        private void InitializeChestSlotsUI()
        {
            if (_currentChest == null || _slotContainer == null || _slotPrefab == null) return;

            foreach (Transform child in _slotContainer)
            {
                Destroy(child.gameObject);
            }

            _chestSlotUIList.Clear();

            var slots = _currentChest.ChestInventory.Slots;
            for (int i = 0; i < _currentChest.ChestInventory.UnlockedCapacity; i++)
            {
                var slotUI = Instantiate(_slotPrefab, _slotContainer);
                slotUI.Setup(_currentChest.ChestInventory, i);
                slotUI.OnSlotClicked += HandleSlotClicked;
                slotUI.UpdateView(slots[i]);

                _chestSlotUIList.Add(slotUI);
            }
        }

        private void HandleChestSlotChanged(int slotIndex)
        {
            if (_currentChest == null) return;

            if (slotIndex >= 0 && slotIndex < _chestSlotUIList.Count &&
                slotIndex < _currentChest.ChestInventory.Slots.Count)
            {
                _chestSlotUIList[slotIndex].UpdateView(_currentChest.ChestInventory.Slots[slotIndex]);
            }
        }

        /// <summary>
        /// 상자 슬롯 더블 클릭 시 플레이어 인벤토리로 해당 아이템 이동
        /// </summary>
        private void HandleSlotClicked(int slotIndex, PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (eventData.clickCount < 2) return;
            if (_currentChest == null || _playerInventory == null) return;

            bool success = _currentChest.ChestInventory.TransferItem(slotIndex, _playerInventory);
            if (success)
            {
                Debug.Log($"[LootChestUI] 상자 슬롯 {slotIndex}의 아이템을 플레이어 인벤토리로 획득했습니다.");

                if (_currentChest.ChestInventory.IsEmpty())
                {
                    Debug.Log("[LootChestUI] 상자가 모두 비었습니다.");
                }
            }
            else
            {
                Debug.LogWarning("[LootChestUI] 플레이어 인벤토리가 가득 차서 아이템을 가져올 수 없습니다.");
            }
        }

        /// <summary>
        /// '모두 획득' 버튼 클릭 핸들러
        /// </summary>
        private void OnTakeAllClicked()
        {
            if (_currentChest == null || _playerInventory == null) return;

            int transferredCount = _currentChest.ChestInventory.TransferAll(_playerInventory);
            Debug.Log($"[LootChestUI] {transferredCount}개 슬롯의 아이템을 모두 획득했습니다.");

            if (_currentChest.ChestInventory.IsEmpty())
            {
                Debug.Log("[LootChestUI] 상자가 모두 비었습니다.");
            }
        }

        #endregion

        #region Audio Control

        private void PlayChestSfx(AudioClip clip)
        {
            if (clip == null || _audioSource == null) return;

            if (_audioSource.outputAudioMixerGroup == null && AudioManager.Instance != null)
            {
                _audioSource.outputAudioMixerGroup = AudioManager.Instance.UiGroup;
            }

            _audioSource.PlayOneShot(clip);
        }

        #endregion
    }
}
