using System;
using System.Collections.Generic;
using Consumable;
using UnityEngine;

namespace ShotgunAndRun.Player
{
    [Serializable]
    public class ConsumableSlot
    {
        [SerializeField] private ConsumableData _data;
        [SerializeField] private int _amount;

        public ConsumableData Data => _data;
        public int Amount => _amount;
        public bool IsEmpty => _data == null || _amount <= 0;

        public ConsumableSlot()
        {
            _data = null;
            _amount = 0;
        }

        public ConsumableSlot(ConsumableData data, int amount)
        {
            _data = data;
            _amount = amount;
        }

        public void Set(ConsumableData data, int amount)
        {
            _data = data;
            _amount = Mathf.Max(0, amount);
            if (_amount == 0)
            {
                _data = null;
            }
        }

        public void AddAmount(int amount)
        {
            _amount = Mathf.Max(0, _amount + amount);
            if (_amount == 0)
            {
                _data = null;
            }
        }

        public void Clear()
        {
            _data = null;
            _amount = 0;
        }
    }

    /// <summary>
    /// 플레이어의 소모품 전용 퀵슬롯(기본 3칸)을 관리하는 컴포넌트입니다.
    /// 단축키 1, 2, 3번 입력을 감지해 소모품 효과(ConsumableEffectService)를 발동하고,
    /// 쉘터 제작 시스템(ConsumableCraftingService)을 통해 즉시 지급받을 수 있습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerConsumableInventory : MonoBehaviour, IConsumableInventory
    {
        public const int DefaultSlotCount = 3;

        [Header("Catalogs & Services")]
        [SerializeField] private ConsumableEffectService _effectService;
        [SerializeField] private ConsumableCatalog _consumableCatalog;

        [Header("Quick Slots (3 Slots)")]
        [SerializeField] private ConsumableSlot[] _slots = new ConsumableSlot[DefaultSlotCount];

        [Header("Input Settings")]
        [SerializeField] private bool _enableHotkeys = true;

        private ConsumableSelectionController _selectionController;

        public int SlotCount => _slots != null ? _slots.Length : DefaultSlotCount;
        public IReadOnlyList<ConsumableSlot> Slots => _slots;

        public event Action<int, ConsumableSlot> SlotChanged;

        private void Awake()
        {
            ResolveDependencies();
            InitializeSlots();
            _selectionController = GetComponentInChildren<ConsumableSelectionController>(true);
        }

        private void OnEnable()
        {
            if (_effectService != null)
            {
                _effectService.UsedConsumable += HandleUsedConsumable;
            }
        }

        private void OnDisable()
        {
            if (_effectService != null)
            {
                _effectService.UsedConsumable -= HandleUsedConsumable;
            }
        }

        private void Reset()
        {
            ResolveDependencies();
            InitializeSlots();
        }

        private void Update()
        {
            // 선택 컨트롤러가 있는 플레이어는 숫자키를 퀵슬롯 선택에 사용합니다.
            // 여기서 다시 사용 처리하면 선택과 사용이 같은 입력에서 중복 실행됩니다.
            if (!_enableHotkeys || _selectionController != null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                TryUseConsumable(0);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                TryUseConsumable(1);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                TryUseConsumable(2);
            }
        }

        public void ResolveDependencies()
        {
            _effectService ??= GetComponent<ConsumableEffectService>() ??
                               GetComponentInParent<ConsumableEffectService>();

#if UNITY_EDITOR
            if (_consumableCatalog == null)
            {
                _consumableCatalog =
                    UnityEditor.AssetDatabase.LoadAssetAtPath<ConsumableCatalog>(
                        "Assets/07.Datas/ConsumableCatalog.asset");
            }
#endif
        }

        public void InitializeSlots()
        {
            if (_slots == null || _slots.Length != DefaultSlotCount)
            {
                _slots = new ConsumableSlot[DefaultSlotCount];
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i] ??= new ConsumableSlot();
            }
        }

        public ConsumableSlot GetSlot(int index)
        {
            if (index < 0 || index >= _slots.Length)
            {
                return null;
            }

            return _slots[index];
        }

        public bool SetSlot(int index, ConsumableData data, int amount)
        {
            InitializeSlots();

            if (index < 0 || index >= _slots.Length)
            {
                return false;
            }

            if (data == null || amount <= 0)
            {
                _slots[index].Clear();
                SlotChanged?.Invoke(index, _slots[index]);
                return true;
            }

            _slots[index].Set(data, amount);
            SlotChanged?.Invoke(index, _slots[index]);
            return true;
        }

        public bool ClearSlot(int index)
        {
            return SetSlot(index, null, 0);
        }

        public void ClearAll()
        {
            InitializeSlots();
            for (int i = 0; i < _slots.Length; i++)
            {
                ClearSlot(i);
            }
        }

        public bool CanAddConsumable(ConsumableData data, int amount = 1)
        {
            if (data == null || amount <= 0)
            {
                return false;
            }

            InitializeSlots();

            // 1. 기존 동일 아이템 슬롯에 중첩 여유가 있는지 확인
            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].IsEmpty && _slots[i].Data == data)
                {
                    if (_slots[i].Amount + amount <= data.MaxStack)
                    {
                        return true;
                    }
                }
            }

            // 2. 비어있는 슬롯이 있는지 확인
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    return amount <= data.MaxStack;
                }
            }

            return false;
        }

        public bool TryAddConsumable(ConsumableData data, int amount = 1)
        {
            if (!CanAddConsumable(data, amount))
            {
                return false;
            }

            // 1. 기존 동일 아이템 슬롯에 먼저 누적
            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].IsEmpty && _slots[i].Data == data)
                {
                    if (_slots[i].Amount + amount <= data.MaxStack)
                    {
                        _slots[i].AddAmount(amount);
                        SlotChanged?.Invoke(i, _slots[i]);
                        return true;
                    }
                }
            }

            // 2. 비어있는 슬롯에 배치
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    _slots[i].Set(data, amount);
                    SlotChanged?.Invoke(i, _slots[i]);
                    return true;
                }
            }

            return false;
        }

        public bool TryUseConsumable(int slotIndex)
        {
            InitializeSlots();

            if (slotIndex < 0 || slotIndex >= _slots.Length)
            {
                return false;
            }

            ConsumableSlot slot = _slots[slotIndex];
            if (slot == null || slot.IsEmpty)
            {
                return false;
            }

            ConsumableData data = slot.Data;

            if (_effectService == null)
            {
                Debug.LogWarning(
                    "[PlayerConsumableInventory] ConsumableEffectService를 찾을 수 없습니다.");
                return false;
            }

            _effectService.UseConsumable(data);

            return true;
        }

        public void SetSelectionController(ConsumableSelectionController selectionController)
        {
            _selectionController = selectionController;
        }

        private void HandleUsedConsumable(ConsumableData usedData)
        {
            if (usedData == null)
            {
                return;
            }

            InitializeSlots();

            // 1. 현재 선택된 슬롯이 사용된 아이템과 일치한다면 해당 슬롯을 우선 차감
            if (_selectionController != null && _selectionController.SelectedIndex >= 0)
            {
                int selectedIdx = _selectionController.SelectedIndex;
                if (selectedIdx < _slots.Length)
                {
                    ConsumableSlot selectedSlot = _slots[selectedIdx];
                    if (selectedSlot != null && !selectedSlot.IsEmpty && selectedSlot.Data == usedData)
                    {
                        selectedSlot.AddAmount(-1);
                        SlotChanged?.Invoke(selectedIdx, selectedSlot);
                        _selectionController?.HandleUsedConsumable(usedData);
                        return;
                    }
                }
            }

            // 2. 일치하는 슬롯 탐색 및 차감
            for (int i = 0; i < _slots.Length; i++)
            {
                ConsumableSlot slot = _slots[i];

                if (slot == null || slot.IsEmpty || slot.Data != usedData)
                {
                    continue;
                }

                slot.AddAmount(-1);
                SlotChanged?.Invoke(i, slot);
                _selectionController?.HandleUsedConsumable(usedData);
                return;
            }

            Debug.LogWarning(
                $"사용된 소모품을 인벤토리에서 찾을 수 없습니다: {usedData.ConsumableType}");
        }

        /// <summary>
        /// 씬 전환 시 퀵슬롯 보존을 위해 현재 슬롯들의 (소모품 타입명, 수량) 배열을 반환합니다.
        /// </summary>
        public (string typeName, int amount)[] GetSlotSnapshots()
        {
            InitializeSlots();
            var snapshots = new (string typeName, int amount)[_slots.Length];
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (slot != null && !slot.IsEmpty && slot.Data != null)
                {
                    snapshots[i] = (slot.Data.ConsumableType.ToString(), slot.Amount);
                }
                else
                {
                    snapshots[i] = (string.Empty, 0);
                }
            }

            return snapshots;
        }

        /// <summary>
        /// 씬 전환 후 백업된 (소모품 타입명, 수량) 데이터를 바탕으로 슬롯을 복원합니다.
        /// </summary>
        public void RestoreFromSnapshots(IReadOnlyList<(string typeName, int amount)> snapshots)
        {
            InitializeSlots();
            ResolveDependencies();

            for (int i = 0; i < _slots.Length; i++)
            {
                if (snapshots != null && i < snapshots.Count && _consumableCatalog != null)
                {
                    var (typeName, amount) = snapshots[i];
                    if (!string.IsNullOrEmpty(typeName) && amount > 0 &&
                        Enum.TryParse<ConsumableType>(typeName, out var type))
                    {
                        ConsumableData data = _consumableCatalog.FindConsumableDataByType(type);
                        if (data != null)
                        {
                            _slots[i].Set(data, amount);
                            SlotChanged?.Invoke(i, _slots[i]);
                            continue;
                        }
                    }
                }

                _slots[i].Clear();
                SlotChanged?.Invoke(i, _slots[i]);
            }
        }
    }
}
