using System.Collections;
using Consumable;
using ShotgunAndRun.Player;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace ShotgunAndRun.UI
{
    /// <summary>
    /// 플레이어의 소모품 인벤토리(PlayerConsumableInventory)를 직접 구독하여
    /// 쉘터 제작 시스템에서 지급된 소모품을 퀵슬롯 UI에 실시간 표시하고 상호작용하는 UI 컴포넌트입니다.
    /// ConsumableSelectionController가 존재할 경우 선택/토글 하이라이트도 함께 연동합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ConsumableQuickSlotUI : MonoBehaviour
    {
        [Header("Target Inventory (비워둘 시 자동 탐색)")]
        [Tooltip("제작 시스템과 연결된 플레이어 소모품 인벤토리")]
        [SerializeField]
        private PlayerConsumableInventory _inventory;

        [Tooltip("소모품 선택 컨트롤러 (선택 하이라이트 연동용, 선택 사항)")]
        [SerializeField]
        private ConsumableSelectionController _selectionController;

        [Header("Slot UI Items (1, 2, 3번 슬롯 순서대로 연결)")]
        [SerializeField] private ConsumableQuickSlotItemUI[] _slotItems;

        [Header("Settings")]
        [Tooltip("슬롯 UI 클릭 시 즉시 사용할지 여부 (false면 선택/토글 모드)")]
        [SerializeField]
        private bool _useImmediatelyOnClick = false;

        [Tooltip("이미 선택된 슬롯을 다시 클릭하면 선택을 해제(기본 무기 모드로 복귀)할지 여부")]
        [SerializeField]
        private bool _toggleDeselectOnReclick = true;

        [Tooltip("UI 자체에서 숫자키(1, 2, 3) 입력을 처리할지 여부")]
        [SerializeField]
        private bool _enableHotkeys = true;

        [System.NonSerialized] private bool _isSubscribed = false;

        private void Awake()
        {
            ResolveDependencies();
            InitializeSlots();
        }

        private void OnEnable()
        {
            StartCoroutine(WaitAndSubscribeRoutine());
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void Update()
        {
            if (!_enableHotkeys || Mathf.Approximately(Time.timeScale, 0f))
            {
                return;
            }

            CheckHotkeyInput();
        }

        /// <summary>
        /// 인스펙터에 할당되지 않은 컴포넌트를 플레이어 인스턴스로부터 탐색하여 캐싱합니다.
        /// </summary>
        public bool ResolveDependencies()
        {
            if (_inventory != null && _selectionController != null)
            {
                return true;
            }

            var player = Player.Player.PlayerInstance;
            if (player != null)
            {
                _inventory ??= player.ConsumableInventory ?? player.GetComponent<PlayerConsumableInventory>();
                _selectionController ??= player.GetComponentInChildren<ConsumableSelectionController>();
                return _inventory != null && _selectionController != null;
            }

            return false;
        }

        private void InitializeSlots()
        {
            if (_slotItems == null || _slotItems.Length == 0)
            {
                _slotItems = GetComponentsInChildren<ConsumableQuickSlotItemUI>(true);
            }

            for (int i = 0; i < _slotItems.Length; i++)
            {
                if (_slotItems[i] != null)
                {
                    _slotItems[i].Initialize(i, HandleSlotClicked, (i + 1).ToString());
                }
            }
        }

        private IEnumerator WaitAndSubscribeRoutine()
        {
            if (ResolveDependencies())
            {
                SubscribeEvents();
                RefreshAllSlots();
                yield break;
            }

            // 씬 로드 직후 플레이어 인스턴스가 셋업될 때까지 대기
            float timeout = 2f;
            while (!ResolveDependencies() && timeout > 0f)
            {
                yield return null;
                timeout -= Time.deltaTime;
            }

            if (ResolveDependencies())
            {
                SubscribeEvents();
                RefreshAllSlots();
            }
        }

        private void SubscribeEvents()
        {
            if (_inventory == null || _selectionController == null)
            {
                return;
            }

            if (_inventory != null)
            {
                _inventory.SlotChanged -= HandleInventorySlotChanged;
                _inventory.SlotChanged += HandleInventorySlotChanged;
            }

            if (_selectionController != null)
            {
                _selectionController.OnSelectedConsumable -= HandleSelectionChanged;
                _selectionController.OnSelectedConsumable += HandleSelectionChanged;
                _selectionController.OnDeselectedConsumable -= HandleDeselected;
                _selectionController.OnDeselectedConsumable += HandleDeselected;
            }

            _isSubscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (_inventory != null)
            {
                _inventory.SlotChanged -= HandleInventorySlotChanged;
            }

            if (_selectionController != null)
            {
                _selectionController.OnSelectedConsumable -= HandleSelectionChanged;
                _selectionController.OnDeselectedConsumable -= HandleDeselected;
            }

            _isSubscribed = false;
        }

        private void HandleInventorySlotChanged(int slotIndex, ShotgunAndRun.Player.ConsumableSlot slot)
        {
            RefreshAllSlots();
        }

        private void HandleSelectionChanged(ConsumableData data)
        {
            RefreshAllSlots();
        }

        private void HandleDeselected()
        {
            RefreshAllSlots();
        }

        /// <summary>
        /// 모든 슬롯의 소모품 아이콘, 수량, 하이라이트 상태를 새로고침합니다.
        /// </summary>
        public void RefreshAllSlots()
        {
            if (_inventory == null || _slotItems == null)
            {
                return;
            }

            var slots = _inventory.Slots;
            int selectedIndex = GetCurrentSelectedIndex();

            for (int i = 0; i < _slotItems.Length; i++)
            {
                if (_slotItems[i] == null)
                {
                    continue;
                }

                if (slots != null && i < slots.Count && slots[i] != null && !slots[i].IsEmpty)
                {
                    var slot = slots[i];
                    _slotItems[i].UpdateData(slot.Data, slot.Amount);
                }
                else
                {
                    _slotItems[i].SetEmpty();
                }

                bool isSelected = (selectedIndex == i);
                _slotItems[i].SetSelected(isSelected);
            }
        }

        private int GetCurrentSelectedIndex()
        {
            if (_selectionController == null || _selectionController.SelectedConsumable == null)
            {
                return -1;
            }

            return _selectionController.SelectedIndex;
        }

        /// <summary>
        /// 슬롯 클릭 이벤트 핸들러
        /// </summary>
        private void HandleSlotClicked(int index)
        {
            if (_inventory == null || _selectionController == null)
            {
                return;
            }

            // 1. 이미 선택된 슬롯을 다시 클릭/입력한 경우, 슬롯이 비어있더라도 해제 허용
            int currentSelected = GetCurrentSelectedIndex();
            if (_toggleDeselectOnReclick && currentSelected == index)
            {
                _selectionController.DeselectedConsumable();
                return;
            }

            var slot = _inventory.GetSlot(index);
            if (slot == null || slot.IsEmpty)
            {
                return;
            }

            // 2. 즉시 사용 모드
            if (_useImmediatelyOnClick)
            {
                _inventory.TryUseConsumable(index);
                return;
            }

            // 3. 선택 모드
            _selectionController.ChangeSelectedConsumable(index);
        }

        private void CheckHotkeyInput()
        {
            for (int i = 0; i < 3; i++)
            {
                if (IsDigitPressed(i + 1))
                {
                    HandleSlotClicked(i);
                    break;
                }
            }
        }

        private bool IsDigitPressed(int numberKey)
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                KeyControl digitKey = numberKey switch
                {
                    1 => Keyboard.current.digit1Key,
                    2 => Keyboard.current.digit2Key,
                    3 => Keyboard.current.digit3Key,
                    _ => null
                };

                KeyControl keypadKey = numberKey switch
                {
                    1 => Keyboard.current.numpad1Key,
                    2 => Keyboard.current.numpad2Key,
                    3 => Keyboard.current.numpad3Key,
                    _ => null
                };

                if ((digitKey != null && digitKey.wasPressedThisFrame) ||
                    (keypadKey != null && keypadKey.wasPressedThisFrame))
                {
                    return true;
                }
            }
            // 입력 처리가 Both일 때 구 Input Manager까지 보면 같은 키가 다음 프레임에 한 번 더 눌린 것으로 잡혀 선택이 바로 해제됩니다.
#elif ENABLE_LEGACY_INPUT_MANAGER
            KeyCode legacyKey = numberKey switch
            {
                1 => KeyCode.Alpha1,
                2 => KeyCode.Alpha2,
                3 => KeyCode.Alpha3,
                _ => KeyCode.None
            };

            KeyCode legacyKeypadKey = numberKey switch
            {
                1 => KeyCode.Keypad1,
                2 => KeyCode.Keypad2,
                3 => KeyCode.Keypad3,
                _ => KeyCode.None
            };

            if ((legacyKey != KeyCode.None && Input.GetKeyDown(legacyKey)) ||
                (legacyKeypadKey != KeyCode.None && Input.GetKeyDown(legacyKeypadKey)))
            {
                return true;
            }
#endif
            return false;
        }
    }
}
