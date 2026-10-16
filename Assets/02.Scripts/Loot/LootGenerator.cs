using System;
using System.Collections.Generic;
using InventorySystem;

namespace ShotgunAndRun.Loot
{
    // 런 시작 시 상자 위치와 내용물을 한 번에 결정합니다 (game-spec 7.2).
    // 순서: 존 상자 → 커넥터 상자 → 퀘스트 아이템. 퀘스트 아이템은 이미 생성된 상자에 넣습니다 (회수 고정 상자 제외).
    // MonoBehaviour가 아니므로 같은 시드 · 입력이면 항상 같은 결과가 나옵니다.
    public sealed class LootGenerator
    {
        private readonly StageLootRules _rules;

        public LootGenerator(StageLootRules rules)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        public LootPlan Generate(int seed, IReadOnlyList<ChestPointInfo> points, IReadOnlyList<QuestLootRequest> quests)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            Random random = new Random(seed);
            LootPlan plan = new LootPlan(seed);

            PlaceZoneChests(random, points, plan);
            PlaceConnectorChests(random, points, plan);

            if (quests != null)
            {
                PlaceQuestItems(random, points, quests, plan);
            }

            return plan;
        }

        // 존마다 후보 지점 중 3곳 → 세트 1개를 골라 섞어서 배정 (game-spec 7.3)
        private void PlaceZoneChests(Random random, IReadOnlyList<ChestPointInfo> points, LootPlan plan)
        {
            foreach (ZoneLootDefinition zone in _rules.Zones)
            {
                List<int> candidates = FindPoints(points, ChestPointKind.Zone, zone.ZoneId);
                if (candidates.Count < _rules.ZoneChestCount)
                {
                    plan.Warn($"{zone.ZoneId}: 존 상자 후보 지점 {candidates.Count}곳 < {_rules.ZoneChestCount}개");
                }

                List<ZoneChestSet> sets = zone.Sets.Count > 0 ? zone.Sets : _rules.TemplateSets;
                if (sets.Count == 0)
                {
                    plan.Warn($"{zone.ZoneId}: 존 상자 세트 없음");
                    continue;
                }

                ZoneChestSet set = sets[random.Next(sets.Count)];
                List<int> chosen = PickDistinct(random, candidates, _rules.ZoneChestCount);
                List<ChestContent> contents = new List<ChestContent>(set.Chests);
                Shuffle(random, contents);

                for (int i = 0; i < chosen.Count; i++)
                {
                    PlannedChest chest = new PlannedChest(chosen[i], ChestPointKind.Zone, zone.ZoneId);
                    if (i < contents.Count)
                    {
                        FillFromContent(random, zone, contents[i], chest);
                    }

                    plan.Add(chest);
                }
            }
        }

        // 구간마다 1~2개 (Z3–Z9는 1개). 두 존의 주 · 부 재료 슬롯에서 균등 2~3개 + 치유 약초 30% (game-spec 7.3)
        private void PlaceConnectorChests(Random random, IReadOnlyList<ChestPointInfo> points, LootPlan plan)
        {
            foreach (ConnectorSegmentDefinition segment in _rules.Segments)
            {
                List<MaterialType> pool = BuildConnectorPool(segment, plan);
                if (pool.Count == 0)
                {
                    continue;
                }

                List<int> candidates = FindPoints(points, ChestPointKind.Connector, segment.SegmentId);
                int count = random.Next(segment.MinChests, Math.Max(segment.MinChests, segment.MaxChests) + 1);
                if (candidates.Count < count)
                {
                    plan.Warn($"{segment.SegmentId}: 커넥터 상자 후보 지점 {candidates.Count}곳 < {count}개");
                }

                foreach (int pointIndex in PickDistinct(random, candidates, count))
                {
                    PlannedChest chest = new PlannedChest(pointIndex, ChestPointKind.Connector, segment.SegmentId);
                    int itemCount = random.Next(_rules.ConnectorMinItems, Math.Max(_rules.ConnectorMinItems, _rules.ConnectorMaxItems) + 1);
                    for (int i = 0; i < itemCount; i++)
                    {
                        chest.AddMaterial(pool[random.Next(pool.Count)], 1);
                    }

                    if (random.NextDouble() < _rules.ConnectorHerbChance)
                    {
                        chest.AddMaterial(_rules.HerbMaterial, 1);
                    }

                    plan.Add(chest);
                }
            }
        }

        // 두 존의 주 · 부 재료 슬롯 6개 (마력석 슬롯 제외). 같은 재료가 두 번 들어가면 그만큼 자주 나옵니다.
        private List<MaterialType> BuildConnectorPool(ConnectorSegmentDefinition segment, LootPlan plan)
        {
            List<MaterialType> pool = new List<MaterialType>(6);
            foreach (string zoneId in new[] { segment.ZoneA, segment.ZoneB })
            {
                ZoneLootDefinition zone = _rules.FindZone(zoneId);
                if (zone == null)
                {
                    plan.Warn($"{segment.SegmentId}: 존 {zoneId} 정의 없음");
                    continue;
                }

                foreach (MaterialType material in new[] { zone.Main, zone.Sub1, zone.Sub2 })
                {
                    if (material != _rules.ConnectorExcludedMaterial)
                    {
                        pool.Add(material);
                    }
                }
            }

            return pool;
        }

        // 인증 상자를 먼저 잠가야 보물 후보에서 뺄 수 있으므로 초반 탐사 → 회수 → 보물찾기 순으로 처리합니다.
        private void PlaceQuestItems(Random random, IReadOnlyList<ChestPointInfo> points, IReadOnlyList<QuestLootRequest> quests, LootPlan plan)
        {
            foreach (QuestLootRequest quest in quests)
            {
                if (quest.Kind == QuestLootKind.ZoneCertificate)
                {
                    PlaceCertificate(random, quest, plan);
                }
            }

            foreach (QuestLootRequest quest in quests)
            {
                if (quest.Kind == QuestLootKind.Recovery)
                {
                    PlaceRecovery(random, points, quest, plan);
                }
            }

            foreach (QuestLootRequest quest in quests)
            {
                if (quest.Kind == QuestLootKind.Treasure)
                {
                    PlaceTreasure(random, quest, plan);
                }
            }
        }

        // 대상 존의 존 상자 3개 중 1개. 잠그지 않음: 수비대 사이에서 열어야 하므로 사실상 소탕이 필요 (game-spec 9.3)
        private static void PlaceCertificate(Random random, QuestLootRequest quest, LootPlan plan)
        {
            List<PlannedChest> candidates = new List<PlannedChest>();
            foreach (PlannedChest chest in plan.Chests)
            {
                if (chest.Kind == ChestPointKind.Zone && chest.OwnerId == quest.ZoneA)
                {
                    candidates.Add(chest);
                }
            }

            if (candidates.Count == 0)
            {
                plan.Warn($"{quest.ItemId}: {quest.ZoneA}에 인증 아이템을 넣을 존 상자 없음");
                return;
            }

            PlannedChest target = candidates[random.Next(candidates.Count)];
            target.AddQuestItem(quest.ItemId);
        }

        // 존의 고정 상자에 회수 아이템 + 치유 약초 1~2개 (game-spec 9.3)
        private void PlaceRecovery(Random random, IReadOnlyList<ChestPointInfo> points, QuestLootRequest quest, LootPlan plan)
        {
            List<int> candidates = FindPoints(points, ChestPointKind.RecoveryFixed, quest.ZoneA);
            if (candidates.Count == 0)
            {
                plan.Warn($"{quest.ItemId}: {quest.ZoneA}에 회수 고정 상자 지점 없음");
                return;
            }

            PlannedChest chest = new PlannedChest(candidates[0], ChestPointKind.RecoveryFixed, quest.ZoneA);
            chest.AddQuestItem(quest.ItemId);
            chest.AddMaterial(_rules.HerbMaterial, random.Next(_rules.RecoveryHerbMin, Math.Max(_rules.RecoveryHerbMin, _rules.RecoveryHerbMax) + 1));
            plan.Add(chest);
        }

        // 두 존의 존 상자 + 사이 커넥터 상자 중 이번 런에 생성된 상자
        private void PlaceTreasure(Random random, QuestLootRequest quest, LootPlan plan)
        {
            List<PlannedChest> candidates = new List<PlannedChest>();
            foreach (PlannedChest chest in plan.Chests)
            {
                bool inZone = chest.Kind == ChestPointKind.Zone && (chest.OwnerId == quest.ZoneA || chest.OwnerId == quest.ZoneB);
                bool inSegment = chest.Kind == ChestPointKind.Connector && IsSegmentBetween(chest.OwnerId, quest.ZoneA, quest.ZoneB);
                if (inZone || inSegment)
                {
                    candidates.Add(chest);
                }
            }

            if (candidates.Count == 0)
            {
                plan.Warn($"{quest.ItemId}: {quest.ZoneA}–{quest.ZoneB}에 보물을 넣을 상자 없음");
                return;
            }

            candidates[random.Next(candidates.Count)].AddQuestItem(quest.ItemId);
        }

        private bool IsSegmentBetween(string segmentId, string zoneA, string zoneB)
        {
            ConnectorSegmentDefinition segment = _rules.FindSegment(segmentId);
            return segment != null && segment.Connects(zoneA, zoneB);
        }

        private static void FillFromContent(Random random, ZoneLootDefinition zone, ChestContent content, PlannedChest chest)
        {
            foreach (LootEntry entry in content.Entries)
            {
                if (entry.Chance < 1f && random.NextDouble() >= entry.Chance)
                {
                    continue;
                }

                chest.AddMaterial(zone.Resolve(entry.Slot, entry.Material), entry.Amount);
            }
        }

        private static List<int> FindPoints(IReadOnlyList<ChestPointInfo> points, ChestPointKind kind, string ownerId)
        {
            List<int> result = new List<int>();
            for (int i = 0; i < points.Count; i++)
            {
                if (points[i].Kind == kind && points[i].OwnerId == ownerId)
                {
                    result.Add(i);
                }
            }

            return result;
        }

        private static List<int> PickDistinct(Random random, List<int> source, int count)
        {
            List<int> copy = new List<int>(source);
            Shuffle(random, copy);
            if (copy.Count > count)
            {
                copy.RemoveRange(count, copy.Count - count);
            }

            return copy;
        }

        private static void Shuffle<T>(Random random, List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
