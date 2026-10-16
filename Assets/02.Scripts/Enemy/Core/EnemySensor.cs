using System;
using ShotgunAndRun.Player;
using UnityEngine;


public class EnemySensor : MonoBehaviour
{
    // LOS 관련 Layer
    [SerializeField] private LayerMask _visionObstacleMask;
    private Vector2 _lastSeenPosition;
    private bool _hasLastSeenPosition;

    // Component Field
    private Transform _targetTransform;
    private PlayerHealth _targetHealth;

    // Range Field
    private float _detectionRange;
    private float _attackRange;

    // Property
    public Transform TargetTransform => _targetTransform;
    // 플레이어가 사망하면 타깃이 없는 것으로 취급합니다.
    public bool HasTarget => _targetTransform != null && (_targetHealth == null || !_targetHealth.IsDead);
    public float DetectionRange => _detectionRange;
    public float AttackRange => _attackRange;
    public Vector2 LastSeenPosition => _lastSeenPosition;
    public bool HasLastSeenPosition => _hasLastSeenPosition;

    public void Initialize(float detectionRange, float attackRange)
    {
        _detectionRange = detectionRange;
        _attackRange = attackRange;
        ClearLastSeenPosition();
    }

    public event Action TargetDied;

    public void SetTargetTransform(Transform targetTransform)
    {
        UnsubscribeTargetHealth();
        _targetTransform = targetTransform;
        _targetHealth = targetTransform != null ? targetTransform.GetComponentInParent<PlayerHealth>() : null;

        if (_targetHealth != null)
        {
            _targetHealth.Died += HandleTargetDied;
        }
    }

    public void ClearTargetTransform()
    {
        UnsubscribeTargetHealth();
        _targetTransform = null;
    }

    private void OnDestroy()
    {
        UnsubscribeTargetHealth();
    }

    private void UnsubscribeTargetHealth()
    {
        if (_targetHealth != null)
        {
            _targetHealth.Died -= HandleTargetDied;
            _targetHealth = null;
        }
    }

    private void HandleTargetDied()
    {
        TargetDied?.Invoke();
    }

    public void ClearLastSeenPosition()
    {
        _lastSeenPosition = Vector2.zero;
        _hasLastSeenPosition = false;
    }

    // Patrol 중 Hit 상태가 되어 적을 Chase해야 할 때
    public void RememberTargetPosition()
    {
        _lastSeenPosition = _targetTransform.position;
        _hasLastSeenPosition = true;
    }

    public bool IsTargetInDetectionRange()
    {
        return DistanceToTarget() <= _detectionRange;
    }

    public bool IsTargetInAttackRange()
    {
        return DistanceToTarget() <= _attackRange;
    }

    public float DistanceToTarget()
    {
        if (!HasTarget) return float.PositiveInfinity;

        Vector3 delta = _targetTransform.position - transform.position;
        delta.z = 0f;

        return delta.magnitude;
    }

    public Vector3 DirectionToTarget()
    {
        if (!HasTarget) return Vector3.zero;

        Vector3 delta = _targetTransform.position - transform.position;
        delta.z = 0f;

        return delta.normalized;
    }

    public bool HasLineOfSightToTarget()
    {
        if (!HasTarget) return false;

        Vector2 origin = transform.position;
        Vector2 target = TargetTransform.position;

        RaycastHit2D hit = Physics2D.Linecast(origin, target, _visionObstacleMask);

        bool hasLineOfSight = hit.collider == null;

        if (hasLineOfSight)
        {
            _lastSeenPosition = target;
            _hasLastSeenPosition = true;
        }

        return hasLineOfSight;
    }
}
