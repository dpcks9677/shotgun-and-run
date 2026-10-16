using System;
using System.Collections;
using System.Collections.Generic;
using ShotgunAndRun.Enemy;
using UnityEngine;

public class EnemyDestructionSpawner : EnemySpawner
{
    [SerializeField] private List<EnemyWave> _enemyWaves;

    [Tooltip("비우면 포탈 위치를 무작위로 정합니다. 지정하면 스포너 기준 이 위치에 포탈을 열고, 웨이브의 청크마다 차례로 씁니다 (청크가 더 많으면 처음부터 다시)")]
    [SerializeField] private Vector2[] _portalOffsets = new Vector2[0];
    private bool _isWaveStarted;
    protected override EnemyStateMode Mode => EnemyStateMode.Destruction;

    // 플레이어 카메라
    private Camera _playerCamera;

    // 웨이브 정보
    private int _currentWaveIndex = 0;
    private int _currentWaveKillCount;
    private int _currentWaveEnemyCount;
    private float _currentWaveCoolTime;
    private bool _isNextWavePreparing = false;
    private int _spawningChunkCount;
    private int _aliveEnemyCount;
    private const float EnemySpawnDelay = 0.7f;

    // 웨이브 종료 필드
    private bool _isCompleted;
    public event Action CompletedEvent;

    // 기즈모 테스트
    private readonly List<Rect> _lastWaveSpawnAreas = new List<Rect>();
    private readonly List<Vector2> _lastPortalPositions = new List<Vector2>();
    private readonly List<Bounds> _lastPortalBlockBounds = new List<Bounds>();
    private readonly List<EnemyPortalSpawnCheck> _tankerSpawnChecks = new List<EnemyPortalSpawnCheck>();

    protected override void Awake()
    {
        base.Awake();
        SetSpawnInitializer(new DestructionSpawnerInitializer());
    }

    protected override void Start()
    {
        base.Start();

        _isWaveStarted = false;

        InitializePlayerCamera();

        // 1. 웨이브 데이터 확인
        if (_enemyWaves == null || _enemyWaves.Count == 0)
        {
            Debug.LogError($"[EnemyDestructionSpawner.cs] - 설정된 웨이브가 없습니다.");
        }

        // 2. 플레이어 참조 확인
        if (PlayerTransform == null)
        {
            Debug.LogError($"[EnemyDestructionSpawner.cs] - 플레이어 참조가 없습니다.");
            return;
        }
    }

    private void Update()
    {
        // 웨이브 진행 여부에 따라 Update 확인
        if (!_isWaveStarted || _spawningChunkCount > 0) return;

        // 1. 마지막 웨이브 체크 (다음 웨이브가 있을 경우에만)
        bool hasNextWave = _currentWaveIndex < _enemyWaves.Count - 1;
        if (!hasNextWave)
        {
            if (_aliveEnemyCount == 0)
            {
                CompleteBattle();
            }

            return;
        }

        // 2. 현재 웨이브 정보 확인
        EnemyWave currentWave = _enemyWaves[_currentWaveIndex];

        // 2.1 다음 웨이브를 준비 중이 아니라면
        if (!_isNextWavePreparing)
        {
            bool isWaveZero = _currentWaveEnemyCount == 0;
            bool isRatioExceeded = !isWaveZero &&
                                   (float)_currentWaveKillCount / _currentWaveEnemyCount >= currentWave.NextWaveRatio;

            if (!(isWaveZero || isRatioExceeded)) return;

            _isNextWavePreparing = true;
            _currentWaveCoolTime = 0f;
        }

        // 2.2 다음 웨이브를 준비 중이라면
        _currentWaveCoolTime += Time.deltaTime;

        // 3. 쿨타임 만족하면 다음 웨이브 시작
        if (_currentWaveCoolTime < currentWave.NextWaveDelay) return;

        _isNextWavePreparing = false;
        _currentWaveIndex++;

        StartWave(_currentWaveIndex);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_isWaveStarted) return;

        bool isPlayerCollider = other.transform == PlayerTransform || other.transform.IsChildOf(PlayerTransform);

        if (!isPlayerCollider) return;

        _isWaveStarted = true;

        Debug.Log("[EnemyDestructionSpawner.cs : OnTriggerEnter2D] - 플레이어 입장~ 스포너로부터 웨이브 스폰 시작");

        StartWave(_currentWaveIndex);
    }

    private void StartWave(int waveIndex)
    {
        // 0. 현재 Wave 정보 초기화
        _currentWaveKillCount = 0;
        _currentWaveEnemyCount = 0;
        _currentWaveCoolTime = 0f;
        _spawningChunkCount = 0;

        EnemyChunk[] chunks = _enemyWaves[waveIndex].EnemyChunks;
        List<Vector2> portalPositions = _portalOffsets.Length > 0
            ? GetFixedPortalPositions(chunks.Length)
            : GetRandomPortalPositions(chunks.Length);
        _lastPortalPositions.Clear();
        _lastPortalPositions.AddRange(portalPositions);
        _lastPortalBlockBounds.Clear();
        _tankerSpawnChecks.Clear();

        // 3. 각 청크 별로 포탈에 Enemy 순차적으로 스폰
        for (int chunkIdx = 0; chunkIdx < chunks.Length; chunkIdx++)
        {
            EnemyChunk chunk = chunks[chunkIdx];
            Vector2 portalPosition = portalPositions[chunkIdx];

            _spawningChunkCount++;
            StartCoroutine(SpawnChunk(chunk, portalPosition, waveIndex));
        }
    }

    private List<Vector2> GetFixedPortalPositions(int chunkCount)
    {
        List<Vector2> portalPositions = new List<Vector2>();

        for (int chunkIdx = 0; chunkIdx < chunkCount; chunkIdx++)
        {
            portalPositions.Add((Vector2)transform.position + _portalOffsets[chunkIdx % _portalOffsets.Length]);
        }

        return portalPositions;
    }

    // 스폰 가능 영역 안에서 랜덤 포탈 위치 생성
    private List<Vector2> GetRandomPortalPositions(int chunkCount)
    {
        // 1. 스폰 가능 영역 구하기
        List<Rect> spawnAreas =
            EnemySpawnerPositionUtils.GetSpawnableAreas(transform.position, SpawnAreaSizeVector,
                PlayerTransform.position);

        _lastWaveSpawnAreas.Clear();
        _lastWaveSpawnAreas.AddRange(spawnAreas);

        foreach (Rect area in spawnAreas)
        {
            Debug.Log($"후보 영역: {area}");
        }

        // 2. 스폰 가능 영역 내 랜덤 포탈 위치 생성
        return EnemySpawnerPortalUtils.GetRandomPortalPositions(spawnAreas, chunkCount, ObstacleLayerMask,
            GroundTilemap);
    }

    private void InitializePlayerCamera()
    {
        if (_playerCamera != null) return;

        _playerCamera = Camera.main;

        if (_playerCamera == null)
        {
            Debug.LogError($"[EnemyDestructionSpawner.cs : InitializePlayerCamera] - MainCamera를 찾을 수 없습니다.");
        }
    }

    private IEnumerator SpawnChunk(EnemyChunk chunk, Vector2 portalPosition, int waveIndex)
    {
        // 포탈이 솟아오른 뒤에 스폰을 시작한다
        RaidPortal portal = EnemySpawnerPortalUtils.OpenPortal(portalPosition, RaidPortal, PlayerTransform);

        if (portal != null)
        {
            _lastPortalBlockBounds.Add(portal.BlockBounds);
            yield return portal.WaitForOpen();
        }

        foreach (EnemyEntry entry in chunk.Entries)
        {
            BoxCollider2D movementCollider = GetMovementCollider(entry.EnemyType);
            for (int i = 0; i < entry.Count; i++)
            {
                EnemyPortalSpawnCheck tankerCheck = null;
                if (entry.EnemyType == EnemyType.Tanker)
                {
                    Vector2 origin = portal != null
                        ? EnemySpawnerPortalUtils.GetPreferredSpawnPosition(portal, movementCollider)
                        : portalPosition;
                    tankerCheck = new EnemyPortalSpawnCheck
                    {
                        Origin = origin,
                        OriginValid = EnemySpawnerPositionUtils.IsPossibleSpawnToPosition(
                            origin, entry.EnemyType, movementCollider, ObstacleLayerMask, GroundTilemap)
                    };
                    _tankerSpawnChecks.Add(tankerCheck);
                }

                Vector2 spawnPosition;
                bool found;

                do
                {
                    found = portal != null
                        ? EnemySpawnerPortalUtils.TryGetSpawnPositionNearPortal(
                            portal, entry.EnemyType, movementCollider, ObstacleLayerMask, GroundTilemap, out spawnPosition)
                        : EnemySpawnerPositionUtils.TryGetRandomSpawnPositionInArea(
                            portalPosition, entry.EnemyType, EnemySpawnerPortalUtils.NoPortalSpawnSize,
                            out spawnPosition, ObstacleLayerMask, GroundTilemap);

                    if (!found) yield return new WaitForSeconds(EnemySpawnDelay);
                } while (!found);

                if (tankerCheck != null)
                {
                    tankerCheck.SpawnPosition = spawnPosition;
                    tankerCheck.Found = true;
                }

                Enemy enemy = SpawnEnemy(entry.EnemyType, spawnPosition);
                EnemyHealth enemyHealth = enemy.EnemyHealth;
                enemyHealth.DeathEvent += () => UpdateWaveKillCount(waveIndex);
                _currentWaveEnemyCount++;
                _aliveEnemyCount++;

                yield return new WaitForSeconds(EnemySpawnDelay);
            }
        }

        // Chunk 생성 종료 후 포탈 닫기
        if (portal != null)
        {
            portal.Close();
        }

        _spawningChunkCount--;
    }

    public void UpdateWaveKillCount(int waveIndex)
    {
        _aliveEnemyCount--;

        if (_currentWaveIndex != waveIndex) return;

        _currentWaveKillCount++;
        Debug.Log($"[Wave {waveIndex + 1} : 처치 정보] - {_currentWaveKillCount}/{_currentWaveEnemyCount}");
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying)
            return;

        foreach (Rect area in _lastWaveSpawnAreas)
        {
            Vector3 center = new Vector3(
                area.center.x,
                area.center.y,
                transform.position.z);

            Vector3 size = new Vector3(
                area.width,
                area.height,
                0f);

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(center, size);
        }

        if (_lastPortalBlockBounds.Count > 0)
            EnemySpawnerPortalUtils.DrawSpawnGizmos(_lastPortalPositions, _lastPortalBlockBounds,
                _tankerSpawnChecks, GetMovementCollider(EnemyType.Tanker));
    }

    private void CompleteBattle()
    {
        if (_isCompleted) return;

        _isCompleted = true;

        Debug.Log($"[EnemyDestructionSpawner] - 전투 완료");

        CompletedEvent?.Invoke();
    }
}
