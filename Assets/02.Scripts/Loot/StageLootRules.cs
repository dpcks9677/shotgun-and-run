using System;
using System.Collections.Generic;
using InventorySystem;
using UnityEngine;

namespace ShotgunAndRun.Loot
{
    // 스테이지 하나의 상자 생성 규칙 (game-spec 4.5 · 7.3 · 9.3).
    // StageLootConfig 에셋이 들고 있고, LootGenerator는 이 데이터만 읽습니다.
    [Serializable]
    public sealed class StageLootRules
    {
        [Header("Zone Chests")]
        [Min(1)] public int ZoneChestCount = 3;

        [Tooltip("일반 존 공통 템플릿 세트. Main · Sub1 · Sub2 슬롯은 존마다 다른 재료로 바뀝니다.")]
        public List<ZoneChestSet> TemplateSets = new List<ZoneChestSet>();

        public List<ZoneLootDefinition> Zones = new List<ZoneLootDefinition>();

        [Header("Connector Chests")]
        public List<ConnectorSegmentDefinition> Segments = new List<ConnectorSegmentDefinition>();

        [Min(1)] public int ConnectorMinItems = 2;
        [Min(1)] public int ConnectorMaxItems = 3;
        [Range(0f, 1f)] public float ConnectorHerbChance = 0.3f;

        [Tooltip("커넥터 상자에서 나오지 않는 재료 (마력석)")] public MaterialType ConnectorExcludedMaterial = MaterialType.MagicStone;

        [Header("Common")]
        public MaterialType HerbMaterial = MaterialType.HealingHerb;

        [Header("Recovery Quest Chest")]
        [Min(0)] public int RecoveryHerbMin = 1;
        [Min(0)] public int RecoveryHerbMax = 2;

        public ZoneLootDefinition FindZone(string zoneId)
        {
            foreach (ZoneLootDefinition zone in Zones)
            {
                if (zone.ZoneId == zoneId)
                {
                    return zone;
                }
            }

            return null;
        }

        public ConnectorSegmentDefinition FindSegment(string segmentId)
        {
            foreach (ConnectorSegmentDefinition segment in Segments)
            {
                if (segment.SegmentId == segmentId)
                {
                    return segment;
                }
            }

            return null;
        }
    }

    [Serializable]
    public sealed class ZoneLootDefinition
    {
        public string ZoneId;
        public MaterialType Main;
        public MaterialType Sub1;
        public MaterialType Sub2;

        [Tooltip("비어 있으면 공통 템플릿 세트를 사용합니다 (Z5 · Z8 · Z10만 직접 작성).")]
        public List<ZoneChestSet> Sets = new List<ZoneChestSet>();

        public MaterialType Resolve(LootMaterialSlot slot, MaterialType fixedMaterial)
        {
            switch (slot)
            {
                case LootMaterialSlot.Main:
                    return Main;
                case LootMaterialSlot.Sub1:
                    return Sub1;
                case LootMaterialSlot.Sub2:
                    return Sub2;
                default:
                    return fixedMaterial;
            }
        }
    }

    // 존 상자 3개분 내용. 런마다 세트 하나를 골라 생성된 지점에 섞어서 배정합니다.
    [Serializable]
    public sealed class ZoneChestSet
    {
        public List<ChestContent> Chests = new List<ChestContent>();
    }

    [Serializable]
    public sealed class ChestContent
    {
        public List<LootEntry> Entries = new List<LootEntry>();
    }

    [Serializable]
    public sealed class LootEntry
    {
        public LootMaterialSlot Slot = LootMaterialSlot.Fixed;
        public MaterialType Material;
        [Min(1)] public int Amount = 1;

        [Tooltip("항목 전체가 들어갈 확률 (예: Z8 마력석 0.5)")]
        [Range(0f, 1f)]
        public float Chance = 1f;

        public LootEntry()
        {
        }

        public LootEntry(LootMaterialSlot slot, MaterialType material, int amount, float chance = 1f)
        {
            Slot = slot;
            Material = material;
            Amount = amount;
            Chance = chance;
        }
    }

    // 커넥터 구간 (level-design 4장). 상자 재료는 두 존 기준입니다.
    [Serializable]
    public sealed class ConnectorSegmentDefinition
    {
        public string SegmentId;
        public string ZoneA;
        public string ZoneB;
        [Min(0)] public int MinChests = 1;
        [Min(0)] public int MaxChests = 2;

        public bool Connects(string zoneA, string zoneB)
        {
            return (ZoneA == zoneA && ZoneB == zoneB) || (ZoneA == zoneB && ZoneB == zoneA);
        }
    }
}
