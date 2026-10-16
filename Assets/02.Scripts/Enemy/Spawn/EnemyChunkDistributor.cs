using System.Collections.Generic;
using UnityEngine;

public sealed class EnemyChunkDistributor : MonoBehaviour
{
    [SerializeField] private EnemyChunkDistributionSO _distribution;

    public EnemyChunkDistributionSO Distribution => _distribution;
    public bool IsReady { get; private set; }

    private void Awake()
    {
        IsReady = false;
        if (!TryDistribute(out string error))
            Debug.LogError($"[EnemyChunkDistributor] {error}", this);
    }

    public EnemyStandardSpawner[] GetSpawners()
    {
        var spawners = new List<EnemyStandardSpawner>();
        foreach (EnemyStandardSpawner spawner in GetComponentsInChildren<EnemyStandardSpawner>())
        {
            if (spawner.enabled && spawner.GetComponentInParent<EnemyChunkDistributor>() == this)
                spawners.Add(spawner);
        }
        return spawners.ToArray();
    }

    public bool TryDistribute(out string error)
    {
        if (Application.isPlaying && IsReady)
        {
            error = "이번 런의 청크는 이미 결정되어 있습니다.";
            return false;
        }
        if (_distribution == null)
        {
            error = "Chunk Distribution 설정 SO가 없습니다.";
            return false;
        }

        EnemyStandardSpawner[] spawners = GetSpawners();
        if (!_distribution.TryCreateAssignments(spawners.Length, out EnemyChunkPreset[] assignments, out error))
            return false;

        for (int i = 0; i < spawners.Length; i++)
            spawners[i].SetChunkPreset(assignments[i]);
        IsReady = true;
        return true;
    }
}
