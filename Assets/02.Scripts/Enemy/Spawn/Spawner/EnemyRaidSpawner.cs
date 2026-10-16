using System.Collections;
using System.Collections.Generic;
using ShotgunAndRun.Core;
using ShotgunAndRun.Enemy;
using UnityEngine;

public class EnemyRaidSpawner : EnemySpawner
{
    [SerializeField] private RaidRoundDataTableSO _raidRoundDataTable;
    protected override EnemyStateMode Mode => EnemyStateMode.Destruction;
    private RaidTimer _raidTimer;

    [SerializeField] private EnemyChunk[] _chunks;
    private int _currentChunkIndex;
    private readonly List<Vector2> _lastPortalPositions = new();
    private readonly List<Bounds> _lastPortalBlockBounds = new();
    private readonly List<EnemyPortalSpawnCheck> _tankerSpawnChecks = new();

    // SpawnChunk 관련 필드
    private float _enemySpawnDelay = 0.7f;

    // 플레이어 카메라
    protected override Vector2 SpawnAreaSizeVector => new Vector2(45f, 45f);

    protected override void Awake()
    {
        base.Awake();

        SetSpawnInitializer(new DestructionSpawnerInitializer());
    }

    protected override void Start()
    {
        base.Start();

        // 1. GameManager의 runSession과 연결 후 RaidTimer 연결
        RunSession runSession = GameManager.Instance.CurrentRun;

        if (runSession == null)
        {
            Debug.Log("[EnemyRaidSpawner.cs : Start] - runSession is null");
            return;
        }

        _raidTimer = runSession.RaidTimer;
        _raidTimer.RaidEvent += OnRaidEventStart;
        _currentChunkIndex = 0;
    }

    private void OnRaidEventStart()
    {
        Debug.Log("[EnemyRaidSpawner.cs : OnRaidEventStart] - 습격 시작이 되어 스포너가 작동합니다.");

        // 0. 현재 chunk 설정
        _chunks = _raidRoundDataTable.datas[_currentChunkIndex].enemyChunks;

        // 1. 플레이어 기준으로 스폰 가능한 영역 구하기
        List<Rect> spawnAreas = EnemySpawnerPositionUtils.GetSpawnableAreas(PlayerTransform.position,
            SpawnAreaSizeVector, PlayerTransform.position);

        // 2. 스폰 가능 영역 내 랜덤 포탈 위치 생성
        List<Vector2> portalPositions =
            EnemySpawnerPortalUtils.GetRandomPortalPositions(spawnAreas, _chunks.Length, ObstacleLayerMask, GroundTilemap);
        _lastPortalPositions.Clear();
        _lastPortalPositions.AddRange(portalPositions);
        _lastPortalBlockBounds.Clear();
        _tankerSpawnChecks.Clear();

        if (portalPositions.Count < _chunks.Length)
        {
            Debug.LogWarning($"[EnemyRaidSpawner] 포탈 위치를 {_chunks.Length}개 요청했지만 {portalPositions.Count}개만 찾았습니다.");
        }

        // 3. 각 청크 별로 포탈에 Enemy 순차적으로 스폰
        for (int chunkIdx = 0; chunkIdx < _chunks.Length && chunkIdx < portalPositions.Count; chunkIdx++)
        {
            EnemyChunk chunk = _chunks[chunkIdx];
            Vector2 portalPosition = portalPositions[chunkIdx];

            StartCoroutine(SpawnChunk(chunk, portalPosition));
        }

        // 준비된 라운드를 다 넘기면 마지막 라운드를 반복한다
        _currentChunkIndex = Mathf.Min(_currentChunkIndex + 1, _raidRoundDataTable.datas.Length - 1);
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || PlayerTransform == null) return;

        Vector2 playerPosition = PlayerTransform.position;
        List<Rect> spawnAreas = EnemySpawnerPositionUtils.GetSpawnableAreas(
            playerPosition, SpawnAreaSizeVector, playerPosition);

        Gizmos.color = Color.green;
        foreach (Rect area in spawnAreas)
        {
            Gizmos.DrawWireCube(area.center, area.size);
        }

        // 플레이어 중심 20U 영역에 여유 1U를 더한 제외 영역.
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(playerPosition, new Vector3(22f, 22f, 0f));

        if (_lastPortalBlockBounds.Count > 0)
            EnemySpawnerPortalUtils.DrawSpawnGizmos(_lastPortalPositions, _lastPortalBlockBounds,
                _tankerSpawnChecks, GetMovementCollider(EnemyType.Tanker));
    }

    private void OnDestroy()
    {
        if (_raidTimer != null)
        {
            _raidTimer.RaidEvent -= OnRaidEventStart;
        }
    }

    private IEnumerator SpawnChunk(EnemyChunk chunk, Vector2 portalPosition)
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
                Vector2 spawnOrigin = portal != null
                    ? EnemySpawnerPortalUtils.GetPreferredSpawnPosition(portal, movementCollider)
                    : portalPosition;
                EnemyPortalSpawnCheck tankerCheck = null;
                if (entry.EnemyType == EnemyType.Tanker)
                {
                    tankerCheck = new EnemyPortalSpawnCheck
                    {
                        Origin = spawnOrigin,
                        OriginValid = EnemySpawnerPositionUtils.IsPossibleSpawnToPosition(
                            spawnOrigin, entry.EnemyType, movementCollider, ObstacleLayerMask, GroundTilemap)
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

                    if (!found) yield return new WaitForSeconds(_enemySpawnDelay);
                } while (!found);

                if (tankerCheck != null)
                {
                    tankerCheck.SpawnPosition = spawnPosition;
                    tankerCheck.Found = true;
                }

                Enemy enemy = SpawnEnemy(entry.EnemyType, spawnPosition);

                yield return new WaitForSeconds(_enemySpawnDelay);
            }
        }

        // Chunk 생성 종료 후 포탈 닫기
        if (portal != null)
        {
            portal.Close();
        }
    }
}
