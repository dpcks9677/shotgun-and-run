using System.Collections.Generic;
using System.Linq;
using InventorySystem;
using NUnit.Framework;

namespace ShotgunAndRun.Loot.Tests
{
    public sealed class LootGeneratorTests
    {
        private const int SeedCount = 1000;

        private static readonly string[] TemplateZones = { "Z1", "Z2", "Z3", "Z4", "Z6", "Z7", "Z9" };

        private StageLootRules _rules;
        private List<ChestPointInfo> _points;

        [SetUp]
        public void SetUp()
        {
            _rules = StageLootDefaults.CreateStage1();
            _points = CreatePoints(_rules, zonePoints: 6, connectorPoints: 5);
        }

        [Test]
        public void Generate_SameSeedGivesSamePlan()
        {
            LootGenerator generator = new LootGenerator(_rules);

            LootPlan a = generator.Generate(42, _points, null);
            LootPlan b = generator.Generate(42, _points, null);

            Assert.That(Describe(a), Is.EqualTo(Describe(b)));
        }

        [Test]
        public void ZoneChests_ThreePerZoneWithSetTotals()
        {
            LootGenerator generator = new LootGenerator(_rules);

            for (int seed = 0; seed < SeedCount; seed++)
            {
                LootPlan plan = generator.Generate(seed, _points, null);

                foreach (ZoneLootDefinition zone in _rules.Zones)
                {
                    List<PlannedChest> chests = ChestsOf(plan, ChestPointKind.Zone, zone.ZoneId);
                    Assert.That(chests.Count, Is.EqualTo(3), zone.ZoneId);
                    Assert.That(chests.Select(c => c.PointIndex).Distinct().Count(), Is.EqualTo(3), zone.ZoneId);
                }

                foreach (string zoneId in TemplateZones)
                {
                    Assert.That(Total(ChestsOf(plan, ChestPointKind.Zone, zoneId)), Is.EqualTo(7), zoneId);
                }

                // Z10 마력석 1개 확정 (game-spec 7.3)
                Assert.That(Count(ChestsOf(plan, ChestPointKind.Zone, "Z10"), MaterialType.MagicStone), Is.EqualTo(1));
            }
        }

        [Test]
        public void ZoneChests_ManaStoneExpectationMatchesSpec()
        {
            LootGenerator generator = new LootGenerator(_rules);
            int z5 = 0;
            int z8 = 0;

            for (int seed = 0; seed < SeedCount; seed++)
            {
                LootPlan plan = generator.Generate(seed, _points, null);
                z5 += Count(ChestsOf(plan, ChestPointKind.Zone, "Z5"), MaterialType.MagicStone);
                z8 += Count(ChestsOf(plan, ChestPointKind.Zone, "Z8"), MaterialType.MagicStone);
            }

            Assert.That(z5 / (float)SeedCount, Is.EqualTo(2.5f).Within(0.1f));
            Assert.That(z8 / (float)SeedCount, Is.EqualTo(0.5f).Within(0.1f));
        }

        [Test]
        public void ConnectorChests_FollowSegmentCountAndMaterialRules()
        {
            LootGenerator generator = new LootGenerator(_rules);

            for (int seed = 0; seed < SeedCount; seed++)
            {
                LootPlan plan = generator.Generate(seed, _points, null);

                foreach (ConnectorSegmentDefinition segment in _rules.Segments)
                {
                    List<PlannedChest> chests = ChestsOf(plan, ChestPointKind.Connector, segment.SegmentId);
                    Assert.That(chests.Count, Is.InRange(segment.MinChests, segment.MaxChests), segment.SegmentId);

                    HashSet<MaterialType> allowed = AllowedConnectorMaterials(segment);
                    foreach (PlannedChest chest in chests)
                    {
                        Assert.That(chest.CountOf(MaterialType.MagicStone), Is.Zero, segment.SegmentId);
                        Assert.That(Total(new[] { chest }), Is.InRange(2, 4), segment.SegmentId);
                        Assert.That(chest.Materials.All(m => allowed.Contains(m.Material)), Is.True, segment.SegmentId);
                    }
                }

                // 탄약 하향 구간 (level-design 4장)
                Assert.That(ChestsOf(plan, ChestPointKind.Connector, "Z3-Z9").Count, Is.EqualTo(1));
            }
        }

        [Test]
        public void Certificate_InOneChestOfTargetZone()
        {
            LootGenerator generator = new LootGenerator(_rules);
            QuestLootRequest[] quests = { new QuestLootRequest(QuestLootKind.ZoneCertificate, "cert_z1", "Z1") };

            for (int seed = 0; seed < 200; seed++)
            {
                LootPlan plan = generator.Generate(seed, _points, quests);

                List<PlannedChest> holders = plan.Chests.Where(c => c.QuestItemIds.Contains("cert_z1")).ToList();
                Assert.That(holders.Count, Is.EqualTo(1));
                Assert.That(holders[0].Kind, Is.EqualTo(ChestPointKind.Zone));
                Assert.That(holders[0].OwnerId, Is.EqualTo("Z1"));
                Assert.That(holders[0].Materials.Count, Is.GreaterThan(0), "인증 상자에도 재료가 들어 있어야 함");
                Assert.That(ChestsOf(plan, ChestPointKind.Zone, "Z1").Count, Is.EqualTo(3), "상자를 새로 만들지 않음");
            }
        }

        [Test]
        public void Treasure_OnlyInSpawnedChestsBetweenTwoZones()
        {
            LootGenerator generator = new LootGenerator(_rules);
            QuestLootRequest[] quests =
            {
                new QuestLootRequest(QuestLootKind.ZoneCertificate, "cert_z1", "Z1"),
                new QuestLootRequest(QuestLootKind.Treasure, "treasure", "Z1", "Z9")
            };
            int withoutQuests = generator.Generate(0, _points, null).Chests.Count;

            for (int seed = 0; seed < SeedCount; seed++)
            {
                LootPlan plan = generator.Generate(seed, _points, quests);

                List<PlannedChest> holders = plan.Chests.Where(c => c.QuestItemIds.Contains("treasure")).ToList();
                Assert.That(holders.Count, Is.EqualTo(1));

                PlannedChest holder = holders[0];
                bool inZone = holder.Kind == ChestPointKind.Zone && (holder.OwnerId == "Z1" || holder.OwnerId == "Z9");
                bool inSegment = holder.Kind == ChestPointKind.Connector && holder.OwnerId == "Z1-Z9";
                Assert.That(inZone || inSegment, Is.True, $"{holder.Kind} {holder.OwnerId}");
            }

            // 상자를 새로 만들지 않음: 존 상자 30개 + 구간 생성 수는 퀘스트와 무관
            Assert.That(generator.Generate(0, _points, quests).Chests.Count, Is.EqualTo(withoutQuests));
        }

        [Test]
        public void Recovery_CreatesFixedChestWithItemAndHerb()
        {
            _points.Add(new ChestPointInfo(ChestPointKind.RecoveryFixed, "Z4"));
            LootGenerator generator = new LootGenerator(_rules);
            QuestLootRequest[] quests = { new QuestLootRequest(QuestLootKind.Recovery, "recovery_z4", "Z4") };

            for (int seed = 0; seed < 200; seed++)
            {
                LootPlan plan = generator.Generate(seed, _points, quests);

                PlannedChest chest = plan.Chests.Single(c => c.Kind == ChestPointKind.RecoveryFixed);
                Assert.That(chest.PointIndex, Is.EqualTo(_points.Count - 1));
                Assert.That(chest.QuestItemIds, Is.EqualTo(new[] { "recovery_z4" }));
                Assert.That(chest.CountOf(MaterialType.HealingHerb), Is.InRange(1, 2));
                Assert.That(Total(new[] { chest }), Is.EqualTo(chest.CountOf(MaterialType.HealingHerb)));
            }

            // 퀘스트가 없으면 고정 상자를 만들지 않음
            Assert.That(generator.Generate(0, _points, null).Chests.Any(c => c.Kind == ChestPointKind.RecoveryFixed),
                Is.False);
        }

        [Test]
        public void MissingData_WarnsInsteadOfThrowing()
        {
            List<ChestPointInfo> fewPoints = new List<ChestPointInfo> { new ChestPointInfo(ChestPointKind.Zone, "Z1") };
            QuestLootRequest[] quests = { new QuestLootRequest(QuestLootKind.Recovery, "recovery_z4", "Z4") };

            LootPlan plan = new LootGenerator(_rules).Generate(1, fewPoints, quests);

            Assert.That(ChestsOf(plan, ChestPointKind.Zone, "Z1").Count, Is.EqualTo(1));
            Assert.That(plan.Warnings, Is.Not.Empty);
        }

        // 공급량 확인용 (game-spec 7.3). 존 1곳 · 커넥터 구간 1개 평균을 출력합니다.
        [Test]
        public void Supply_PrintAverages()
        {
            LootGenerator generator = new LootGenerator(_rules);
            float zoneTotal = 0f;
            float connectorChests = 0f;
            float connectorItems = 0f;

            for (int seed = 0; seed < SeedCount; seed++)
            {
                LootPlan plan = generator.Generate(seed, _points, null);
                zoneTotal += Total(plan.Chests.Where(c => c.Kind == ChestPointKind.Zone));
                List<PlannedChest> connectors = plan.Chests.Where(c => c.Kind == ChestPointKind.Connector).ToList();
                connectorChests += connectors.Count;
                connectorItems += Total(connectors);
            }

            float perZone = zoneTotal / SeedCount / _rules.Zones.Count;
            float perConnectorChest = connectorItems / connectorChests;
            float perSegment = connectorItems / SeedCount / _rules.Segments.Count;
            NUnit.Framework.TestContext.WriteLine(
                $"존 1곳 평균 {perZone:F2}개, 커넥터 상자 1개 평균 {perConnectorChest:F2}개, 구간 1개 평균 {perSegment:F2}개");
            NUnit.Framework.TestContext.WriteLine(
                $"존 2곳 + 구간 3개 ≈ {perZone * 2 + perSegment * 3:F1}개, 존 3곳 + 구간 4개 ≈ {perZone * 3 + perSegment * 4:F1}개 (엘리트 주머니 제외)");

            Assert.That(perConnectorChest, Is.EqualTo(2.8f).Within(0.1f));
        }

        private static List<ChestPointInfo> CreatePoints(StageLootRules rules, int zonePoints, int connectorPoints)
        {
            List<ChestPointInfo> points = new List<ChestPointInfo>();
            foreach (ZoneLootDefinition zone in rules.Zones)
            {
                for (int i = 0; i < zonePoints; i++)
                {
                    points.Add(new ChestPointInfo(ChestPointKind.Zone, zone.ZoneId));
                }
            }

            foreach (ConnectorSegmentDefinition segment in rules.Segments)
            {
                for (int i = 0; i < connectorPoints; i++)
                {
                    points.Add(new ChestPointInfo(ChestPointKind.Connector, segment.SegmentId));
                }
            }

            return points;
        }

        private HashSet<MaterialType> AllowedConnectorMaterials(ConnectorSegmentDefinition segment)
        {
            HashSet<MaterialType> allowed = new HashSet<MaterialType> { MaterialType.HealingHerb };
            foreach (string zoneId in new[] { segment.ZoneA, segment.ZoneB })
            {
                ZoneLootDefinition zone = _rules.FindZone(zoneId);
                allowed.Add(zone.Main);
                allowed.Add(zone.Sub1);
                allowed.Add(zone.Sub2);
            }

            allowed.Remove(MaterialType.MagicStone);
            return allowed;
        }

        private static List<PlannedChest> ChestsOf(LootPlan plan, ChestPointKind kind, string ownerId)
        {
            return plan.Chests.Where(c => c.Kind == kind && c.OwnerId == ownerId).ToList();
        }

        private static int Total(IEnumerable<PlannedChest> chests)
        {
            return chests.Sum(c => c.Materials.Sum(m => m.Amount));
        }

        private static int Count(IEnumerable<PlannedChest> chests, MaterialType material)
        {
            return chests.Sum(c => c.CountOf(material));
        }

        private static string Describe(LootPlan plan)
        {
            return string.Join("\n", plan.Chests.Select(c =>
                $"{c.PointIndex} {c.Kind} {c.OwnerId} [{string.Join(", ", c.Materials)}] [{string.Join(", ", c.QuestItemIds)}]"));
        }
    }
}
