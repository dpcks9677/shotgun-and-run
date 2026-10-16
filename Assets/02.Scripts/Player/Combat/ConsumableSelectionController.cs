using System;
using ShotgunAndRun.Player;
using UnityEngine;

namespace Consumable
{
    public class ConsumableSelectionController : MonoBehaviour
    {
        private ConsumableEffectService _consumableEffectService;

        [SerializeField] private ConsumableLoadout _loadout;
        [SerializeField] private PlayerConsumableInventory _inventory;

        private ConsumableData _selectedConsumable;
        public ConsumableData SelectedConsumable => _selectedConsumable;

        private int _selectedIndex = -1;
        public int SelectedIndex => _selectedIndex;

        public event Action<ConsumableData> OnSelectedConsumable;
        public event Action OnDeselectedConsumable;
        public event Action OnEquipedConsumableChanged;

        // TestData
        [SerializeField] private ConsumableData[] _datas;

        [NonSerialized] private bool _isInventorySubscribed;

        private void Awake()
        {
            _consumableEffectService = GetComponent<ConsumableEffectService>();
            _loadout ??= GetComponent<ConsumableLoadout>();
            _inventory ??= GetComponentInParent<PlayerConsumableInventory>() ??
                           GetComponent<PlayerConsumableInventory>();

            InitializeFallbackLoadout();

            // 새 인벤토리 경로는 PlayerConsumableInventory.TryUseConsumable에서
            // 사용과 수량 차감을 함께 처리하므로 기존 Loadout 이벤트를 구독하지 않습니다.
            if (_inventory == null && _loadout != null && _consumableEffectService != null)
            {
                _consumableEffectService.UsedConsumable += HandleUsedConsumable;
            }

            if (_consumableEffectService != null)
            {
                _consumableEffectService.CanceledConsumable += HandleCanceledConsumable;
            }
        }

        private void OnEnable()
        {
            SubscribeInventoryEvents();
        }

        private void OnDisable()
        {
            UnsubscribeInventoryEvents();
        }

        public void SetInventory(PlayerConsumableInventory inventory)
        {
            if (_inventory == inventory)
            {
                return;
            }

            UnsubscribeInventoryEvents();
            _inventory = inventory;
            if (isActiveAndEnabled)
            {
                SubscribeInventoryEvents();
            }
        }

        private void InitializeFallbackLoadout()
        {
            // ConsumableLoadout이 없고 PlayerConsumableInventory도 없는 경우에만 초기화를 중단합니다.
            // ChangeEquipedConsumables가 두 저장소를 함께 갱신하므로 UI와 선택 데이터가 동기화됩니다.
            if (_loadout == null && _inventory == null)
            {
                return;
            }

            if (_datas != null && _datas.Length > 0)
            {
                int slotCount = _inventory != null ? _inventory.SlotCount : _loadout.SlotLength;
                int count = Mathf.Min(slotCount, _datas.Length);
                for (int i = 0; i < count; i++)
                {
                    if (_datas[i] != null)
                    {
                        ChangeEquipedConsumables(i, 3, _datas[i]);
                    }
                }
            
                return;
            }
        }

        private void OnDestroy()
        {
            UnsubscribeInventoryEvents();

            if (_consumableEffectService != null)
            {
                _consumableEffectService.UsedConsumable -= HandleUsedConsumable;
                _consumableEffectService.CanceledConsumable -= HandleCanceledConsumable;
            }
        }

        private void SubscribeInventoryEvents()
        {
            if (_inventory == null)
            {
                return;
            }

            _inventory.SlotChanged -= HandleInventorySlotChanged;
            _inventory.SlotChanged += HandleInventorySlotChanged;
            _isInventorySubscribed = true;
        }

        private void UnsubscribeInventoryEvents()
        {
            if (_inventory != null)
            {
                _inventory.SlotChanged -= HandleInventorySlotChanged;
            }

            _isInventorySubscribed = false;
        }

        private void HandleInventorySlotChanged(int slotIndex, ShotgunAndRun.Player.ConsumableSlot slot)
        {
            if (_selectedIndex == slotIndex)
            {
                if (slot == null || slot.IsEmpty || slot.Data == null)
                {
                    DeselectedConsumable();
                }
                else
                {
                    _selectedConsumable = slot.Data;
                }
            }
        }

        public void ChangeEquipedConsumables(int index, int quantity, ConsumableData insertConsumable)
        {
            // TODO 수량보다 더 삽입하는 경우에 대한 내용 추가

            if ((_loadout == null && _inventory == null) || insertConsumable == null)
            {
                Debug.LogError("객체가 초기화 되지 않았습니다.");
                return;
            }

            int slotCount = _inventory != null ? _inventory.SlotCount : _loadout.SlotLength;
            if (index < 0 || index >= slotCount)
            {
                Debug.LogError("올바르지 않은 index입니다.");
                return;
            }

            if (_inventory != null)
            {
                if (!_inventory.SetSlot(index, insertConsumable, quantity))
                {
                    return;
                }
            }
            else
            {
                _loadout.EquipConsumable(index, quantity, insertConsumable);
            }

            if (_selectedIndex == index)
            {
                ChangeSelectedConsumable(index);
            }

            if (_inventory == null)
            {
                _loadout?.DeselectConsumable();
            }

            OnEquipedConsumableChanged?.Invoke();
        }

        public void ChangeSelectedConsumable(int index)
        {
            if (!_isInventorySubscribed && _inventory != null)
            {
                SubscribeInventoryEvents();
            }

            if (_inventory != null)
            {
                ChangeSelectedInventoryConsumable(index);
                return;
            }

            if (_loadout == null)
            {
                Debug.LogError("_loadout이 초기화 되지 않았습니다.");
                return;
            }

            if (index < 0 || index >= _loadout.SlotLength)
            {
                Debug.LogError("올바르지 않은 index입니다.");
                return;
            }

            if (_loadout.Slots == null ||
                index >= _loadout.Slots.Length ||
                _loadout.Slots[index] == null ||
                _loadout.Slots[index].ConsumableData == null)
            {
                return;
            }

            CancelActiveHealIfSelectionChanges(index);
            ConsumableData nextConsumable = _loadout.ChangeSelectedConsumable(index);
            _selectedIndex = index;
            _selectedConsumable = nextConsumable;
            OnSelectedConsumable?.Invoke(_selectedConsumable);
        }

        private void ChangeSelectedInventoryConsumable(int index)
        {
            if (index < 0 || index >= _inventory.SlotCount)
            {
                Debug.LogError("올바르지 않은 index입니다.");
                return;
            }

            ShotgunAndRun.Player.ConsumableSlot slot = _inventory.GetSlot(index);
            if (slot == null || slot.IsEmpty || slot.Data == null)
            {
                return;
            }

            CancelActiveHealIfSelectionChanges(index);
            _selectedIndex = index;
            _selectedConsumable = slot.Data;
            OnSelectedConsumable?.Invoke(_selectedConsumable);
        }

        private void CancelActiveHealIfSelectionChanges(int nextIndex)
        {
            if (_selectedIndex == nextIndex || _consumableEffectService == null)
            {
                return;
            }

            if (_consumableEffectService.IsUsingHealKit)
            {
                _consumableEffectService.CancelActiveConsumable();
            }
        }

        public void DeselectedConsumable()
        {
            if (_loadout == null && _inventory == null)
            {
                Debug.LogError("소모품 컨트롤러가 초기화되지 않았습니다.");
                return;
            }

            _selectedConsumable = null;
            _selectedIndex = -1;
            _consumableEffectService?.CancelActiveConsumable();

            if (_inventory == null)
            {
                _loadout?.DeselectConsumable();
            }

            OnDeselectedConsumable?.Invoke();
        }

        private void HandleCanceledConsumable()
        {
            if (_selectedConsumable == null && _selectedIndex < 0)
            {
                return;
            }

            _selectedConsumable = null;
            _selectedIndex = -1;
            if (_inventory == null)
            {
                _loadout?.DeselectConsumable();
            }

            OnDeselectedConsumable?.Invoke();
        }

        public void UseConsumable()
        {
            if (_selectedConsumable == null)
            {
                Debug.LogError("현재 소모품이 선택되지 않았습니다.");
                return;
            }

            // 현재 플레이어는 PlayerConsumableInventory를 사용하므로
            // 수량 확인/효과 실행/수량 차감을 동일한 저장소에서 처리합니다.
            if (_inventory != null)
            {
                if (!_inventory.TryUseConsumable(_selectedIndex))
                {
                    Debug.LogError("소모품의 개수가 부족하거나 선택된 슬롯이 올바르지 않습니다.");
                    DeselectedConsumable();
                    return;
                }

                // 힐 킷은 여기서 선택을 해제하지 않습니다.
                // 사용 완료(UsedConsumable) 또는 마우스 해제/이동/대시 취소 이벤트에서 정리합니다.
                return;
            }

            // _consumableEffectService.UseConsumable(_selectedConsumable);
        }

        public bool IsConsumableSelected()
        {
            return _selectedConsumable != null;
        }

        public void HandleUsedConsumable(ConsumableData selectedConsumable)
        {
            if (_inventory == null && _loadout != null)
            {
                _loadout.UsedConsumable(selectedConsumable);
            }

            // 사용 완료 후에는 취소 호출을 보장하되, 힐 코루틴이 완료된 뒤에는 안전한 no-op입니다.
            _consumableEffectService?.CancelActiveConsumable();

            _selectedConsumable = null;
            _selectedIndex = -1;
            if (_inventory == null)
            {
                _loadout?.DeselectConsumable();
            }

            OnDeselectedConsumable?.Invoke();
        }
    }
}
