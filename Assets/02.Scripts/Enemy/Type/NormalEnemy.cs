using ShotgunAndRun.Combat;
using UnityEngine;

public class NormalEnemy : Enemy
{
    // 컴포넌트 캐싱
    [SerializeField] private Transform _attackPoint;
    [SerializeField] private float _attackRadius;
    [SerializeField] private LayerMask _targetLayer;

    [SerializeField] private float _speedBoostInterval = 5f;
    [SerializeField] private float _speedBoostDuration = 1.2f;
    [SerializeField] private float _speedBoostMultiplier = 2f;

    private float _chaseElapsed;
    private float _speedBoostElapsed;
    private float _baseMoveSpeed;
    private bool _isSpeedBoosting;

    protected override void OnInitialized(EnemyData data)
    {
        _baseMoveSpeed = data.moveSpeed;
        _chaseElapsed = 0f;
        _speedBoostElapsed = 0f;
        _isSpeedBoosting = false;
    }

    public override void PerformChaseBehavior()
    {
        if (!EnemySensor.IsTargetInAttackRange())
            UpdateSpeedBoost(Time.deltaTime);

        base.PerformChaseBehavior();
    }

    private void UpdateSpeedBoost(float deltaTime)
    {
        if (!_isSpeedBoosting)
        {
            _chaseElapsed += deltaTime;
            if (_chaseElapsed < _speedBoostInterval) return;

            _chaseElapsed = 0f;
            _speedBoostElapsed = 0f;
            _baseMoveSpeed = EnemyMove.MoveSpeed;
            _isSpeedBoosting = true;
            EnemySound?.PlayDetect();
            return;
        }

        _speedBoostElapsed += deltaTime;
        if (_speedBoostElapsed >= _speedBoostDuration)
        {
            OnChaseInterrupted();
            return;
        }

        float progress = _speedBoostElapsed / _speedBoostDuration;
        float blend = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Abs(progress * 2f - 1f));
        EnemyMove.SetMoveSpeed(_baseMoveSpeed * Mathf.Lerp(1f, _speedBoostMultiplier, blend));
    }

    public override void OnChaseInterrupted()
    {
        _chaseElapsed = 0f;
        _speedBoostElapsed = 0f;
        if (!_isSpeedBoosting) return;

        _isSpeedBoosting = false;
        EnemyMove.SetMoveSpeed(_baseMoveSpeed);
    }

    protected override void OnDisable()
    {
        OnChaseInterrupted();
        base.OnDisable();
    }

    public void OnAttackHit()
    {
        Debug.Log("Attack Hit Frame");

        if (StateMachine == null || EnemyCombat == null || !EnemyCombat.IsAttacking) return;

        Transform target = EnemySensor.TargetTransform;

        if (target == null) return;

        EnemyAnimation.SetSpriteFlipImmediately(EnemySensor.DirectionToTarget());

        Vector3 attackPointPosition = _attackPoint.localPosition;

        attackPointPosition.x = Mathf.Abs(attackPointPosition.x) * (EnemyAnimation.GetSpriteFlipX() ? -1f : 1f);

        _attackPoint.localPosition = attackPointPosition;

        Collider2D hitCollider = Physics2D.OverlapCircle(_attackPoint.position, _attackRadius, _targetLayer);

        if (hitCollider == null) return;

        IDamageable damageable = hitCollider.GetComponentInParent<IDamageable>();

        if (damageable == null)
        {
            Debug.Log($"[NormalEnemy.cs : OnAttackHit] - damageable is null");
            return;
        }

        Vector2 hitPoint = hitCollider.ClosestPoint(_attackPoint.position);
        Vector2 hitDirection = EnemySensor.DirectionToTarget();
        DamageInfo damageInfo = new DamageInfo(Power, hitPoint, hitDirection, gameObject);

        EnemyCombat.NotifyAttackHit();

        if (damageable.TryTakeDamage(damageInfo))
        {
            Debug.Log($"[NormalEnemy.cs : OnAttackHit] - On Attack Hit : {hitCollider.name}");
        }
    }

    public void OnAttackFinished()
    {
        EnemyCombat?.NotifyAttackFinished();
    }

    private void OnDrawGizmosSelected()
    {
        if (_attackPoint == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(_attackPoint.position, _attackRadius);
    }
}
