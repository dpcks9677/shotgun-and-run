using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ShotgunAndRun.Loot.Tests
{
    // Stage1ChestPoints 프리팹에 칠한 상자 스폰 지점이 StageLootConfig 규칙과 맞는지 검사합니다 (level-design 4장).
    public sealed class Stage1ChestPointsTests
    {
        private const string PrefabPath = "Assets/03.Prefabs/Environment/Stage1ChestPoints.prefab";
        private const string ConfigPath = "Assets/07.Datas/StageLootConfig.asset";

        // game-spec 4.3: 존마다 후보 지점 5~6곳
        private const int MinZonePoints = 5;

        private GameObject _root;
        private StageLootRules _rules;
        private List<ChestPointPlacement> _points;

        [SetUp]
        public void SetUp()
        {
            _root = PrefabUtility.LoadPrefabContents(PrefabPath);
            _rules = AssetDatabase.LoadAssetAtPath<StageLootConfig>(ConfigPath).Rules;
            _points = new List<ChestPointPlacement>();
            foreach (ChestPointTilemap tilemap in _root.GetComponentsInChildren<ChestPointTilemap>())
            {
                tilemap.CollectPoints(_points);
            }
        }

        [TearDown]
        public void TearDown()
        {
            PrefabUtility.UnloadPrefabContents(_root);
        }

        [Test]
        public void OwnerIds_MatchConfig()
        {
            HashSet<string> zones = new HashSet<string>(_rules.Zones.Select(zone => zone.ZoneId));
            HashSet<string> segments = new HashSet<string>(_rules.Segments.Select(segment => segment.SegmentId));

            foreach (ChestPointPlacement point in _points)
            {
                HashSet<string> owners = point.Kind == ChestPointKind.Connector ? segments : zones;
                Assert.That(owners, Does.Contain(point.OwnerId), $"{point.Kind} 지점 {point.Position}의 소속이 규칙에 없습니다.");
            }
        }

        [Test]
        public void EveryZone_HasEnoughPoints()
        {
            foreach (ZoneLootDefinition zone in _rules.Zones)
            {
                int count = _points.Count(point => point.Kind == ChestPointKind.Zone && point.OwnerId == zone.ZoneId);
                Assert.That(count, Is.GreaterThanOrEqualTo(Mathf.Max(MinZonePoints, _rules.ZoneChestCount)), zone.ZoneId);
            }
        }

        [Test]
        public void EverySegment_HasMorePointsThanMaxChests()
        {
            // 최대 생성 수보다 지점이 많아야 런마다 위치가 바뀝니다.
            foreach (ConnectorSegmentDefinition segment in _rules.Segments)
            {
                int count = _points.Count(point => point.Kind == ChestPointKind.Connector && point.OwnerId == segment.SegmentId);
                Assert.That(count, Is.GreaterThan(segment.MaxChests), segment.SegmentId);
            }
        }

        [Test]
        public void Tilemaps_HoldOnlyChestPointTiles()
        {
            foreach (Tilemap tilemap in _root.GetComponentsInChildren<Tilemap>())
            {
                foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
                {
                    TileBase tile = tilemap.GetTile(cell);
                    Assert.That(tile == null || tile is ChestPointTile, $"{tilemap.name} {cell}: {tile}");
                }
            }
        }
    }
}
