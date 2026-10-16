using System.Collections;
using System.Collections.Generic;
using ShotgunAndRun.Combat;
using ShotgunAndRun.Core;
using UnityEngine;

public class TankerEnemy : Enemy, IPenetrationBlocker
{
    [Header("Contact Attack")]
    [SerializeField, Min(0f)] private float _attackWindup = 0.15f;
    [SerializeField, Min(0f)] private float _attackRecovery = 0.45f;
    [SerializeField, Min(0f)] private float _contactRadius = 1f;
    [SerializeField] private LayerMask _targetLayer = 1 << 6;

    [SerializeField] private float _chargeDistance = 9f;
    [SerializeField] private float _chargeWidth = 2f;
    [SerializeField] private float _chargeSpeed = 6f;
    [SerializeField] private float _chargePreparationDuration = 0.5f;
    [SerializeField] private float _chargeCooldown = 8f;
    [SerializeField] private float _chargeStepShakeMagnitude = 0.4f;
    [SerializeField] private float _chargeStepShakeDuration = 0.16f;
    [SerializeField] private float _chargeMoveAnimationSpeed = 1.5f;
    [SerializeField] private float _chargeMovePitch = 0.75f;
    [SerializeField] private float _chargeMoveVolumeMultiplier = 3f;
    [SerializeField] private LayerMask _chargeCollisionMask = ~0;
    [SerializeField] private TankerShockwaveProjectile _shockwavePrefab;
    [SerializeField] private float _shockwavePreparationDuration = 0.65f;
    [SerializeField] private float _shockwaveCooldown = 12f;
    [SerializeField] private AnimationClip _shockwaveAttackClip;

    private Coroutine _attackCoroutine;
    private Coroutine _chargeCoroutine;
    private Coroutine _shockwaveCoroutine;
    private TankerChargeIndicator _chargeIndicator;
    private TankerShockwaveIndicator _shockwaveIndicator;
    private float _nextChargeTime;
    private float _nextShockwaveTime;
    private float _baseMoveSpeed;
    private bool _chargeRequested;
    private bool _shockwaveRequested;
    private bool _isPreparingCharge;
    private bool _isCharging;
    private bool _isPreparingShockwave;
    private bool _isCastingShockwave;
    private LayerMask _originalBodyExclusions;
    private Animator _animator;
    private float _baseAnimatorSpeed;
    [SerializeField] private AnimationClip _defenceClip;
    [SerializeField] private float _defenceDuration = 7f;
    [SerializeField] private float _defenceCooldown = 15f;
    [SerializeField] private Color _defenceColor = new Color(0.4f, 0.6f, 0.8f, 1f);
    private float _defenceEndTime = float.NegativeInfinity;
    private float _nextDefenceTime;
    private float _defenceAnimationEndTime;
    private bool _defenceOutlineActive;

    private bool HasDefence => Time.time < _defenceEndTime;
    public override bool CanBeKnockedBack => !_isPreparingCharge && !_isCharging && !_isPreparingShockwave && !_isCastingShockwave;
    public override bool IsAttackInterruptible => CanBeKnockedBack;
    public override bool PursueTargetWithoutLineOfSight =>
        Time.time >= _nextShockwaveTime && EnemySensor.HasTarget &&
        EnemySensor.DistanceToTarget() <= _shockwavePrefab.Range;
    protected override bool PlayAnimationWhenAttackStarts => false;

    protected override void Awake()
    {
        base.Awake();
        _animator = GetComponent<Animator>();
        EnemyHealth.DamageModifier = ModifyDefenceDamage;
    }

    private int ModifyDefenceDamage(DamageInfo damageInfo)
    {
        if (!HasDefence || damageInfo.Source == null ||
            damageInfo.Source.GetComponentInParent<ShotgunAndRun.Player.Player>() == null)
            return damageInfo.Damage;

        return Mathf.Max(1, Mathf.CeilToInt(damageInfo.Damage * 0.5f));
    }

    protected override void OnInitialized(EnemyData data)
    {
        EnemyHealth.DamageModifier = ModifyDefenceDamage;
        _nextDefenceTime = Time.time;
        _nextChargeTime = Time.time + _chargeCooldown;
        _nextShockwaveTime = Time.time + _shockwaveCooldown;
    }

    public override void PerformChaseBehavior()
    {
        if (Time.time < _defenceAnimationEndTime)
        {
            EnemyMove.StopAstarMovement();
            return;
        }

        if (Time.time >= _nextDefenceTime && EnemySensor.HasTarget &&
            EnemySensor.HasLineOfSightToTarget())
        {
            EnemyMove.StopAstarMovement();
            _defenceEndTime = Time.time + _defenceDuration;
            _nextDefenceTime = Time.time + _defenceCooldown;
            _defenceAnimationEndTime = Time.time + _defenceClip.length;
            _defenceOutlineActive = true;
            EnemyAnimation.SetDefenceOutline(true, _defenceColor);
            EnemyAnimation.PlayDefence();
            EnemySound?.PlayDefence();
            return;
        }

        if (Time.time >= _nextShockwaveTime && EnemySensor.HasTarget &&
            EnemySensor.DistanceToTarget() <= _shockwavePrefab.Range)
        {
            _shockwaveRequested = true;
            StateMachine.ChangeState(StateMachine.AttackState);
            return;
        }

        if (Time.time >= _nextChargeTime && EnemySensor.HasTarget &&
            !EnemySensor.IsTargetInAttackRange() &&
            EnemySensor.DistanceToTarget() <= _chargeDistance &&
            EnemySensor.HasLineOfSightToTarget())
        {
            _chargeRequested = true;
            StateMachine.ChangeState(StateMachine.AttackState);
            return;
        }

        base.PerformChaseBehavior();
    }

    private void Update()
    {
        if (_defenceOutlineActive && (!HasDefence || EnemyHealth.IsDead))
        {
            _defenceOutlineActive = false;
            EnemyAnimation.SetDefenceOutline(false, _defenceColor);
        }
    }

    protected override void PlayHitSound()
    {
        if (_isPreparingCharge || _isPreparingShockwave) return;
        if (HasDefence) EnemySound?.PlayHitDefence();
        else base.PlayHitSound();
    }

    protected override void OnAttackSequenceStarted()
    {
        bool startCharge = _chargeRequested;
        bool startShockwave = _shockwaveRequested;
        StopAttackRoutine();
        if (startCharge)
        {
            _chargeRequested = false;
            _isPreparingCharge = true;
            _nextChargeTime = Time.time + _chargeCooldown;
            _chargeCoroutine = StartCoroutine(ChargeRoutine());
        }
        else if (startShockwave)
        {
            _isPreparingShockwave = true;
            _nextShockwaveTime = Time.time + _shockwaveCooldown;
            _shockwaveCoroutine = StartCoroutine(ShockwaveRoutine());
        }
        else
        {
            PlayAttackAnimation();
            _attackCoroutine = StartCoroutine(ContactAttackRoutine());
        }
    }

    protected override void OnAttackSequenceCancelled()
    {
        StopAttackRoutine();
    }

    protected override void HandleAttackFinished()
    {
        StopAttackRoutine();
        base.HandleAttackFinished();
    }

    protected override void OnDisable()
    {
        _defenceEndTime = float.NegativeInfinity;
        _defenceAnimationEndTime = 0f;
        _defenceOutlineActive = false;
        EnemyAnimation?.SetDefenceOutline(false, _defenceColor);
        StopAttackRoutine();
        if (_chargeIndicator != null)
        {
            Destroy(_chargeIndicator.gameObject);
            _chargeIndicator = null;
        }
        if (_shockwaveIndicator != null)
        {
            Destroy(_shockwaveIndicator.gameObject);
            _shockwaveIndicator = null;
        }
        base.OnDisable();
    }

    private IEnumerator ContactAttackRoutine()
    {
        yield return new WaitForSeconds(_attackWindup);

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            _contactRadius,
            _targetLayer);

        foreach (Collider2D hit in hits)
        {
            IDamageable damageable = hit.GetComponentInParent<IDamageable>();

            if (damageable == null)
            {
                continue;
            }

            Vector2 direction = EnemySensor.DirectionToTarget();
            Vector2 hitPoint = hit.ClosestPoint(transform.position);
            DamageInfo damageInfo = new DamageInfo(Power, hitPoint, direction, gameObject);

            EnemyCombat.NotifyAttackHit();
            damageable.TryTakeDamage(damageInfo);
            break;
        }

        yield return new WaitForSeconds(_attackRecovery);

        _attackCoroutine = null;
        EnemyCombat.NotifyAttackFinished();
    }

    private IEnumerator ChargeRoutine()
    {
        EnemyMove.StopAstarMovement();
        _baseMoveSpeed = EnemyMove.MoveSpeed;
        Vector2 direction = EnemySensor.DirectionToTarget();
        Vector2 start = EnemyMove.BodyCollider.bounds.center;
        EnemyAnimation.SetSpriteFlipImmediately(direction);

        if (_chargeIndicator == null)
        {
            GameObject indicator = new GameObject("TankerChargeIndicator");
            _chargeIndicator = indicator.AddComponent<TankerChargeIndicator>();
            _chargeIndicator.Initialize(GetComponent<SpriteRenderer>());
        }

        float elapsed = 0f;
        while (elapsed < _chargePreparationDuration)
        {
            _chargeIndicator.Show(start, direction, _chargeDistance, _chargeWidth,
                elapsed / _chargePreparationDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        _chargeIndicator.Show(start, direction, _chargeDistance, _chargeWidth, 1f);
        _isPreparingCharge = false;
        _isCharging = true;
        _baseAnimatorSpeed = _animator.speed;
        _animator.speed = _chargeMoveAnimationSpeed;
        EnemySound?.BeginManualMoveSound(_chargeMovePitch, _chargeMoveVolumeMultiplier);
        _originalBodyExclusions = EnemyMove.Rigidbody.excludeLayers;
        EnemyMove.Rigidbody.excludeLayers |= LayerMask.GetMask("Enemy", "EnemyBody");
        EnemyMove.SetMoveSpeed(_chargeSpeed);
        float travelled = 0f;

        while (travelled < _chargeDistance)
        {
            float step = Mathf.Min(_chargeSpeed * Time.fixedDeltaTime, _chargeDistance - travelled);
            if (ResolveChargeCollision(direction, step)) break;

            StartMoving(direction);
            yield return new WaitForFixedUpdate();
            travelled += step;
            float actualTravelled = Vector2.Dot(
                (Vector2)EnemyMove.BodyCollider.bounds.center - start, direction);
            _chargeIndicator.ShowRemaining(start, direction, _chargeDistance, _chargeWidth,
                actualTravelled);
        }

        _isCharging = false;
        _animator.speed = _baseAnimatorSpeed;
        EnemySound?.EndManualMoveSound();
        EnemyMove.Rigidbody.excludeLayers = _originalBodyExclusions;
        StopMoving();
        EnemyMove.SetMoveSpeed(_baseMoveSpeed);
        _chargeIndicator.Hide();
        EnemyAnimation.CancelAttack();
        _chargeCoroutine = null;
        EnemyCombat.NotifyAttackFinished();
    }

    public void OnChargeFootstep()
    {
        if (!_isCharging) return;
        EnemySound?.PlayManualMoveStep();
        CameraShakeSignal.Request(_chargeStepShakeMagnitude, _chargeStepShakeDuration);
    }

    private IEnumerator ShockwaveRoutine()
    {
        EnemyMove.StopAstarMovement();
        Vector2 center = EnemyMove.BodyCollider.bounds.center;
        SpriteRenderer bodyRenderer = GetComponent<SpriteRenderer>();
        if (_shockwaveIndicator == null)
        {
            GameObject indicator = new GameObject("TankerShockwaveIndicator");
            _shockwaveIndicator = indicator.AddComponent<TankerShockwaveIndicator>();
            _shockwaveIndicator.Initialize(bodyRenderer, _shockwavePrefab.Range,
                _shockwavePrefab.WarningWidth);
        }

        float elapsed = 0f;
        while (elapsed < _shockwavePreparationDuration)
        {
            _shockwaveIndicator.Show(center, _shockwavePrefab.Range,
                _shockwavePrefab.WarningWidth,
                elapsed / _shockwavePreparationDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        _shockwaveIndicator.Hide();
        _isPreparingShockwave = false;
        _isCastingShockwave = true;
        _animator.Play("Attack3", 0, 0f);
        yield return new WaitForSeconds(_shockwaveAttackClip.length);

        _isCastingShockwave = false;
        _nextChargeTime = Mathf.Max(_nextChargeTime, Time.time + _chargeCooldown);
        EnemyAnimation.CancelAttack();
        _shockwaveCoroutine = null;
        EnemyCombat.NotifyAttackFinished();
    }

    public void OnShockwaveRelease()
    {
        if (!_isCastingShockwave) return;

        Vector2 center = EnemyMove.BodyCollider.bounds.center;
        SpriteRenderer bodyRenderer = GetComponent<SpriteRenderer>();
        HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
        Vector2[] directions = { Vector2.right, Vector2.left, Vector2.up, Vector2.down };
        foreach (Vector2 direction in directions)
        {
            TankerShockwaveProjectile projectile = Instantiate(_shockwavePrefab, center,
                Quaternion.identity);
            projectile.Initialize(direction, Power, gameObject, hitTargets, bodyRenderer);
        }
    }

    private bool ResolveChargeCollision(Vector2 direction, float distance)
    {
        Bounds bounds = EnemyMove.BodyCollider.bounds;
        RaycastHit2D[] hits = Physics2D.BoxCastAll(bounds.center, bounds.size, 0f,
            direction, distance + Physics2D.defaultContactOffset, _chargeCollisionMask);

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform) ||
                hit.collider.GetComponentInParent<Enemy>() != null) continue;

            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                DamageInfo damage = new DamageInfo(Power, hit.point, direction, gameObject);
                EnemyCombat.NotifyAttackHit();
                target.TryTakeDamage(damage);
                return true;
            }

            if (!hit.collider.isTrigger &&
                (hit.distance == 0f || Vector2.Dot(direction, hit.normal) < 0f))
                return true;
        }

        return false;
    }

    private void StopAttackRoutine()
    {
        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
            _attackCoroutine = null;
        }

        if (_chargeCoroutine != null)
        {
            StopCoroutine(_chargeCoroutine);
            _chargeCoroutine = null;
        }

        if (_shockwaveCoroutine != null)
        {
            StopCoroutine(_shockwaveCoroutine);
            _shockwaveCoroutine = null;
        }

        _chargeRequested = false;
        _shockwaveRequested = false;
        _isPreparingCharge = false;
        _isPreparingShockwave = false;
        _isCastingShockwave = false;
        if (_chargeIndicator != null) _chargeIndicator.Hide();
        if (_shockwaveIndicator != null) _shockwaveIndicator.Hide();
        if (_isCharging)
        {
            _isCharging = false;
            _animator.speed = _baseAnimatorSpeed;
            EnemySound?.EndManualMoveSound();
            EnemyMove.Rigidbody.excludeLayers = _originalBodyExclusions;
            EnemyMove.SetMoveSpeed(_baseMoveSpeed);
            StopMoving();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _contactRadius);
    }
}
