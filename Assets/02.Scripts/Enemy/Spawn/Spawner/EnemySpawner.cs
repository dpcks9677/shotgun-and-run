using System;
using System.Collections.Generic;
using ShotgunAndRun.Enemy;
using ShotgunAndRun.Player;
using UnityEngine;
using UnityEngine.Tilemaps;

public abstract class EnemySpawner : MonoBehaviour
{
    // 모든 스포너 공통 필수 참조 요소
    [SerializeField] private EnemyDataTableSO _enemyDataTable;  // Enemy 스폰에 필요한 데이터 테이블 접근

    private Transform _playerTransform;                         // Enemy 스폰 이후 Player 위치 전달 용도
    protected Transform PlayerTransform => _playerTransform;

    private Tilemap _groundTilemap;                             // Enemy 스폰 가능 여부 중 Ground 체크를 위한 용도
    protected Tilemap GroundTilemap => _groundTilemap;

    private LayerMask _obstacleLayerMask; // Enemy 스폰 가능 여부 중 장애물 체크를 위한 용도
    protected LayerMask ObstacleLayerMask => _obstacleLayerMask;
    private static readonly string[] ObstacleLayerNames = { "Default", "Enemy", "EnemyBody", "EnemyHitbox", "Player", "Wall", "Water", "Obstacle" };

    // 모든 스폰 관련 필드
    private IEnemySpawnInitializer _spawnInitializer;           // Enemy 스폰 이후 Enemy 초기화를 위한 인터페이스

    // Todo: 이거 EnemyStateMode를 상태 모드가 아니라
    //       MapZoneMode처럼 아예 따로 구분해야되려나?
    protected abstract EnemyStateMode Mode                      // 스포너의 모드
    {
        get;
    }
    [SerializeField] private Vector2 _spawnAreaSizeVector = new Vector2(10f, 10f);
    protected virtual Vector2 SpawnAreaSizeVector => _spawnAreaSizeVector;

    // 습격 포탈 프리팹 (비어 있으면 포탈 없이 기존처럼 스폰)
    [SerializeField] private RaidPortal _portalPrefab;
    protected RaidPortal RaidPortal => _portalPrefab;

    // 스폰 직후 알림 (퀘스트 대상 적 표시 등, 스포너 밖에서 생성된 적을 구분할 때 사용)
    public event Action<Enemy> EnemySpawned;

    protected BoxCollider2D GetMovementCollider(EnemyType type)
    {
        return _enemyDataTable.ReadEnemyData(type).prefab.GetComponent<BoxCollider2D>();
    }

    // 소환 제한 판정 범위 시각화 --> 기즈모 Field
    private readonly List<(Vector2, EnemyType)> _debugSpawnPositions = new();
    protected List<(Vector2, EnemyType)> DebugSpawnPositions => _debugSpawnPositions;

    protected virtual void Awake()
    {
        if (_enemyDataTable == null)
        {
            Debug.LogError($"[EnemySpawner.cs : Awake] - _enemyDataTable is null");
        }
    }

    protected virtual void Start()
    {
        // 1. Ground Tile 맵 확인을 위한 캐싱
        _groundTilemap = GroundTilemapProvider.Ground;

        // 2. Player Transform 캐싱
        if (Player.PlayerInstance == null)
        {
            Debug.LogError("[EnemySpawner.cs : InitializePlayerTransform] - Player 인스턴스를 찾을 수 없습니다.");
            return;
        }

        _playerTransform = Player.PlayerInstance.transform;

        // 3. Obstacle Layer Mask 초기화
        _obstacleLayerMask = LayerMask.GetMask(ObstacleLayerNames);
    }

    protected void SetSpawnInitializer(IEnemySpawnInitializer spawnInitializer)
    {
        _spawnInitializer = spawnInitializer;
    }

    protected Enemy SpawnEnemy(EnemyType type, Vector2 spawnPosition, float revealDuration = 0f)
    {
        // 1. 타입에 맞는 Enemy Data 가져오기
        EnemyData enemyData = _enemyDataTable.ReadEnemyData(type);

        if (enemyData == null)
        {
            Debug.LogError($"[EnemySpawner.cs : SpawnEnemy] - enemyData is null");
            return null;
        }

        // 2. 해당 타입 Enemy 생성 및 초기화
        Enemy newEnemy = EnemyLayer.Acquire(enemyData.prefab, spawnPosition);
        newEnemy.Initialize(enemyData);

        _spawnInitializer.InitializeSpawnedEnemy(newEnemy, spawnPosition,
            _playerTransform); // 정찰 거리, 스폰 위치, 적 센서에 Player Transform

        // 3. Enemy FSM 연결 및 초기화
        EnemyStateMachine fsm = newEnemy.GetComponent<EnemyStateMachine>();
        fsm.SetMode(Mode);
        if (revealDuration > 0f)
            newEnemy.BeginSpawnReveal(revealDuration);
        else
            fsm.Begin();

        EnemySpawned?.Invoke(newEnemy);

        return newEnemy;
    }

    private void OnDrawGizmosSelected()
    {
        DrawSpawnerGizmos();
    }

    protected void DrawSpawnerGizmos()
    {
        // 1. Spawn 영역 색상
        Gizmos.color = Color.green;

        Gizmos.DrawWireCube(transform.position, new Vector3(SpawnAreaSizeVector.x, SpawnAreaSizeVector.y, 0f));

        // 2. 충돌 반경 표시 (빨간색)
        Gizmos.color = Color.red;

        float checkRadius = EnemySpawnerPositionUtils.GetEnemyColliderRadius(EnemyType.Normal);

        if (Application.isPlaying)
        {
            foreach ((Vector2, EnemyType) data in _debugSpawnPositions)
            {
                checkRadius = EnemySpawnerPositionUtils.GetEnemyColliderRadius(data.Item2);
                Gizmos.DrawWireSphere(data.Item1, checkRadius);
            }
        }
        else
        {
            // 실행 전에는 Spawner 중심에서 반경미리 보기
            Gizmos.DrawWireSphere(transform.position, checkRadius);
        }
    }
}
