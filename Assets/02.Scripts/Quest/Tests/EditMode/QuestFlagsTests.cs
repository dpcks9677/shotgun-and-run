using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ShotgunAndRun.Quest.Tests
{
    public sealed class QuestFlagsTests
    {
        private const string MapMetaPath = "Assets/04.Sprites/Map/map-meta.json";

        [System.Serializable]
        private class MapMeta
        {
            public string[] zones;
        }

        [Test]
        public void MapReveal_EveryMapZoneHasKnownFlag()
        {
            MapMeta meta = JsonUtility.FromJson<MapMeta>(AssetDatabase.LoadAssetAtPath<TextAsset>(MapMetaPath).text);

            foreach (string zone in meta.zones)
            {
                Assert.IsTrue(QuestFlags.IsKnown(QuestFlags.MapReveal(zone)), $"{zone}의 지도 공개 플래그 상수가 없습니다.");
            }
        }
    }
}
