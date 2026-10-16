using NUnit.Framework;
using UnityEngine;

namespace Consumable
{
    [CreateAssetMenu(fileName = "ConsumableData", menuName = "ShotgunAndRun/Inventory/ConsumableData")]
    public class ConsumableData : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _consumableName;
        [SerializeField] private Sprite _icon;
        [SerializeField] private ConsumableEffectData _consumableEffectData;
        [SerializeField] private ConsumableType _consumableType;
        [SerializeField] private ConsumableCategory _consumableCategory;
        [TextArea(2, 4)]
        [SerializeField] private string _description;
        [SerializeField, Min(0)] private int _maxStack = 3;

        public string Id => _id;
        public string ConsumableName => _consumableName;
        public Sprite Icon => _icon;
        public ConsumableEffectData ConsumableEffectData => _consumableEffectData;
        public ConsumableType ConsumableType => _consumableType;
        public string Description => _description;
        public int MaxStack => _maxStack;
    }
}
