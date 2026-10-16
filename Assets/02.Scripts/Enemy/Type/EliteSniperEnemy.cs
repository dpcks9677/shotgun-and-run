using System.Collections.Generic;
using Consumable;
using ShotgunAndRun.Combat;
using UnityEngine;
using UnityEngine.Serialization;

public class EliteSniperEnemy : SniperEnemy
{
    private const float ChargeDurationMultiplier = 0.7f;

    [Header("Elite Pattern")]
    [SerializeField, Min(0f)] private float _closeRange = 5f;
    [FormerlySerializedAs("_gimmickCooldown"), SerializeField, Min(0f)]
    private float _bombCooldown = 4f;
    [SerializeField, Min(0f)] private float _teleportCooldown = 6f;
    [SerializeField, Min(0f)] private float _teleportMinimumDistance = 12f;
    [SerializeField, Min(0f)] private float _teleportMaximumDistance = 15f;
    [SerializeField, Min(0f)] private float _teleportRetryInterval = 0.25f;
    [SerializeField] private Collider2D _teleportArea;
    [SerializeField] private GameObject _teleportEffectPrefab;
    [SerializeField] private Vector2 _teleportEffectOffset;
    [SerializeField] private TeleportDestinationIndicator _teleportIndicatorPrefab;
    [SerializeField] private float _teleportWarningDuration = 0.8f;
    [SerializeField] private float _returnTeleportCooldown = 1f;
    [SerializeField]
    private LayerMask _teleportObstacleMask = (1 << Layers.Default) | (1 << Layers.Water) | (1 << Layers.Obstacle) | (1 << Layers.Wall);
    [SerializeField, Min(0.1f)] private float _bombRadius = 3f;
    [SerializeField, Min(0.1f)] private float _bombFuse = 1.5f;
    [SerializeField, Min(0f)] private float _bombSpacing = 4f;
    [SerializeField] private LayerMask _bombTargetLayer = 1 << 6;
    [SerializeField] private EnemyBomb _bombPrefab;
    [SerializeField] private GrenadeExplosionVFX _bombExplosionPrefab;
    [SerializeField] private float _bombFlightDuration = 0.6f;
    [SerializeField] private float _bombArcHeight = 1.5f;
    [SerializeField] private float _bombRollDistance = 1f;
    [SerializeField] private float _bombRollDuration = 0.25f;

    private float _lastBombTime = float.NegativeInfinity;
    private float _lastTeleportTime = float.NegativeInfinity;
    private float _lastTeleportAttemptTime = float.NegativeInfinity;
    private TeleportDestinationIndicator _teleportIndicator;
    private Vector2 _teleportDestination;
    private float _teleportWarningElapsed;
    private bool _isTeleportPending;
    private bool _isReturnTeleport;
    private float _lastReturnTeleportAttemptTime = float.NegativeInfinity;

    protected override void Awake()
    {
        base.Awake();
        _trackingDuration *= ChargeDurationMultiplier;
    }

    protected override void OnInitialized(EnemyData data)
    {
        _lastBombTime = float.NegativeInfinity;
        _lastTeleportTime = float.NegativeInfinity;
        _lastTeleportAttemptTime = float.NegativeInfinity;
        _lastReturnTeleportAttemptTime = float.NegativeInfinity;
    }

    private void Update()
    {
        if (_isTeleportPending)
        {
            UpdatePendingTeleport();
            return;
        }

        if (StateMachine.CurrentState != StateMachine.ReturnState)
            TryTeleportIfAvailable();
    }

    public override void PerformReturnBehavior()
    {
        if (_isTeleportPending)
        {
            if (_isReturnTeleport) return;
            ClearTeleportWarning();
        }

        if (EnemyHealth.IsDead || IsStunned ||
            Time.time - _lastTeleportTime < Mathf.Max(0f, _returnTeleportCooldown - _teleportWarningDuration) ||
            Time.time - _lastReturnTeleportAttemptTime < _returnTeleportCooldown) return;

        _lastReturnTeleportAttemptTime = Time.time;
        if (!TryGetReturnTeleportDestination(out Vector2 destination)) return;

        StopMoving();
        BeginTeleport(destination, true);
    }

    private bool TryGetReturnTeleportDestination(out Vector2 destination)
    {
        Vector2 currentPosition = transform.position;
        Vector2 directionHome = SpawnPosition - currentPosition;
        float remainingDistance = directionHome.magnitude;
        float maximumStep = Mathf.Max(_teleportMinimumDistance, _teleportMaximumDistance);
        float minimumStep = Mathf.Min(_teleportMinimumDistance, remainingDistance);
        float maximumDistance = Mathf.Min(maximumStep, remainingDistance);

        // 마지막 구간에서는 홈 좌표에 정확히 도착합니다.
        if (remainingDistance <= maximumStep && IsTeleportPositionSafe(SpawnPosition, true))
        {
            destination = SpawnPosition;
            return true;
        }

        Vector2 direction = directionHome.normalized;
        for (int attempt = 0; attempt < 32; attempt++)
        {
            float distance = Random.Range(minimumStep, maximumDistance);
            Vector2 candidate = currentPosition + direction * distance;
            if (!IsTeleportPositionSafe(candidate, true)) continue;

            destination = candidate;
            return true;
        }

        destination = default;
        return false;
    }

    protected override void OnDisable()
    {
        ClearTeleportWarning();
        base.OnDisable();
    }

    private void UpdatePendingTeleport()
    {
        bool isReturning = StateMachine.CurrentState == StateMachine.ReturnState;
        if (EnemyHealth.IsDead || IsStunned ||
            (_isReturnTeleport ? !isReturning : isReturning || !EnemySensor.HasTarget))
        {
            ClearTeleportWarning();
            return;
        }

        _teleportWarningElapsed += Time.deltaTime;
        if (_teleportWarningElapsed < _teleportWarningDuration) return;

        bool isSafe = IsTeleportPositionSafe(_teleportDestination, _isReturnTeleport);
        if (_isReturnTeleport && !isSafe) _lastReturnTeleportAttemptTime = Time.time;
        ClearTeleportWarning();
        _lastTeleportAttemptTime = Time.time;
        if (!isSafe) return;

        PlayTeleportEffect(transform.position);
        EnemyMove.Teleport(_teleportDestination);
        EnemySound?.PlayTeleportIn();
        PlayTeleportEffect(transform.position);
        _lastTeleportTime = Time.time;
    }

    private void ClearTeleportWarning()
    {
        EnemySound?.StopTeleport();
        if (_teleportIndicator != null) Destroy(_teleportIndicator.gameObject);
        _teleportIndicator = null;
        _isTeleportPending = false;
        _isReturnTeleport = false;
    }

    protected override bool TryUsePreChargeGimmick(out bool shouldSkipShot)
    {
        shouldSkipShot = false;

        if (!EnemySensor.HasTarget ||
            Time.time - _lastBombTime < _bombCooldown)
        {
            return false;
        }

        _lastBombTime = Time.time;
        SpawnBombs();

        return true;
    }

    private void SpawnBombs()
    {
        EnemySound?.PlayThrow();

        Vector2 targetPosition = GetTargetPosition();
        List<Vector2> positions = new List<Vector2>(3) { targetPosition };

        for (int index = 1; index < 3; index++)
        {
            positions.Add(FindBombPosition(targetPosition, positions, index));
        }

        foreach (Vector2 position in positions)
        {
            EnemyBomb bomb = Instantiate(_bombPrefab, position, Quaternion.identity);
            bomb.Initialize(
                position,
                _bombRadius,
                _bombFuse,
                Power,
                _bombTargetLayer,
                gameObject,
                _bombExplosionPrefab);

            bomb.PlayFlight(transform.position, _bombFlightDuration, _bombArcHeight,
                _bombRollDistance, _bombRollDuration);
        }
    }

    private Vector2 FindBombPosition(
        Vector2 center,
        IReadOnlyList<Vector2> existingPositions,
        int index)
    {
        for (int attempt = 0; attempt < 24; attempt++)
        {
            Vector2 direction = Random.insideUnitCircle.normalized;
            Vector2 candidate = center + direction * Random.Range(_bombSpacing, 6f);
            bool hasSpacing = true;

            foreach (Vector2 existingPosition in existingPositions)
            {
                if ((candidate - existingPosition).sqrMagnitude <
                    _bombSpacing * _bombSpacing)
                {
                    hasSpacing = false;
                    break;
                }
            }

            if (hasSpacing)
            {
                return candidate;
            }
        }

        float angle = Mathf.PI * 2f * index / 2f;
        return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _bombSpacing;
    }

    private bool TryTeleportIfAvailable()
    {
        if (EnemyHealth == null || EnemyHealth.IsDead || IsStunned ||
            !EnemySensor.HasTarget ||
            EnemySensor.DistanceToTarget() > _closeRange ||
            Time.time - _lastTeleportTime < _teleportCooldown ||
            Time.time - _lastTeleportAttemptTime < _teleportRetryInterval)
        {
            return false;
        }

        _lastTeleportAttemptTime = Time.time;

        if (!TryTeleportAwayFromTarget())
        {
            return false;
        }

        return true;
    }

    private bool TryTeleportAwayFromTarget()
    {
        var ground = GroundTilemapProvider.Ground;
        if (ground == null) return false;

        Vector2 targetPosition = GetTargetPosition();

        for (int attempt = 0; attempt < 32; attempt++)
        {
            Vector2 candidate = CreateTeleportCandidate(targetPosition);

            if ((candidate - targetPosition).sqrMagnitude <
                _teleportMinimumDistance * _teleportMinimumDistance ||
                !IsTeleportPositionSafe(candidate))
            {
                continue;
            }

            BeginTeleport(candidate, false);
            return true;
        }

        return false;
    }

    private void BeginTeleport(Vector2 destination, bool isReturnTeleport)
    {
        _teleportDestination = destination;
        _teleportWarningElapsed = 0f;
        _isTeleportPending = true;
        _isReturnTeleport = isReturnTeleport;
        EnemySound?.PlayTeleportOut();
        if (_teleportIndicatorPrefab != null)
        {
            _teleportIndicator = Instantiate(_teleportIndicatorPrefab,
                (Vector3)destination + (Vector3)_teleportEffectOffset, Quaternion.identity);
        }
    }

    private void PlayTeleportEffect(Vector3 position)
    {
        if (_teleportEffectPrefab == null) return;

        Instantiate(_teleportEffectPrefab, position + (Vector3)_teleportEffectOffset,
            Quaternion.identity);
    }

    private bool IsTeleportPositionSafe(Vector2 position, bool isReturnTeleport = false)
    {
        var ground = GroundTilemapProvider.Ground;
        return ground != null &&
            (isReturnTeleport || _teleportArea == null || _teleportArea.OverlapPoint(position)) &&
            EnemySpawnerPositionUtils.IsOnGround(position, EnemyType, ground) &&
            !IsTeleportPositionBlocked(position);
    }

    private Vector2 CreateTeleportCandidate(Vector2 targetPosition)
    {
        if (_teleportArea != null)
        {
            Bounds bounds = _teleportArea.bounds;
            return new Vector2(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y));
        }

        Vector2 direction = Random.insideUnitCircle.normalized;
        float distance = Random.Range(
            _teleportMinimumDistance,
            Mathf.Max(_teleportMinimumDistance, _teleportMaximumDistance));
        return targetPosition + direction * distance;
    }

    private bool IsTeleportPositionBlocked(Vector2 position)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            position,
            EnemyMove.BodyRadius,
            _teleportObstacleMask);

        foreach (Collider2D hit in hits)
        {
            if (hit == null || hit.isTrigger || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            return true;
        }

        return false;
    }
}
