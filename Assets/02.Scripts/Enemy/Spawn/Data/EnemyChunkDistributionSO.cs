using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EnemyChunkDistribution", menuName = "ShotgunAndRun/Enemy/Chunk Distribution")]
public class EnemyChunkDistributionSO : ScriptableObject
{
    [SerializeField] private List<EnemyChunkPreset> _presets = new();
    [SerializeField] private int _easyPercent = 50;
    [SerializeField] private int _normalPercent = 30;
    [SerializeField] private int _hardPercent = 15;
    [SerializeField] private int _veryHardPercent = 5;

    public int[] Percentages => new[] { _easyPercent, _normalPercent, _hardPercent, _veryHardPercent };
    public int PercentageTotal => _easyPercent + _normalPercent + _hardPercent + _veryHardPercent;

    public bool TryCreateAssignments(int spawnerCount, out EnemyChunkPreset[] assignments, out string error)
    {
        assignments = null;
        int[] percentages = Percentages;
        var pools = new List<EnemyChunkPreset>[percentages.Length];
        for (int i = 0; i < pools.Length; i++)
            pools[i] = new List<EnemyChunkPreset>();

        if (PercentageTotal != 100 || Array.Exists(percentages, value => value < 0 || value > 100))
        {
            error = "난이도 비율은 각각 0~100이고 합계는 100이어야 합니다.";
            return false;
        }

        var registered = new HashSet<EnemyChunkPreset>();
        foreach (EnemyChunkPreset preset in _presets)
        {
            if (preset == null || !registered.Add(preset) || preset.Chunk.Entries.Count == 0)
            {
                error = "프리셋 목록에 빈 참조, 중복 또는 빈 Chunk가 있습니다.";
                return false;
            }

            int difficulty = (int)preset.Difficulty;
            if (difficulty < 0 || difficulty >= pools.Length ||
                preset.Chunk.Entries.Exists(entry => entry == null || entry.Count <= 0))
            {
                error = $"{preset.name}의 난이도 또는 Enemy 개수가 올바르지 않습니다.";
                return false;
            }
            pools[difficulty].Add(preset);
        }

        for (int i = 0; i < percentages.Length; i++)
        {
            if (percentages[i] > 0 && pools[i].Count == 0)
            {
                error = $"{(EnemyChunkDifficulty)i} 난이도의 후보 프리셋이 없습니다.";
                return false;
            }
        }

        int[] counts = CalculateCounts(spawnerCount, percentages);
        assignments = new EnemyChunkPreset[spawnerCount];
        int index = 0;
        for (int difficulty = 0; difficulty < counts.Length; difficulty++)
        {
            for (int i = 0; i < counts[difficulty]; i++)
                assignments[index++] = pools[difficulty][UnityEngine.Random.Range(0, pools[difficulty].Count)];
        }

        for (int i = assignments.Length - 1; i > 0; i--)
        {
            int other = UnityEngine.Random.Range(0, i + 1);
            (assignments[i], assignments[other]) = (assignments[other], assignments[i]);
        }

        error = null;
        return true;
    }

    public static int[] CalculateCounts(int spawnerCount, int[] percentages)
    {
        if (spawnerCount < 0)
            throw new ArgumentOutOfRangeException(nameof(spawnerCount));

        int difficultyCount = Enum.GetValues(typeof(EnemyChunkDifficulty)).Length;
        if (percentages == null || percentages.Length != difficultyCount ||
            Array.Exists(percentages, value => value < 0 || value > 100))
            throw new ArgumentException("난이도별 0~100 비율이 필요합니다.", nameof(percentages));

        int percentageTotal = 0;
        foreach (int percentage in percentages)
            percentageTotal += percentage;
        if (percentageTotal != 100)
            throw new ArgumentException("비율 합계는 100이어야 합니다.", nameof(percentages));

        int[] counts = new int[difficultyCount];
        int[] remainders = new int[difficultyCount];
        int remaining = spawnerCount;
        for (int i = 0; i < difficultyCount; i++)
        {
            long weightedCount = (long)spawnerCount * percentages[i];
            counts[i] = (int)(weightedCount / 100);
            remainders[i] = (int)(weightedCount % 100);
            remaining -= counts[i];
        }

        // 소수 부분이 큰 순서로 배정합니다. 동률은 enum 순서로 처리합니다.
        while (remaining-- > 0)
        {
            int largest = 0;
            for (int i = 1; i < difficultyCount; i++)
                if (remainders[i] > remainders[largest]) largest = i;
            counts[largest]++;
            remainders[largest] = -1;
        }
        return counts;
    }
}
