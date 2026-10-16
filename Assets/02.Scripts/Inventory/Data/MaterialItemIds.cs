using System;

namespace InventorySystem
{
    // Material Type의 ItemId를 반환하기 위해 생성한 클래스
    public static class MaterialItemIds
    {
        public const string IronIngot = "material.iron_ingot";
        public const string DwarvenMechanism = "material.dwarven_mechanism";
        public const string TrollOil = "material.troll_oil";
        public const string TinkerSpring = "material.tinkers_spring";
        public const string TannedLeather = "material.tanned_leather";
        public const string ElvenSilk = "material.elven_silk";
        public const string SulfurPowder = "material.sulfur_powder";
        public const string LeadChunk = "material.lead_chunk";
        public const string MagicStone = "material.magic_stone";
        public const string HealingHerb = "material.healing_herb";

        public static string GetItemId(MaterialType materialType)
        {
            if (TryGetItemId(materialType, out string itemId))
            {
                return itemId;
            }

            throw new ArgumentOutOfRangeException(
                nameof(materialType),
                materialType,
                null);
        }

        public static bool TryGetItemId(
            MaterialType materialType,
            out string itemId)
        {
            itemId = materialType switch
            {
                MaterialType.DwarvenMechanism => DwarvenMechanism,
                MaterialType.ElvenSilk => ElvenSilk,
                MaterialType.TrollOil => TrollOil,
                MaterialType.TinkerSpring => TinkerSpring,
                MaterialType.TannedLeather => TannedLeather,
                MaterialType.IronIngot => IronIngot,
                MaterialType.SulfurPowder => SulfurPowder,
                MaterialType.LeadChunk => LeadChunk,
                MaterialType.MagicStone => MagicStone,
                MaterialType.HealingHerb => HealingHerb,
                _ => null
            };

            return itemId != null;
        }
    }
}
