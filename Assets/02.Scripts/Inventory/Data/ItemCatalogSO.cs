using System.Collections.Generic;
using UnityEngine;

namespace InventorySystem
{
    /// <summary>
    /// 프로젝트에서 사용하는 ItemData 목록을 등록하고 ID로 검색하는 카탈로그입니다.
    /// 인벤토리 수량이나 보유 상태는 저장하지 않고, 아이템 정의 에셋만 관리합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemCatalog", menuName = "ShotgunAndRun/Inventory/Item Catalog")]
    public sealed class ItemCatalogSO : ScriptableObject
    {
        [Header("Item Definitions")]
        [SerializeField] private List<ItemData> _items = new();

        // id에 따른 ItemData 검색용 딕셔너리
        private readonly Dictionary<string, ItemData> _itemsById =
            new Dictionary<string, ItemData>(System.StringComparer.Ordinal);

        //MaterialType에 따른 ItemData 검색용 딕셔너리
        private readonly Dictionary<MaterialType, ItemData> _materialsByType = new();
        private bool _lookupBuilt; // 검색용 Dictionary가 구성되었는지 여부

        public IReadOnlyList<ItemData> Items => _items;

        private void OnEnable()
        {
            RebuildLookup(logErrors: true);
        }

        private void OnValidate()
        {
            RebuildLookup(logErrors: true);
        }

        /// <summary>
        /// ID로 아이템 정의를 검색합니다.
        /// 저장 데이터 복원 시 사용합니다.
        /// </summary>
        public bool TryGetById(string id, out ItemData itemData)
        {
            EnsureLookupBuilt();

            if (string.IsNullOrWhiteSpace(id))
            {
                itemData = null;
                return false;
            }

            return _itemsById.TryGetValue(id, out itemData);
        }

        /// <summary>
        /// ID로 아이템 정의를 검색합니다. 찾지 못하면 null을 반환합니다.
        /// </summary>
        public ItemData GetById(string id)
        {
            return TryGetById(id, out ItemData itemData) ? itemData : null;
        }

        public bool ContainsId(string id)
        {
            return TryGetById(id, out _);
        }

        // MaterialType으로 ItemData를 검색
        public bool TryGetByMaterialType(
            MaterialType materialType,
            out ItemData itemData)
        {
            EnsureLookupBuilt();
            return _materialsByType.TryGetValue(materialType, out itemData);
        }

        /// <summary>
        /// 카탈로그의 ID와 null 항목을 검사합니다.
        /// Inspector의 Context Menu에서도 실행할 수 있습니다.
        /// </summary>
        [ContextMenu("Validate Catalog")]
        public bool ValidateCatalog()
        {
            return RebuildLookup(logErrors: true);
        }

        private void EnsureLookupBuilt()
        {
            if (!_lookupBuilt)
            {
                RebuildLookup(logErrors: true);
            }
        }

        // 검색용 Dictionary를 다시 구성
        private bool RebuildLookup(bool logErrors)
        {
            _itemsById.Clear();
            _materialsByType.Clear();
            _lookupBuilt = true;

            bool isValid = true;

            if (_items == null)
            {
                _items = new List<ItemData>();
                return true;
            }

            for (int i = 0; i < _items.Count; i++)
            {
                ItemData itemData = _items[i];

                if (itemData == null)
                {
                    isValid = false;
                    if (logErrors)
                    {
                        Debug.LogError(
                            $"{name}의 Item 목록 {i}번 항목이 비어 있습니다.",
                            this);
                    }

                    continue;
                }

                if (string.IsNullOrWhiteSpace(itemData.Id))
                {
                    isValid = false;
                    if (logErrors)
                    {
                        Debug.LogError(
                            $"ItemData '{itemData.name}'의 ID가 비어 있습니다.",
                            itemData);
                    }

                    continue;
                }

                if (_itemsById.ContainsKey(itemData.Id))
                {
                    isValid = false;
                    if (logErrors)
                    {
                        Debug.LogError(
                            $"ItemData ID가 중복되었습니다: '{itemData.Id}'. " +
                            "저장 데이터 복원을 위해 ID는 고유해야 합니다.",
                            itemData);
                    }

                    continue;
                }

                _itemsById.Add(itemData.Id, itemData);

                if (itemData.ItemType != ItemType.Material)
                {
                    continue;
                }

                if (_materialsByType.ContainsKey(itemData.MaterialType))
                {
                    isValid = false;
                    if (logErrors)
                    {
                        Debug.LogError(
                            $"MaterialType이 중복되었습니다: " +
                            $"'{itemData.MaterialType}'.",
                            itemData);
                    }

                    continue;
                }

                _materialsByType.Add(itemData.MaterialType, itemData);
            }

            return isValid;
        }
    }
}
