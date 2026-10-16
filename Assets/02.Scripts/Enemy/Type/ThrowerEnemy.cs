using System.Collections;
using System.Collections.Generic;
using Consumable;
using UnityEngine;

public class ThrowerEnemy : Enemy
{
    [Header("Keep Distance")]
    private float _minimumDistance = 5.0f;

    private float _maximumDistance = 7.5f;

    [Header("Bomb Pattern")]
    [SerializeField, Min(1)] private int _minimumBombCount = 2;

    [SerializeField, Min(1)] private int _maximumBombCount = 3;
    [SerializeField, Min(0.1f)] private float _explosionRadius = 3f;
    [SerializeField, Min(0f)] private float _minimumBombSpacing = 4f;
    [SerializeField, Min(0f)] private float _bombScatterRadius = 6f;
    [SerializeField, Min(0.1f)] private float _fuseDuration = 1.5f;
    [SerializeField, Min(0.1f)] private float _attackInterval = 3f;
    [SerializeField] private LayerMask _targetLayer = 1 << 6;
    [SerializeField] private EnemyBomb _bombPrefab;
    [SerializeField] private GrenadeExplosionVFX _bombExplosionPrefab;
    [SerializeField] private float _bombWarningBlinkInterval = 0.15f;
    [SerializeField] private float _bombFlightDuration = 0.6f;
    [SerializeField] private float _bombArcHeight = 1.5f;
    private float _bombRollDistance = 1.0f;
    private float _bombRollDuration = 0.25f;

    private Coroutine _attackCoroutine;

    // 공격 쿨다운 및 좌우 이동 필드
    [SerializeField] private float _strafeDirectionDuration = 1f;

    private float _nextAttackTime;
    private float _nextStrafeChangeTime;
    private float _strafeSide;
    private float _idleDurationAfterThrow = 1.1f;

    protected override void OnInitialized(EnemyData data)
    {
        _nextAttackTime = Time.time;
        _nextStrafeChangeTime = Time.time;
    }

    public override bool IsAttackInterruptible => false;

    public override void PerformChaseBehavior()
    {
        float distance = EnemySensor.DistanceToTarget();
        bool hasLineOfSight = EnemySensor.HasLineOfSightToTarget();

        if (hasLineOfSight &&
            distance <= _maximumDistance &&
            Time.time >= _nextAttackTime)
        {
            StateMachine.ChangeState(StateMachine.AttackState);
            return;
        }

        if (distance < _minimumDistance)
        {
            Vector2 direction =
                EnemyMove.GetRetreatDirection(-EnemySensor.DirectionToTarget());

            StartMoving(direction);
            return;
        }

        if (!hasLineOfSight || distance > _maximumDistance)
        {
            MoveTowardsPosition(EnemySensor.TargetTransform.position);
            return;
        }

        if (Time.time >= _nextStrafeChangeTime)
        {
            _strafeSide = Random.value < 0.5f ? -1f : 1f;
            _nextStrafeChangeTime = Time.time + _strafeDirectionDuration;
        }

        Vector2 towardTarget = EnemySensor.DirectionToTarget();
        Vector2 strafeDirection =
            Vector2.Perpendicular(towardTarget) * _strafeSide;

        StartMoving(EnemyMove.GetRetreatDirection(strafeDirection));
    }

    protected override void OnAttackSequenceStarted()
    {
        _nextAttackTime = Time.time + _idleDurationAfterThrow + _attackInterval;

        List<Vector2> landingPositions = CreateLandingPositions();

        if (landingPositions.Count > 0) EnemySound?.PlayThrow();

        foreach (Vector2 landingPosition in landingPositions)
        {
            EnemyBomb bomb = Instantiate(_bombPrefab, landingPosition, Quaternion.identity);

            bomb.Initialize(
                landingPosition,
                _explosionRadius,
                _fuseDuration,
                Power,
                _targetLayer,
                gameObject,
                _bombExplosionPrefab,
                _bombWarningBlinkInterval);

            bomb.PlayFlight(transform.position, _bombFlightDuration, _bombArcHeight,
                _bombRollDistance, _bombRollDuration);
        }

        EnemyCombat.NotifyAttackFinished();
    }

    protected override void OnAttackSequenceCancelled()
    {
        StopAttackRoutine();
    }

    protected override void OnDisable()
    {
        StopAttackRoutine();
        base.OnDisable();
    }

    protected override void HandleAttackFinished()
    {
        StateMachine.EnterIdle(_idleDurationAfterThrow);
    }

    private List<Vector2> CreateLandingPositions()
    {
        int minimumCount = Mathf.Max(1, _minimumBombCount);
        int maximumCount = Mathf.Max(minimumCount, _maximumBombCount);
        int bombCount = Random.Range(minimumCount, maximumCount + 1);

        Vector2 targetPosition = EnemySensor.HasTarget
            ? EnemySensor.TargetTransform.position
            : transform.position;

        // 첫 폭탄 위치도 반드시 검증합니다.
        if (!IsBombPositionOnGround(targetPosition))
        {
            targetPosition = transform.position;
        }

        List<Vector2> positions = new List<Vector2>(bombCount);

        if (!IsBombPositionOnGround(targetPosition))
        {
            Debug.LogWarning(
                "[ThrowerEnemy] Ground 위의 폭탄 중심 위치를 찾지 못했습니다.",
                this);

            return positions;
        }

        positions.Add(targetPosition);

        for (int index = 1; index < bombCount; index++)
        {
            positions.Add(FindScatteredPosition(targetPosition, positions));
        }

        return positions;
    }

    private bool IsBombPositionOnGround(Vector2 position)
    {
        var ground = GroundTilemapProvider.Ground;

        if (ground == null) return false;

        return ground.HasTile(ground.WorldToCell(position));
    }

    private Vector2 FindScatteredPosition(
        Vector2 center,
        IReadOnlyList<Vector2> existingPositions)
    {
        float minimumRadius =
            Mathf.Min(_minimumBombSpacing, _bombScatterRadius);

        // 1. Ground와 폭탄 사이 간격을 모두 만족하는 위치를 찾습니다.
        for (int attempt = 0; attempt < 20; attempt++)
        {
            Vector2 direction = Random.insideUnitCircle.normalized;
            float distance = Random.Range(minimumRadius, _bombScatterRadius);
            Vector2 candidate = center + direction * distance;

            if (!IsBombPositionOnGround(candidate)) continue;
            if (!HasMinimumSpacing(candidate, existingPositions)) continue;

            return candidate;
        }

        // 2. 좁은 지형에서는 간격을 완화하고 Ground 조건만 유지합니다.
        for (int attempt = 0; attempt < 20; attempt++)
        {
            Vector2 candidate =
                center + Random.insideUnitCircle * _bombScatterRadius;

            if (!IsBombPositionOnGround(candidate)) continue;

            return candidate;
        }

        // 3. 검증되지 않은 좌표 대신, Ground가 확인된 중심을 사용합니다.
        return center;
    }

    private bool HasMinimumSpacing(Vector2 candidate, IReadOnlyList<Vector2> existingPositions)
    {
        float minimumSpacingSquared = _minimumBombSpacing * _minimumBombSpacing;

        foreach (Vector2 existingPosition in existingPositions)
        {
            if ((candidate - existingPosition).sqrMagnitude < minimumSpacingSquared)
            {
                return false;
            }
        }

        return true;
    }

    private void StopAttackRoutine()
    {
        if (_attackCoroutine == null)
        {
            return;
        }

        StopCoroutine(_attackCoroutine);
        _attackCoroutine = null;
    }
}
