using UnityEngine;

namespace InventorySystem
{
    [CreateAssetMenu(fileName = "NewItemData", menuName = "ShotgunAndRun/Inventory/Item Data")]
    public class ItemData : ScriptableObject
    {
        [Header("Item Info")]
        [SerializeField] private string _id;
        [SerializeField] private string _itemName;
        [SerializeField] private Sprite _icon;
        [SerializeField] private ItemType _itemType; // 아이템이 소모품, 퀘스트, 강화 재료, 기타인지
        [SerializeField] private MaterialCategory _materialCategory; // 아이템이 재료 중 일반 재료인지, 코어 재료인지
        [Tooltip("ItemType이 Material일 때 사용하는 재료 식별자입니다.")]
        [SerializeField] private MaterialType _materialType;
        [TextArea(2, 4)]
        [SerializeField] private string _description;

        [Header("Stack Settings")]
        [Tooltip("한 슬롯에 최대 몇 개까지 겹칠 수 있는지 설정합니다.")]
        [SerializeField, Min(1)] private int _maxStack = 99;

        public string Id => _id;
        public string ItemName => _itemName;
        public string Description => _description;
        public Sprite Icon => _icon;
        public ItemType ItemType => _itemType;
        public MaterialCategory MaterialCategory => _materialCategory;
        public MaterialType MaterialType => _materialType;
        public int MaxStack => _maxStack;
    }
}
