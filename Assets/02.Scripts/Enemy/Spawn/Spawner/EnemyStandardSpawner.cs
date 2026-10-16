using System.Collections;
using ShotgunAndRun.Core;
using UnityEngine;

public class EnemyStandardSpawner : EnemySpawner
{
    [SerializeField] private EnemyChunkPreset _chunkPreset;
    [SerializeField] private float _scoutRange = 15f;
    private float _respawnInterval = 240f;
    private float _respawnDelay = 0.3f;
    private float _respawnRevealDuration = 1.5f;
    private const int MaxSpawnWaves = 3;
    private RunSession _runSession;
    private float _nextRespawnTime = float.PositiveInfinity;
    private int _spawnWaveCount;
    protected override EnemyStateMode Mode => EnemyStateMode.Standard;
    public EnemyChunkPreset ChunkPreset => _chunkPreset;

    public void SetChunkPreset(EnemyChunkPreset preset)
    {
        _chunkPreset = preset;
    }

    protected override void Awake()
    {
        base.Awake();
        SetSpawnInitializer(new StandardSpawnerInitializer(_scoutRange));
    }

    protected override void Start()
    {
        base.Start();

        EnemyChunkDistributor distributor = GetComponentInParent<EnemyChunkDistributor>();
        if (distributor != null && !distributor.IsReady)
        {
            Debug.LogError("[EnemyStandardSpawner] Chunk 배분이 완료되지 않았습니다.", this);
            return;
        }

        if (_chunkPreset == null)
        {
            Debug.LogError("[EnemyStandardSpawner] Chunk Preset이 없습니다.", this);
            return;
        }

        EnemyChunk chunk = _chunkPreset.Chunk;

        if (chunk.Entries.Count == 0)
        {
            Debug.Log($"[EnemyStandardSpawner.cs : Start] - Chunk에 할당된 Enemy Entry들이 없습니다.");
            return;
        }

        _runSession = GameManager.Instance?.CurrentRun;
        _spawnWaveCount = 1;
        SpawnChunkInstantly(chunk);
    }

    private float PlayTime => _runSession != null ? _runSession.Elapsed : Time.time;

    private void Update()
    {
        if (_spawnWaveCount == 0 || _spawnWaveCount >= MaxSpawnWaves ||
            _runSession != null && _runSession.IsEnded || PlayTime < _nextRespawnTime)
            return;

        _spawnWaveCount++;
        _nextRespawnTime = float.PositiveInfinity;
        StartCoroutine(RespawnChunk(_chunkPreset.Chunk));
    }

    private void SpawnChunkInstantly(EnemyChunk chunk)
    {
        foreach (EnemyEntry entry in chunk.Entries)
        {
            for (int i = 0; i < entry.Count; i++)
                SpawnEntry(entry, 0f);
        }
    }

    private IEnumerator RespawnChunk(EnemyChunk chunk)
    {
        DebugSpawnPositions.Clear();
        foreach (EnemyEntry entry in chunk.Entries)
        {
            for (int i = 0; i < entry.Count; i++)
            {
                if (_runSession != null && _runSession.IsEnded) yield break;
                if (!SpawnEntry(entry, _respawnRevealDuration)) continue;

                float nextSpawnTime = PlayTime + _respawnDelay;
                while (PlayTime < nextSpawnTime)
                    yield return null;
            }
        }
    }

    private bool SpawnEntry(EnemyEntry entry, float revealDuration)
    {
        bool found = EnemySpawnerPositionUtils.TryGetRandomSpawnPositionInArea(transform.position,
            entry.EnemyType, SpawnAreaSizeVector, out Vector2 spawnPosition,
            ObstacleLayerMask, GroundTilemap);
        if (!found) return false;

        Enemy enemy = SpawnEnemy(entry.EnemyType, spawnPosition, revealDuration);
        if (enemy == null) return false;

        if (_spawnWaveCount < MaxSpawnWaves)
        {
            EnemyHealth health = enemy.EnemyHealth;
            int spawnWave = _spawnWaveCount;
            void HandleDeath()
            {
                health.DeathEvent -= HandleDeath;
                if (spawnWave == _spawnWaveCount && float.IsPositiveInfinity(_nextRespawnTime))
                    _nextRespawnTime = PlayTime + _respawnInterval;
            }

            health.DeathEvent += HandleDeath;
        }
        DebugSpawnPositions.Add((spawnPosition, entry.EnemyType));
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        DrawSpawnerGizmos();

        Gizmos.color = Color.cyan;

        if (Application.isPlaying)
        {
            foreach (var spawn in DebugSpawnPositions)
                Gizmos.DrawWireSphere(spawn.Item1, _scoutRange);
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, _scoutRange);
        }
    }
}
