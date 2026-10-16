using UnityEngine;
using UnityEngine.InputSystem;

namespace InventorySystem.Test
{
    /// <summary>
    /// 인벤토리 및 상자 루팅 기능을 Unity 에디터 및 런타임에서 즉시 테스트할 수 있는 헬퍼 컴포넌트
    /// </summary>
    public class InventoryTester : MonoBehaviour
    {
        [Header("Target Inventory")]
        [SerializeField] private Inventory _inventory;

        [Header("Target Chest (Optional)")]
        [SerializeField] private LootChest _testChest;

        [Header("Test Items / Catalog")]
        [Tooltip("신규 아이템 카탈로그 (선택 사항)")]
        [SerializeField] private ItemCatalogSO _itemCatalog;
        [Tooltip("기본 테스트용 아이템 A (예: 힐링 허브)")]
        [SerializeField] private ItemData _sampleItemA;
        [Tooltip("기본 테스트용 아이템 B (예: 철괴)")]
        [SerializeField] private ItemData _sampleItemB;

        private void Awake()
        {
            if (_inventory == null)
            {
                _inventory = GetComponent<Inventory>();
                if (_inventory == null)
                {
                    _inventory = FindFirstObjectByType<Inventory>();
                }
            }

            if (_testChest == null)
            {
                _testChest = FindFirstObjectByType<LootChest>();
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // [1] 샘플 아이템 A 5개 추가
            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                TestAddSampleItemA();
            }

            // [2] 샘플 아이템 B 또는 카탈로그 랜덤 아이템 추가
            if (keyboard.digit2Key.wasPressedThisFrame)
            {
                TestAddSampleItemBOrRandom();
            }

            // [3] 슬롯 1개 해금 (퀘스트 완료/아이템 사용 시뮬레이션)
            if (keyboard.digit3Key.wasPressedThisFrame)
            {
                TestUnlockNextSlot();
            }

            // [4] 첫 번째 슬롯 아이템 1개 소모
            if (keyboard.digit4Key.wasPressedThisFrame)
            {
                TestRemoveFirstItem();
            }

            // [5] 상자에 테스트 아이템 채우기
            if (keyboard.digit5Key.wasPressedThisFrame)
            {
                TestPopulateChest();
            }
        }

        [ContextMenu("테스트: 샘플 아이템 A 5개 추가 (단축키 1)")]
        public void TestAddSampleItemA()
        {
            if (_inventory == null) return;

            ItemData targetItem = _sampleItemA;
            if (targetItem == null && _itemCatalog != null && _itemCatalog.Items.Count > 0)
            {
                targetItem = _itemCatalog.Items[0];
            }

            if (targetItem != null)
            {
                _inventory.AddItem(targetItem, 5);
                Debug.Log($"[InventoryTester] 플레이어 인벤토리에 '{targetItem.ItemName}' 5개를 추가했습니다.");
            }
            else
            {
                Debug.LogWarning("[InventoryTester] 추가할 아이템(_sampleItemA 또는 _itemCatalog)이 설정되지 않았습니다.");
            }
        }

        [ContextMenu("테스트: 샘플 아이템 B / 랜덤 재료 3개 추가 (단축키 2)")]
        public void TestAddSampleItemBOrRandom()
        {
            if (_inventory == null) return;

            ItemData targetItem = _sampleItemB;
            if (_itemCatalog != null && _itemCatalog.Items.Count > 0)
            {
                int rand = Random.Range(0, _itemCatalog.Items.Count);
                targetItem = _itemCatalog.Items[rand];
            }

            if (targetItem != null)
            {
                _inventory.AddItem(targetItem, 3);
                Debug.Log($"[InventoryTester] 플레이어 인벤토리에 '{targetItem.ItemName}' 3개를 추가했습니다.");
            }
            else
            {
                Debug.LogWarning("[InventoryTester] 추가할 아이템(_sampleItemB 또는 _itemCatalog)이 설정되지 않았습니다.");
            }
        }

        [ContextMenu("테스트: 다음 슬롯 1개 해금 (단축키 3)")]
        public void TestUnlockNextSlot()
        {
            if (_inventory == null) return;
            bool success = _inventory.UnlockNextSlot();
            if (success)
            {
                Debug.Log($"[InventoryTester] 슬롯 해금 성공! (현재 해금: {_inventory.UnlockedCapacity}/{_inventory.MaxCapacity})");
            }
        }

        [ContextMenu("테스트: 첫 번째 채워진 슬롯 아이템 1개 제거 (단축키 4)")]
        public void TestRemoveFirstItem()
        {
            if (_inventory == null) return;

            for (int i = 0; i < _inventory.UnlockedCapacity; i++)
            {
                if (!_inventory.Slots[i].IsEmpty)
                {
                    _inventory.RemoveItemAt(i, 1);
                    Debug.Log($"[InventoryTester] 슬롯 {i}의 아이템 1개를 제거했습니다.");
                    return;
                }
            }
        }

        [ContextMenu("테스트: 상자에 아이템 채우기 (단축키 5)")]
        public void TestPopulateChest()
        {
            if (_testChest == null || _testChest.ChestInventory == null)
            {
                Debug.LogWarning("[InventoryTester] 씬에 LootChest가 없습니다.");
                return;
            }

            // 상자 자체의 GenerateLoot 호출
            _testChest.GenerateLoot();
            Debug.Log("[InventoryTester] 상자에 아이템을 채웠습니다.");
        }
    }
}
