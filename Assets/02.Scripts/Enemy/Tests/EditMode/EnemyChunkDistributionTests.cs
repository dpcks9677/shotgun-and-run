using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class EnemyChunkDistributionTests
{
    [TestCase(0, 0, 0, 0, 0)]
    [TestCase(40, 20, 12, 6, 2)]
    [TestCase(70, 35, 21, 11, 3)]
    public void Counts_PreserveTotalAndResolveTies(int total, int easy, int normal, int hard, int veryHard)
    {
        Assert.That(EnemyChunkDistributionSO.CalculateCounts(total, new[] { 50, 30, 15, 5 }),
            Is.EqualTo(new[] { easy, normal, hard, veryHard }));
    }

    [Test]
    public void Counts_ForEveryMapSizeStayWithinOneOfIdeal()
    {
        int[] percentages = { 50, 30, 15, 5 };
        for (int total = 1; total <= 200; total++)
        {
            int[] counts = EnemyChunkDistributionSO.CalculateCounts(total, percentages);
            int sum = 0;
            for (int i = 0; i < counts.Length; i++)
            {
                sum += counts[i];
                Assert.That(Math.Abs(counts[i] - total * percentages[i] / 100d), Is.LessThan(1d));
            }
            Assert.That(sum, Is.EqualTo(total));
        }
    }

    [Test]
    public void Counts_ZeroWeightGetsNoSpawner()
    {
        Assert.That(EnemyChunkDistributionSO.CalculateCounts(71, new[] { 0, 100, 0, 0 }),
            Is.EqualTo(new[] { 0, 71, 0, 0 }));
    }

    [Test]
    public void Counts_InvalidTotalIsRejected()
    {
        Assert.Throws<ArgumentException>(() => EnemyChunkDistributionSO.CalculateCounts(70, new[] { 50, 30, 15, 4 }));
    }

    [Test]
    public void Assignments_UseRegisteredPresetsAndKeepExactQuota()
    {
        var distribution = AssetDatabase.LoadAssetAtPath<EnemyChunkDistributionSO>(
            "Assets/07.Datas/Enemy/StandardChunkDistribution.asset");
        Assert.That(distribution, Is.Not.Null);
        UnityEngine.Random.State state = UnityEngine.Random.state;
        try
        {
            UnityEngine.Random.InitState(123);
            Assert.That(distribution.TryCreateAssignments(70, out EnemyChunkPreset[] assigned, out string error),
                Is.True, error);
            int[] counts = new int[4];
            foreach (EnemyChunkPreset preset in assigned)
            {
                Assert.That(AssetDatabase.GetAssetPath(preset), Does.StartWith("Assets/07.Datas/Enemy/ChunkPresets/"));
                counts[(int)preset.Difficulty]++;
            }
            Assert.That(counts, Is.EqualTo(new[] { 35, 21, 11, 3 }));
        }
        finally { UnityEngine.Random.state = state; }
    }

    [Test]
    public void Assignments_MissingPoolIsRejectedWithoutPartialResult()
    {
        var distribution = ScriptableObject.CreateInstance<EnemyChunkDistributionSO>();
        try
        {
            Assert.That(distribution.TryCreateAssignments(70, out EnemyChunkPreset[] assigned, out _), Is.False);
            Assert.That(assigned, Is.Null);
        }
        finally { UnityEngine.Object.DestroyImmediate(distribution); }
    }

    [Test]
    public void Distributor_UsesOnlyActiveEnabledChildrenAndLeavesZonesUntouched()
    {
        var root = new GameObject("DistributionTest");
        UnityEngine.Random.State state = UnityEngine.Random.state;
        try
        {
            var connectors = new GameObject("Connectors");
            connectors.transform.SetParent(root.transform);
            var distributor = connectors.AddComponent<EnemyChunkDistributor>();
            var settings = new SerializedObject(distributor);
            settings.FindProperty("_distribution").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<EnemyChunkDistributionSO>(
                    "Assets/07.Datas/Enemy/StandardChunkDistribution.asset");
            settings.ApplyModifiedPropertiesWithoutUndo();

            for (int i = 0; i < 40; i++)
            {
                var child = new GameObject("Spawner" + i);
                child.transform.SetParent(connectors.transform);
                child.AddComponent<EnemyStandardSpawner>();
            }
            var disabled = new GameObject("Disabled");
            disabled.transform.SetParent(connectors.transform);
            disabled.AddComponent<EnemyStandardSpawner>().enabled = false;
            var inactive = new GameObject("Inactive");
            inactive.transform.SetParent(connectors.transform);
            inactive.AddComponent<EnemyStandardSpawner>();
            inactive.SetActive(false);
            var zones = new GameObject("Zones");
            zones.transform.SetParent(root.transform);
            var zoneSpawner = zones.AddComponent<EnemyStandardSpawner>();
            EnemyChunkPreset fixedPreset = AssetDatabase.LoadAssetAtPath<EnemyChunkPreset>(
                "Assets/07.Datas/Enemy/ChunkPresets/C01_BasicPack.asset");
            zoneSpawner.SetChunkPreset(fixedPreset);

            Assert.That(distributor.TryDistribute(out string error), Is.True, error);
            Assert.That(distributor.IsReady, Is.True);
            Assert.That(distributor.GetSpawners().Length, Is.EqualTo(40));
            int[] counts = new int[4];
            foreach (EnemyStandardSpawner spawner in distributor.GetSpawners())
                counts[(int)spawner.ChunkPreset.Difficulty]++;
            Assert.That(counts, Is.EqualTo(new[] { 20, 12, 6, 2 }));
            Assert.That(zoneSpawner.ChunkPreset, Is.SameAs(fixedPreset));
            Assert.That(disabled.GetComponent<EnemyStandardSpawner>().ChunkPreset, Is.Null);
            Assert.That(inactive.GetComponent<EnemyStandardSpawner>().ChunkPreset, Is.Null);
        }
        finally
        {
            UnityEngine.Random.state = state;
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
