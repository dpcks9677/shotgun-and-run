using System;
using System.Collections;
using ShotgunAndRun.Enemy;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyPhysicsBody))]
[RequireComponent(typeof(EnemyLocalNavigation))]
[RequireComponent(typeof(EnemyPathNavigation))]
public class EnemyMove : MonoBehaviour
{
    [Header("Physics Body")]
    [Tooltip("다른 적과 겹치지 않게 하는 월드 기준 몸체 반지름입니다.")] [SerializeField, Min(0.05f)]
    private float _bodyRadius = 0.5f;

    [Header("Movement")]
    [Tooltip("이동 중 목표 방향으로 회전하는 초당 각도입니다.")] [SerializeField, Min(1f)]
    private float _turnSpeedDegreesPerSecond = 720f;

    private Rigidbody2D _rigidbody;
    private Collider2D _bodyCollider;
    private EnemyPhysicsBody _physicsBody;
    private EnemyLocalNavigation _localNavigation;
    private float _moveSpeed;
    private float _knockbackDistance;
    private float _knockbackDuration;
    private Vector2 _desiredVelocity;
    private Vector2 _knockbackVelocity;
    private Vector3 _currentDirection = Vector3.right;
    private bool _isMoving;
    private bool _isBeingKnockedBack;
    private bool _isBlocked;
    private Coroutine _knockbackCoroutine;

    public float MoveSpeed => _moveSpeed;
    public float BodyRadius => _bodyRadius;
    public Vector3 CurrentDirection => _currentDirection;
    public bool IsMoving => _isMoving;
    public bool IsBeingKnockedBack => _isBeingKnockedBack;
    public float KnockbackDuration => _knockbackDuration;
    public Vector2 PatrolDirection => _localNavigation != null ? _localNavigation.PatrolDirection : Vector2.right;
    public bool IsBlocked => _isBlocked;
    public Rigidbody2D Rigidbody => _rigidbody;
    public Collider2D BodyCollider => _bodyCollider;

    public event Action<bool> MoveEvent;

    // A* Algorithm 관련 필드
    private EnemyPathNavigation _pathNavigation;
    private bool _isPathInitialized;
    private bool _isUsingAstar;

    private void Awake()
    {
        _physicsBody = GetComponent<EnemyPhysicsBody>();

        if (_physicsBody == null)
        {
            _physicsBody = gameObject.AddComponent<EnemyPhysicsBody>();
        }

        _localNavigation = GetComponent<EnemyLocalNavigation>();

        if (_localNavigation == null)
        {
            _localNavigation = gameObject.AddComponent<EnemyLocalNavigation>();
        }

        // A* Algorithm 스크립트 컴포넌트 연결
        _pathNavigation = GetComponent<EnemyPathNavigation>();

        _physicsBody.Initialize(_bodyRadius);
        _rigidbody = _physicsBody.Rigidbody;
        _bodyCollider = _physicsBody.BodyCollider;
        _localNavigation.Initialize(this, _rigidbody, _bodyRadius);
    }

    private void FixedUpdate()
    {
        if (_rigidbody == null)
        {
            return;
        }

        Vector2 targetVelocity = _isBeingKnockedBack
            ? _knockbackVelocity
            : _desiredVelocity;
        float expectedSpeed = targetVelocity.magnitude;

        // 직전 물리 프레임에서 명령 속도를 거의 내지 못했다면 벽이나 다른 몸체에 막힌 상태입니다.
        _isBlocked = expectedSpeed > 0.01f &&
                     _rigidbody.linearVelocity.magnitude < expectedSpeed * 0.15f;

        _localNavigation?.UpdateBlockedState(_isBlocked, _isBeingKnockedBack);

        _rigidbody.linearVelocity = targetVelocity;
    }

    public void Initialize(float moveSpeed, float knockbackDistance, float knockbackDuration)
    {
        CancelKnockback();
        ClearAstarPath();
        SetMoveSpeed(moveSpeed);
        _knockbackDistance = Mathf.Max(0f, knockbackDistance);
        _knockbackDuration = Mathf.Max(0f, knockbackDuration);
        _desiredVelocity = Vector2.zero;
        _isMoving = false;
        _isBlocked = false;
        _localNavigation?.ResetAvoidance();
        if (_rigidbody != null) _rigidbody.linearVelocity = Vector2.zero;
    }

    // 직접 이동 요청
    public void Move(Vector3 direction)
    {
        ClearAstarPath();
        MoveInDirection(direction);
    }

    // 직접 이동과 A* 이동이 함께 사용하는 실제 이동 처리
    private void MoveInDirection(Vector3 direction)
    {
        if (_isBeingKnockedBack) return;

        direction.z = 0f;

        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            Stop();
            return;
        }

        Vector3 targetDirection = direction.normalized;

        _currentDirection = targetDirection;

        _desiredVelocity = (Vector2)_currentDirection * _moveSpeed;
        SetMoving(true);
    }

    public void Stop()
    {
        _desiredVelocity = Vector2.zero;

        if (!_isBeingKnockedBack && _rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector2.zero;
        }

        SetMoving(false);
    }

    public void ApplyKnockback(Vector2 direction)
    {
        if (_knockbackDistance <= 0f || direction.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        CancelKnockback();
        Stop();

        if (_knockbackDuration <= Mathf.Epsilon)
        {
            _rigidbody.MovePosition(_rigidbody.position + direction.normalized * _knockbackDistance);
            return;
        }

        _knockbackCoroutine = StartCoroutine(KnockbackRoutine(direction.normalized));
    }

    public void CancelKnockback()
    {
        if (_knockbackCoroutine != null)
        {
            StopCoroutine(_knockbackCoroutine);
            _knockbackCoroutine = null;
        }

        _isBeingKnockedBack = false;
        _knockbackVelocity = Vector2.zero;

        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = _desiredVelocity;
        }
    }

    public void SetMoveSpeed(float moveSpeed)
    {
        if (moveSpeed < 0f)
        {
            return;
        }

        _moveSpeed = moveSpeed;

        if (_isMoving && !_isBeingKnockedBack)
        {
            _desiredVelocity = (Vector2)_currentDirection * _moveSpeed;
        }
    }

    public void SetPatrolDirection()
    {
        _localNavigation?.SetPatrolDirection();
    }

    public Vector2 GetPatrolSteeringDirection()
    {
        return _localNavigation != null
            ? _localNavigation.GetPatrolSteeringDirection()
            : Vector2.zero;
    }

    public Vector2 GetSteeringDirection(Vector2 desiredDirection)
    {
        return _localNavigation != null
            ? _localNavigation.GetSteeringDirection(desiredDirection)
            : desiredDirection.normalized;
    }

    public Vector2 GetRetreatDirection(Vector2 desiredDirection)
    {
        return _localNavigation.GetRetreatDirection(desiredDirection);
    }

    public void SetImmovable(bool isImmovable)
    {
        _physicsBody?.SetImmovable(isImmovable);
    }

    public void Teleport(Vector2 position)
    {
        if (_rigidbody == null)
        {
            transform.position = position;
            return;
        }

        _desiredVelocity = Vector2.zero;
        _knockbackVelocity = Vector2.zero;
        _rigidbody.linearVelocity = Vector2.zero;
        _rigidbody.position = position;
        Physics2D.SyncTransforms();
    }

    private void SetMoving(bool isMoving)
    {
        if (_isMoving == isMoving)
        {
            return;
        }

        _isMoving = isMoving;
        MoveEvent?.Invoke(isMoving);
    }

    private IEnumerator KnockbackRoutine(Vector2 direction)
    {
        _isBeingKnockedBack = true;
        _knockbackVelocity = direction * (_knockbackDistance / _knockbackDuration);
        float elapsedTime = 0f;

        while (elapsedTime < _knockbackDuration)
        {
            yield return new WaitForFixedUpdate();
            elapsedTime += Time.fixedDeltaTime;
        }

        _isBeingKnockedBack = false;
        _knockbackVelocity = Vector2.zero;
        _knockbackCoroutine = null;

        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector2.zero;
        }
    }

    private void OnDisable()
    {
        CancelKnockback();
        _desiredVelocity = Vector2.zero;
        _isBlocked = false;
        _localNavigation?.ResetAvoidance();
        SetMoving(false);
    }

    public void MoveUsingAstar(Vector2 targetPosition)
    {
        if (_isBeingKnockedBack) return;

        if (!_isPathInitialized)
        {
            if (GroundTilemapProvider.Ground == null)
            {
                Debug.LogError("A* 이동에 필요한 GroundTilemap이 없습니다.");
                Stop();
                return;
            }

            _pathNavigation.Initialize(GroundTilemapProvider.Ground, _bodyCollider);

            _isPathInitialized = true;
        }

        // 직접 이동에서 A*로 전환할 때 새 경로로 시작합니다.
        if (!_isUsingAstar)
        {
            _pathNavigation.ClearPath();
            _isUsingAstar = true;
        }

        Vector2 bodyCenter = _bodyCollider.bounds.center;
        Vector2 bodyOffset = bodyCenter - (Vector2)transform.position;
        Vector2 targetBodyCenter = targetPosition + bodyOffset;

        bool hasDirection =
            _pathNavigation.TryGetDirection(targetBodyCenter, _moveSpeed, _isBlocked, out Vector2 direction);

        if (!hasDirection)
        {
            Stop();
            return;
        }

        // Move()를 호출하면 A* 경로가 지워지므로 실제 이동만 호출합니다.
        MoveInDirection(direction);
    }

    // 이동을 멈추지 않고 A* 경로만 정리합니다.
    private void ClearAstarPath()
    {
        if (!_isUsingAstar) return;

        _pathNavigation.ClearPath();
        _isUsingAstar = false;
    }

    // A* 경로와 실제 이동을 모두 중단합니다.
    public void StopAstarMovement()
    {
        ClearAstarPath();
        Stop();
    }

    public bool CanMoveDirectly(Vector2 targetPosition, bool includeAgents = true)
    {
        if (!_isPathInitialized)
        {
            if (GroundTilemapProvider.Ground == null) return false;

            _pathNavigation.Initialize(GroundTilemapProvider.Ground, _bodyCollider);

            _isPathInitialized = true;
        }

        Vector2 bodyCenter = _bodyCollider.bounds.center;
        Vector2 bodyOffset = bodyCenter - (Vector2)transform.position;

        return _pathNavigation.CanMoveDirectly(targetPosition + bodyOffset, includeAgents);
    }
}
