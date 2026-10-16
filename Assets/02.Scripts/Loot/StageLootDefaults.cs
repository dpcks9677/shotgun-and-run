using System.Collections.Generic;
using InventorySystem;

namespace ShotgunAndRun.Loot
{
    // 스테이지 1 기본 생성 규칙 (game-spec 4.5 · 7.3, level-design 4장).
    // StageLootConfig의 "스테이지 1 기본값으로 채우기"와 테스트가 같은 값을 쓰도록 코드로 둡니다.
    public static class StageLootDefaults
    {
        private const MaterialType IronIngot = MaterialType.IronIngot; // 철괴
        private const MaterialType DwarvenMechanism = MaterialType.DwarvenMechanism; // 드워프 기계장치
        private const MaterialType TrollOil = MaterialType.TrollOil; // 트롤 기름
        private const MaterialType TinkerSpring = MaterialType.TinkerSpring; // 땜장이 용수철
        private const MaterialType TannedLeather = MaterialType.TannedLeather; // 무두질한 가죽
        private const MaterialType ElvenSilk = MaterialType.ElvenSilk; // 엘프 실크
        private const MaterialType SulfurPowder = MaterialType.SulfurPowder; // 유황 가루
        private const MaterialType LeadChunk = MaterialType.LeadChunk; // 납덩이
        private const MaterialType MagicStone = MaterialType.MagicStone; // 마력석
        private const MaterialType Herb = MaterialType.HealingHerb; // 치유 약초

        public static StageLootRules CreateStage1()
        {
            StageLootRules rules = new StageLootRules();
            rules.TemplateSets.AddRange(CreateTemplateSets());

            rules.Zones.Add(Zone("Z1", IronIngot, LeadChunk, TrollOil));
            rules.Zones.Add(Zone("Z2", DwarvenMechanism, TinkerSpring, IronIngot));
            rules.Zones.Add(Zone("Z3", TrollOil, IronIngot, SulfurPowder));
            rules.Zones.Add(Zone("Z4", TinkerSpring, TannedLeather, DwarvenMechanism));
            rules.Zones.Add(Zone("Z5", MagicStone, TannedLeather, ElvenSilk, CreateZ5Sets()));
            rules.Zones.Add(Zone("Z6", TannedLeather, Herb, TinkerSpring));
            rules.Zones.Add(Zone("Z7", ElvenSilk, SulfurPowder, Herb));
            rules.Zones.Add(Zone("Z8", SulfurPowder, MagicStone, LeadChunk, CreateZ8Sets()));
            rules.Zones.Add(Zone("Z9", LeadChunk, ElvenSilk, DwarvenMechanism));
            rules.Zones.Add(Zone("Z10", Herb, MagicStone, SulfurPowder, CreateZ10Sets()));

            rules.Segments.Add(Segment("Z1", "Z9"));
            rules.Segments.Add(Segment("Z1", "Z10"));
            rules.Segments.Add(Segment("Z9", "Z10"));
            rules.Segments.Add(Segment("Z1", "Z2"));
            rules.Segments.Add(Segment("Z3", "Z9", 1, 1)); // 탄약 하향 구간 (level-design 4장)
            rules.Segments.Add(Segment("Z8", "Z9"));
            rules.Segments.Add(Segment("Z3", "Z8"));
            rules.Segments.Add(Segment("Z3", "Z4"));
            rules.Segments.Add(Segment("Z4", "Z5"));
            rules.Segments.Add(Segment("Z4", "Z6"));
            rules.Segments.Add(Segment("Z5", "Z6"));
            rules.Segments.Add(Segment("Z5", "Z7"));
            rules.Segments.Add(Segment("Z6", "Z7"));
            rules.Segments.Add(Segment("Z7", "Z8"));
            rules.Segments.Add(Segment("Z2", "Z8"));
            rules.Segments.Add(Segment("Z2", "Z7"));

            // R7 회랑: 방 다섯 개(R7 · J17 · R21 · R22 · R24)를 합친 큰 방이라 일반 구간보다 많이 생성 (level-design 1.4 · 4장)
            rules.Segments.Add(Segment("Z9", "Z10", 2, 3, R7SegmentId));
            return rules;
        }

        public const string R7SegmentId = "R7";

        public static string SegmentId(string zoneA, string zoneB)
        {
            return $"{zoneA}-{zoneB}";
        }

        // 일반 존 공통 템플릿: 주 3 + 부 3 + 약초 1 = 7개
        private static IEnumerable<ZoneChestSet> CreateTemplateSets()
        {
            yield return Set(
                Chest(E(LootMaterialSlot.Main, 2), E(LootMaterialSlot.Sub1, 1)),
                Chest(E(LootMaterialSlot.Main, 1), E(LootMaterialSlot.Sub2, 1)),
                Chest(E(LootMaterialSlot.Sub1, 1), E(Herb, 1)));
            yield return Set(
                Chest(E(LootMaterialSlot.Main, 2), E(LootMaterialSlot.Sub2, 1)),
                Chest(E(LootMaterialSlot.Main, 1), E(LootMaterialSlot.Sub1, 1)),
                Chest(E(LootMaterialSlot.Sub2, 1), E(Herb, 1)));
            yield return Set(
                Chest(E(LootMaterialSlot.Main, 1), E(LootMaterialSlot.Sub1, 1), E(LootMaterialSlot.Sub2, 1)),
                Chest(E(LootMaterialSlot.Main, 2)),
                Chest(E(LootMaterialSlot.Sub1, 1), E(Herb, 1)));
        }

        // 마력석 기대치 약 2.5
        private static List<ZoneChestSet> CreateZ5Sets()
        {
            return new List<ZoneChestSet>
            {
                Set(
                    Chest(E(MagicStone, 1), E(TannedLeather, 1)),
                    Chest(E(MagicStone, 1), E(TannedLeather, 1), E(ElvenSilk, 1)),
                    Chest(E(MagicStone, 1, 0.5f), E(ElvenSilk, 1), E(Herb, 1))),
                Set(
                    Chest(E(MagicStone, 2), E(TannedLeather, 1)),
                    Chest(E(TannedLeather, 1), E(ElvenSilk, 1), E(Herb, 1)),
                    Chest(E(MagicStone, 1, 0.5f), E(ElvenSilk, 1)))
            };
        }

        // 마력석 기대치 약 0.5. 부 재료 1(마력석) 자리를 납덩이가 대신함
        private static List<ZoneChestSet> CreateZ8Sets()
        {
            return new List<ZoneChestSet>
            {
                Set(
                    Chest(E(SulfurPowder, 2), E(LeadChunk, 1)),
                    Chest(E(SulfurPowder, 1), E(LeadChunk, 1)),
                    Chest(E(LeadChunk, 1), E(Herb, 1), E(MagicStone, 1, 0.5f))),
                Set(
                    Chest(E(SulfurPowder, 2), E(Herb, 1)),
                    Chest(E(SulfurPowder, 1), E(LeadChunk, 2)),
                    Chest(E(LeadChunk, 1), E(MagicStone, 1, 0.5f)))
            };
        }

        // 약초 5 · 유황 2 · 마력석 1 (확정)
        private static List<ZoneChestSet> CreateZ10Sets()
        {
            return new List<ZoneChestSet>
            {
                Set(
                    Chest(E(Herb, 2), E(MagicStone, 1)),
                    Chest(E(Herb, 2), E(SulfurPowder, 1)),
                    Chest(E(Herb, 1), E(SulfurPowder, 1))),
                Set(
                    Chest(E(Herb, 1), E(SulfurPowder, 1), E(MagicStone, 1)),
                    Chest(E(Herb, 2)),
                    Chest(E(Herb, 2), E(SulfurPowder, 1)))
            };
        }

        private static ZoneLootDefinition Zone(string id, MaterialType main, MaterialType sub1, MaterialType sub2,
            List<ZoneChestSet> sets = null)
        {
            return new ZoneLootDefinition
            {
                ZoneId = id,
                Main = main,
                Sub1 = sub1,
                Sub2 = sub2,
                Sets = sets ?? new List<ZoneChestSet>()
            };
        }

        private static ConnectorSegmentDefinition Segment(string zoneA, string zoneB, int min = 1, int max = 2,
            string segmentId = null)
        {
            return new ConnectorSegmentDefinition
            {
                SegmentId = segmentId ?? SegmentId(zoneA, zoneB),
                ZoneA = zoneA,
                ZoneB = zoneB,
                MinChests = min,
                MaxChests = max
            };
        }

        private static ZoneChestSet Set(params ChestContent[] chests)
        {
            return new ZoneChestSet { Chests = new List<ChestContent>(chests) };
        }

        private static ChestContent Chest(params LootEntry[] entries)
        {
            return new ChestContent { Entries = new List<LootEntry>(entries) };
        }

        private static LootEntry E(LootMaterialSlot slot, int amount)
        {
            return new LootEntry(slot, default, amount);
        }

        private static LootEntry E(MaterialType material, int amount, float chance = 1f)
        {
            return new LootEntry(LootMaterialSlot.Fixed, material, amount, chance);
        }
    }
}
