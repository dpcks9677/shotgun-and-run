using ShotgunAndRun.Combat;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyLocalNavigation : MonoBehaviour
{
    private static readonly RaycastHit2D[] NavigationHits = new RaycastHit2D[12];
    private static readonly Collider2D[] NearbyAgents = new Collider2D[24];

    [Header("Local Navigation")]
    [Tooltip("이동 중 우회할 벽 레이어입니다.")]
    [SerializeField] private LayerMask _navigationObstacleMask = (1 << Layers.Default) | (1 << Layers.Water) | (1 << Layers.Obstacle) | (1 << Layers.Wall);
    [Tooltip("이동 중 서로 우회할 Enemy 레이어입니다.")]
    [SerializeField] private LayerMask _navigationAgentMask = 1 << 7;
    [SerializeField, Min(0.1f)] private float _navigationLookAhead = 0.75f;
    [SerializeField, Min(0.1f)] private float _agentAvoidanceLookAhead = 1.5f;
    [SerializeField, Min(0.1f)] private float _agentSeparationRadius = 1.75f;
    [SerializeField, Range(0f, 2f)] private float _agentSeparationWeight = 1.1f;
    [Tooltip("모서리를 통과할 때 기존 우회 측면을 유지하는 시간입니다.")]
    [SerializeField, Min(0f)] private float _avoidanceMemoryDuration = 0.45f;
    [Tooltip("이 시간 이상 움직이지 못하면 반대 측면 우회를 시도합니다.")]
    [SerializeField, Min(0.05f)] private float _stuckRepathDuration = 0.35f;

    private EnemyMove _ownerMove;
    private Rigidbody2D _rigidbody;
    private float _bodyRadius;
    private float _blockedDuration;
    private int _avoidanceSide;
    private float _avoidanceMemoryUntil;
    private Vector2 _lastAvoidanceDirection;
    private Vector2 _patrolDirection = Vector2.right;
    private Collider2D _retreatObstacle;
    private Vector2 _retreatDirection;
    private float _retreatAvoidanceUntil;

    public Vector2 PatrolDirection => _patrolDirection;

    public void Initialize(EnemyMove ownerMove, Rigidbody2D rigidbody, float bodyRadius)
    {
        _ownerMove = ownerMove;
        _rigidbody = rigidbody;
        _bodyRadius = bodyRadius;
        ResetAvoidance();
    }

    // Todo: 이것도 리팩토링 적절하게 해야될 거 같은데 방향 설정이랑 이동이랑 어떻게 책임 분리를 잘 할 까....
    public void SetPatrolDirection()
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        _patrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    public Vector2 GetPatrolSteeringDirection()
    {
        // 벽을 피해 꺾인 방향 자체를 다음 Patrol 방향으로 채택합니다.
        // 그렇지 않으면 매 프레임 원래 벽 방향을 다시 목표로 삼아 벽에 붙게 됩니다.
        _patrolDirection = GetSteeringDirection(_patrolDirection);
        return _patrolDirection;
    }

    // Todo: A* 알고리즘 기반으로 Moving 바꾸면 넌 그냥 코드 휴지통으로
    public Vector2 GetSteeringDirection(Vector2 desiredDirection)
    {
        if (desiredDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            return Vector2.zero;
        }

        desiredDirection.Normalize();
        Vector2 navigationDirection = ApplyAgentSeparation(desiredDirection);
        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = _navigationObstacleMask.value | _navigationAgentMask.value,
            useTriggers = false
        };
        float castDistance = Mathf.Max(_navigationLookAhead, _agentAvoidanceLookAhead);
        int hitCount = Physics2D.CircleCast(
            _rigidbody != null ? _rigidbody.position : (Vector2)transform.position,
            _bodyRadius * 0.9f,
            navigationDirection,
            filter,
            NavigationHits,
            castDistance);

        RaycastHit2D nearestObstacle = default;
        bool hasObstacle = false;

        for (int index = 0; index < hitCount; index++)
        {
            RaycastHit2D hit = NavigationHits[index];

            if (hit.collider == null || hit.collider.isTrigger ||
                hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            EnemyMove otherAgent = hit.collider.GetComponentInParent<EnemyMove>();
            bool isOtherAgent = otherAgent != null && otherAgent != _ownerMove;
            float relevantLookAhead = isOtherAgent
                ? _agentAvoidanceLookAhead
                : _navigationLookAhead;

            if (hit.distance > relevantLookAhead)
            {
                continue;
            }

            if (!hasObstacle || hit.distance < nearestObstacle.distance)
            {
                nearestObstacle = hit;
                hasObstacle = true;
            }
        }

        if (!hasObstacle)
        {
            if (_avoidanceSide != 0 && Time.unscaledTime < _avoidanceMemoryUntil &&
                _lastAvoidanceDirection.sqrMagnitude > Mathf.Epsilon)
            {
                // 벽을 막 벗어난 순간 목표 방향으로 급복귀하지 않고 모서리를 완전히 통과합니다.
                return (_lastAvoidanceDirection * 0.55f + navigationDirection * 0.45f).normalized;
            }

            if (Time.unscaledTime >= _avoidanceMemoryUntil)
            {
                _avoidanceSide = 0;
                _lastAvoidanceDirection = Vector2.zero;
            }

            return navigationDirection;
        }

        Vector2 leftTangent = new Vector2(-nearestObstacle.normal.y, nearestObstacle.normal.x);

        if (_avoidanceSide == 0 || Time.unscaledTime >= _avoidanceMemoryUntil)
        {
            float leftScore = Vector2.Dot(leftTangent, navigationDirection);
            float rightScore = Vector2.Dot(-leftTangent, navigationDirection);
            _avoidanceSide = rightScore > leftScore ||
                (Mathf.Approximately(leftScore, rightScore) && (GetInstanceID() & 1) == 0)
                ? -1
                : 1;
        }

        Vector2 tangent = leftTangent * _avoidanceSide;
        _lastAvoidanceDirection = tangent;
        _avoidanceMemoryUntil = Time.unscaledTime + _avoidanceMemoryDuration;

        // 벽 또는 앞을 막은 Enemy를 따라 비켜가면서 원래 목표 쪽 성분도 유지합니다.
        return (tangent * 0.85f + navigationDirection * 0.15f).normalized;
    }

    public void UpdateBlockedState(bool isBlocked, bool isBeingKnockedBack)
    {
        if (isBlocked && !isBeingKnockedBack)
        {
            _blockedDuration += Time.fixedDeltaTime;

            if (_blockedDuration >= _stuckRepathDuration)
            {
                _avoidanceSide = _avoidanceSide == 0
                    ? ((GetInstanceID() & 1) == 0 ? 1 : -1)
                    : -_avoidanceSide;
                _lastAvoidanceDirection = -_lastAvoidanceDirection;
                _avoidanceMemoryUntil = Time.unscaledTime + _avoidanceMemoryDuration;
                _blockedDuration = 0f;
            }

            return;
        }

        _blockedDuration = 0f;
    }

    public Vector2 GetRetreatDirection(Vector2 desiredDirection)
    {
        desiredDirection.Normalize();

        if (!TryGetRetreatObstacle(desiredDirection, out RaycastHit2D obstacle))
        {
            if (Time.time < _retreatAvoidanceUntil &&
                !TryGetRetreatObstacle(_retreatDirection, out _))
                return _retreatDirection;

            _retreatObstacle = null;
            return desiredDirection;
        }

        if (_retreatObstacle == obstacle.collider &&
            !TryGetRetreatObstacle(_retreatDirection, out _))
            return _retreatDirection;

        Vector2 tangent = Vector2.Perpendicular(obstacle.normal);
        if (Vector2.Dot(tangent, desiredDirection) < 0f)
            tangent = -tangent;

        Vector2 reflected = Vector2.Reflect(desiredDirection, obstacle.normal).normalized;
        if (Vector2.Dot(reflected, desiredDirection) <= 0f)
            reflected = tangent;

        bool followWall = Random.value < 0.5f;
        Vector2 direction = followWall ? tangent : reflected;
        Vector2 alternative = followWall ? reflected : tangent;

        if (TryGetRetreatObstacle(direction, out _))
        {
            direction = alternative;
            if (TryGetRetreatObstacle(direction, out _))
            {
                direction = -tangent;
                if (TryGetRetreatObstacle(direction, out _))
                {
                    direction = -desiredDirection;
                    if (TryGetRetreatObstacle(direction, out _))
                        return Vector2.zero;
                }
            }
        }

        _retreatObstacle = obstacle.collider;
        _retreatDirection = direction;
        _retreatAvoidanceUntil = Time.time + _avoidanceMemoryDuration;
        return direction;
    }

    private bool TryGetRetreatObstacle(Vector2 direction, out RaycastHit2D obstacle)
    {
        Bounds bounds = _ownerMove.BodyCollider.bounds;
        float contactOffset = Physics2D.defaultContactOffset;
        Vector2 castSize = (Vector2)bounds.size - Vector2.one * (contactOffset * 2f);
        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = _navigationObstacleMask,
            useTriggers = false
        };
        float distance = Mathf.Max(_navigationLookAhead, _ownerMove.MoveSpeed * Time.fixedDeltaTime);
        int count = Physics2D.BoxCast(bounds.center, castSize, 0f, direction,
            filter, NavigationHits, distance + contactOffset);

        obstacle = default;
        bool found = false;
        for (int index = 0; index < count; index++)
        {
            RaycastHit2D hit = NavigationHits[index];
            if (hit.collider.transform.IsChildOf(transform) ||
                Vector2.Dot(direction, hit.normal) >= 0f) continue;

            if (!found || hit.distance < obstacle.distance)
            {
                obstacle = hit;
                found = true;
            }
        }

        return found;
    }

    public void ResetAvoidance()
    {
        _blockedDuration = 0f;
        _avoidanceSide = 0;
        _avoidanceMemoryUntil = 0f;
        _lastAvoidanceDirection = Vector2.zero;
        _retreatObstacle = null;
        _retreatDirection = Vector2.zero;
        _retreatAvoidanceUntil = 0f;
    }

    private Vector2 ApplyAgentSeparation(Vector2 desiredDirection)
    {
        Vector2 position = _rigidbody != null
            ? _rigidbody.position
            : (Vector2)transform.position;
        float queryRadius = Mathf.Max(_agentSeparationRadius, _bodyRadius * 2.25f);
        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = _navigationAgentMask,
            useTriggers = false
        };
        int hitCount = Physics2D.OverlapCircle(
            position,
            queryRadius,
            filter,
            NearbyAgents);
        Vector2 separation = Vector2.zero;

        for (int index = 0; index < hitCount; index++)
        {
            Collider2D hit = NearbyAgents[index];

            if (hit == null || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            EnemyMove otherAgent = hit.GetComponentInParent<EnemyMove>();

            if (otherAgent == null || otherAgent == _ownerMove)
            {
                continue;
            }

            Vector2 otherPosition = otherAgent.Rigidbody != null
                ? otherAgent.Rigidbody.position
                : (Vector2)otherAgent.transform.position;
            Vector2 awayFromAgent = position - otherPosition;
            float distance = awayFromAgent.magnitude;
            float separationRange = Mathf.Max(
                queryRadius,
                (_bodyRadius + otherAgent.BodyRadius) * 1.35f);

            if (distance >= separationRange)
            {
                continue;
            }

            if (distance <= 0.001f)
            {
                Vector2 perpendicular = new Vector2(-desiredDirection.y, desiredDirection.x);
                awayFromAgent = ((GetInstanceID() ^ otherAgent.GetInstanceID()) & 1) == 0
                    ? perpendicular
                    : -perpendicular;
                distance = 0f;
            }
            else
            {
                awayFromAgent /= distance;
            }

            float proximity = 1f - Mathf.Clamp01(distance / separationRange);
            separation += awayFromAgent * proximity;
        }

        // 전진 성분은 유지하고 군집 반대쪽의 횡방향 성분만 섞어 줄서기와 뭉침을 줄입니다.
        Vector2 lateralSeparation = separation -
            desiredDirection * Vector2.Dot(separation, desiredDirection);

        if (lateralSeparation.sqrMagnitude <= Mathf.Epsilon)
        {
            return desiredDirection;
        }

        lateralSeparation = Vector2.ClampMagnitude(lateralSeparation, 1f);
        return (desiredDirection + lateralSeparation * _agentSeparationWeight).normalized;
    }
}
