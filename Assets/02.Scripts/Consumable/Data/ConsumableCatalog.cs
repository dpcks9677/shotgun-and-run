using System.Collections.Generic;
using UnityEngine;

namespace Consumable
{
    [CreateAssetMenu(fileName = "ConsumableCatalog", menuName = "ShotgunAndRun/Inventory/ConsumableCatalog")]
    public class ConsumableCatalog : ScriptableObject
    {
        [SerializeField] private List<ConsumableData> _consumableDatas;

        public IReadOnlyList<ConsumableData> AllConsumables => _consumableDatas;

        public ConsumableData FindConsumableDataByType(ConsumableType consumableType)
        {
            ConsumableData data = null;

            if (_consumableDatas != null)
            {
                foreach (ConsumableData candidate in _consumableDatas)
                {
                    if (candidate != null && candidate.ConsumableType == consumableType)
                    {
                        data = candidate;
                        break;
                    }
                }
            }

            return data;
        }

        public ConsumableData FindConsumableDataByName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (System.Enum.TryParse<ConsumableType>(name, true, out var type))
            {
                ConsumableData byType = FindConsumableDataByType(type);
                if (byType != null)
                {
                    return byType;
                }
            }

            if (_consumableDatas != null)
            {
                foreach (ConsumableData candidate in _consumableDatas)
                {
                    if (candidate != null && (
                        string.Equals(candidate.name, name, System.StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(candidate.ConsumableName, name, System.StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(candidate.Id, name, System.StringComparison.OrdinalIgnoreCase)))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }
    }
}
