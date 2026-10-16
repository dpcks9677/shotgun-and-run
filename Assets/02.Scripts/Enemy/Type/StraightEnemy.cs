using System.Collections;
using ShotgunAndRun.Combat;
using UnityEngine;

public class StraightEnemy : Enemy
{
    [Header("Charge")]
    [SerializeField, Min(0f)] private float _chargeSpeed = 10f;
    [SerializeField] private float _chargePreparationDuration = 0.4f;
    [SerializeField] private Color _chargeOutlineColor = Color.yellow;

    [SerializeField, Min(0.1f)] private float _maxChargeDuration = 2f;
    [SerializeField, Min(0f)] private float _collisionRadius = 0.4f;
    [SerializeField, Min(0f)] private float _stunDuration = 1f;
    [SerializeField] private RuntimeAnimatorController _wallStunController;
    [SerializeField] private Vector2 _wallHitPitchRange = new Vector2(0.85f, 0.95f);
    [SerializeField] private float _idleDurationAfterAttack = 0.5f;
    [SerializeField] private LayerMask _collisionMask = ~0;

    private Coroutine _chargeCoroutine;
    private float _baseMoveSpeed;
    private bool _hasBaseMoveSpeed;
    private bool _isCharging;
    private bool _ignoringEnemyBodies;
    private LayerMask _originalBodyExclusions;

    public override float IdleDurationAfterAttack => _idleDurationAfterAttack;
    public override bool CanBeKnockedBack => !_isCharging;
    public override bool IsAttackInterruptible => !_isCharging;
    public override bool PursueTargetWithoutLineOfSight => true;
    protected override bool PlayAnimationWhenAttackStarts => false;

    public override void PerformChaseBehavior()
    {
        Vector2 targetPosition = EnemySensor.TargetTransform.position;
        if (EnemySensor.IsTargetInAttackRange() &&
            EnemySensor.HasLineOfSightToTarget() &&
            EnemyMove.CanMoveDirectly(targetPosition, false))
        {
            StateMachine.ChangeState(StateMachine.AttackState);
            return;
        }

        MoveTowardsPosition(targetPosition);
    }

    protected override void OnAttackSequenceStarted()
    {
        StopCharge();
        _baseMoveSpeed = EnemyMove.MoveSpeed;
        _hasBaseMoveSpeed = true;
        _chargeCoroutine = StartCoroutine(ChargeRoutine());
    }

    protected override void OnAttackSequenceCancelled()
    {
        StopCharge();
    }

    protected override void HandleAttackFinished()
    {
        StopCharge();
        base.HandleAttackFinished();
    }

    protected override void OnDisable()
    {
        StopCharge();
        base.OnDisable();
    }

    private IEnumerator ChargeRoutine()
    {
        EnemyMove.StopAstarMovement();
        float preparationElapsed = 0f;
        while (preparationElapsed < _chargePreparationDuration)
        {
            float blend = Mathf.Clamp01(preparationElapsed / _chargePreparationDuration);
            EnemyAnimation.ShowChargeOutline(Color.Lerp(Color.white, _chargeOutlineColor, blend));
            preparationElapsed += Time.deltaTime;
            yield return null;
        }

        EnemyAnimation.HideChargeOutline();
        if (!EnemySensor.HasTarget ||
            !EnemySensor.IsTargetInAttackRange() ||
            !EnemySensor.HasLineOfSightToTarget() ||
            !EnemyMove.CanMoveDirectly(EnemySensor.TargetTransform.position, false))
        {
            FinishAttack();
            yield break;
        }

        PlayAttackAnimation();
        Vector2 chargeDirection = EnemySensor.DirectionToTarget();

        if (chargeDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            FinishAttack();
            yield break;
        }

        _isCharging = true;
        IgnoreEnemyBodyCollisions();
        EnemyMove.SetMoveSpeed(_chargeSpeed);
        float elapsedTime = 0f;
        Vector2 chargeStartPosition = transform.position;

        while (elapsedTime < _maxChargeDuration)
        {
            if (StateMachine.CurrentState != StateMachine.AttackState || !EnemyCombat.IsAttacking)
            {
                _chargeCoroutine = null;
                StopCharge();
                yield break;
            }

            float moveDistance = _chargeSpeed * Time.deltaTime;

            if (TryResolveChargeCollision(chargeDirection, moveDistance, out bool hitWall))
            {
                if (hitWall)
                {
                    bool hasCharged = Vector2.Dot((Vector2)transform.position - chargeStartPosition,
                        chargeDirection.normalized) > Physics2D.defaultContactOffset;
                    _chargeCoroutine = null;
                    StopCharge();
                    if (!hasCharged)
                    {
                        FinishAttack();
                        yield break;
                    }

                    EnemySound?.PlayHit(Random.Range(_wallHitPitchRange.x, _wallHitPitchRange.y));
                    Stun(_stunDuration, _wallStunController);
                    yield break;
                }
                break;
            }

            StartMoving(chargeDirection);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        _isCharging = false;
        RestoreEnemyBodyCollisions();
        StopMoving();
        EnemyMove.SetMoveSpeed(_baseMoveSpeed);

        yield return new WaitForSeconds(_stunDuration);

        FinishAttack();
    }

    private bool TryResolveChargeCollision(Vector2 direction, float distance, out bool hitWall)
    {
        hitWall = false;

        RaycastHit2D[] hits = CastChargeBody(direction, distance);

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) continue;

            if (hit.collider.GetComponentInParent<Enemy>() != null)
            {
                continue;
            }

            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();

            if (damageable != null)
            {
                DamageInfo damageInfo = new DamageInfo(
                    Power,
                    hit.point,
                    direction,
                    gameObject);

                EnemyCombat.NotifyAttackHit();
                damageable.TryTakeDamage(damageInfo);

                Debug.Log($"[Straight 돌진 종료] 공격 대상: {hit.collider.name}");
                return true;
            }

            if (hit.collider.isTrigger) continue;

            if (Vector2.Dot(direction, hit.normal) >= 0f) continue;

            hitWall = true;
            Debug.Log($"[Straight 돌진 종료] 장애물: {hit.collider.name}");
            return true;
        }

        return false;
    }

    private RaycastHit2D[] CastChargeBody(Vector2 direction, float distance)
    {
        Bounds bodyBounds = EnemyMove.BodyCollider.bounds;
        float contactOffset = Physics2D.defaultContactOffset;
        Vector2 castSize = (Vector2)bodyBounds.size - Vector2.one * (contactOffset * 2f);

        return Physics2D.BoxCastAll(bodyBounds.center, castSize, 0f,
            direction, distance + contactOffset, _collisionMask);
    }

    private void FinishAttack()
    {
        _isCharging = false;
        RestoreEnemyBodyCollisions();
        _chargeCoroutine = null;
        EnemyMove.SetMoveSpeed(_baseMoveSpeed);
        _hasBaseMoveSpeed = false;
        EnemyCombat?.NotifyAttackFinished();
    }

    private void StopCharge()
    {
        _isCharging = false;
        RestoreEnemyBodyCollisions();
        EnemyAnimation?.HideChargeOutline();
        if (_chargeCoroutine != null)
        {
            StopCoroutine(_chargeCoroutine);
            _chargeCoroutine = null;
        }

        if (EnemyMove != null && _hasBaseMoveSpeed)
        {
            EnemyMove.SetMoveSpeed(_baseMoveSpeed);
            _hasBaseMoveSpeed = false;
            StopMoving();
        }
    }

    private void IgnoreEnemyBodyCollisions()
    {
        Rigidbody2D body = EnemyMove.Rigidbody;
        _originalBodyExclusions = body.excludeLayers;
        // 루트와 자식 몸체 Collider 모두 다른 적을 통과하도록 설정합니다.
        body.excludeLayers |= LayerMask.GetMask("Enemy", "EnemyBody");
        _ignoringEnemyBodies = true;
    }

    private void RestoreEnemyBodyCollisions()
    {
        if (!_ignoringEnemyBodies) return;

        EnemyMove.Rigidbody.excludeLayers = _originalBodyExclusions;
        _ignoringEnemyBodies = false;
    }
}
