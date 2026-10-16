using System.Collections;
using ShotgunAndRun.Combat;
using ShotgunAndRun.Core;
using ShotgunAndRun.Enemy;
using UnityEngine;

[RequireComponent(typeof(EnemyHealth), typeof(EnemyMove), typeof(EnemySensor))]
[RequireComponent(typeof(EnemyCombat), typeof(EnemyAnimation))]
public class Enemy : MonoBehaviour, IAttacker, IStunnable
{
    // Core Components
    private EnemyHealth _enemyHealth;
    private EnemyMove _enemyMove;
    private EnemySensor _enemySensor;
    private EnemyCombat _enemyCombat;
    private EnemyAnimation _enemyAnimation;
    private EnemySound _enemySound;
    private EnemyStateMachine _stateMachine;
    private SpriteRenderer _bodyRenderer;
    private Collider2D _stunAnchorCollider;
    private SpriteRenderer _stunRenderer;
    private Animator _stunAnimator;
    private RuntimeAnimatorController _stunController;
    private Collider2D[] _colliders;
    private bool[] _initialColliderStates;

    // Runtime Field
    private EnemyType _enemyType;
    private int _power;

    // Return 관련 Field & Property
    [SerializeField] private float _scoutRange = 12f;
    [SerializeField] private float _homeArrivalDistance = 0.4f;
    [SerializeField] private float _agentBlockDelay = 0.5f;

    private Vector2 _spawnPosition;
    private float _blockedSince = -1f;
    private bool _avoidingAgent;

    public Vector2 SpawnPosition
    {
        get => _spawnPosition;
        set => _spawnPosition = value;
    }

    public float ScoutRange
    {
        get => _scoutRange;
        set => _scoutRange = value;
    }

    // Property
    public EnemyHealth EnemyHealth => _enemyHealth;
    public EnemyMove EnemyMove => _enemyMove;
    public EnemySensor EnemySensor => _enemySensor;
    public EnemyCombat EnemyCombat => _enemyCombat;
    public EnemyAnimation EnemyAnimation => _enemyAnimation;
    public EnemySound EnemySound => _enemySound;
    public bool IsDead => _enemyHealth.IsDead;
    public bool IsStunned => _stateMachine != null && _stateMachine.IsStunned;
    protected EnemyStateMachine StateMachine => _stateMachine;

    public EnemyType EnemyType => _enemyType;
    public string DisplayName => GetEnemyKoreanName(_enemyType);
    public int Power => _power;
    public float HomeArrivalDistance => _homeArrivalDistance;
    internal Bounds IndicatorAnchorBounds => _stunAnchorCollider != null
        ? _stunAnchorCollider.bounds
        : _bodyRenderer.bounds;
    public virtual bool IsAttackInterruptible => true;
    public virtual bool UsesProceduralHitFeedback => true;
    public virtual bool CanBeKnockedBack => true;
    public virtual bool PursueTargetWithoutLineOfSight => false;
    public virtual float IdleDurationAfterAttack => _stateMachine.AttackState.IdleDurationOfAttack;
    protected virtual bool PlayAnimationWhenAttackStarts => true;

    public static string GetEnemyKoreanName(EnemyType type)
    {
        return type switch
        {
            EnemyType.Normal => "고블린",
            EnemyType.Straight => "박쥐",
            EnemyType.Sniper => "해골 병사",
            EnemyType.Thrower => "고블린 폭탄병",
            EnemyType.Tanker => "무장 고블린",
            EnemyType.EliteSniper => "정예 해골 병사",
            EnemyType.Dummy => "훈련용 더미",
            _ => "적 괴물"
        };
    }

    // ==================================
    // Enemy 생성 및 초기 상태
    // ==================================
    protected virtual void Awake()
    {
        // Core Component 연결
        _enemyHealth = GetComponent<EnemyHealth>();
        _enemyMove = GetComponent<EnemyMove>();
        _enemySensor = GetComponent<EnemySensor>();
        _enemyCombat = GetComponent<EnemyCombat>();
        _enemyAnimation = GetComponent<EnemyAnimation>();
        _enemySound = GetComponent<EnemySound>();
        _stateMachine = GetComponent<EnemyStateMachine>();
        _bodyRenderer = GetComponent<SpriteRenderer>();
        Transform stunAnchor = transform.Find("HitBox") ?? transform.Find("EnemyBody");
        _stunAnchorCollider = stunAnchor != null ? stunAnchor.GetComponent<Collider2D>() : null;
    }

    internal void CaptureInitialColliderStates()
    {
        _colliders = GetComponentsInChildren<Collider2D>(true);
        _initialColliderStates = new bool[_colliders.Length];
        for (int i = 0; i < _colliders.Length; i++)
            _initialColliderStates[i] = _colliders[i].enabled;
    }

    public void Initialize(EnemyData data)
    {
        if (data == null)
        {
            Debug.LogError("[Enemy.cs : Initialize] - EnemyData is null");
            return;
        }

        if (_enemyHealth == null || _enemyMove == null || _enemySensor == null ||
            _enemyCombat == null || _enemyAnimation == null)
        {
            Debug.LogError($"[Enemy.cs : Initialize] - {name}에 필요한 Core 컴포넌트가 없습니다.");
            return;
        }

        _enemyType = data.enemyType;
        _power = data.power;

        _enemyHealth.Initialize(data.maxHp, data.isImmortal);
        _enemyMove.Initialize(data.moveSpeed, data.knockbackDistance, data.knockbackDuration);
        _enemySensor.Initialize(data.detectionRange, data.attackRange);
        _enemyCombat.Initialize(data.attackTimeout);
        _enemyAnimation.Initialize();
        if (_colliders != null)
        {
            for (int i = 0; i < _colliders.Length; i++)
                _colliders[i].enabled = _initialColliderStates[i];
        }
        _blockedSince = -1f;
        _avoidingAgent = false;

        OnInitialized(data);
    }

    // ==================================
    // Enemy 주요 행동
    // ==================================
    public void StartMoving(Vector3 direction)
    {
        if (_enemyMove == null || _enemyAnimation == null)
        {
            Debug.Log($"[Enemy.cs : Move] - EnemyMove or EnemyAnimation is null");
            return;
        }

        _enemyMove.Move(direction);

        if (_enemyMove.IsMoving)
        {
            _enemyAnimation.SetSpriteFlipByDirection(_enemyMove.CurrentDirection);
        }
    }

    public void StopMoving()
    {
        _enemyMove.Stop();
    }

    public void Stun(float duration, RuntimeAnimatorController indicatorController)
    {
        if (_enemyHealth.IsDead || indicatorController == null) return;

        _stunController = indicatorController;
        _stateMachine.EnterStun(duration);
    }

    public void ShowStunIndicator()
    {
        if (_stunRenderer == null)
        {
            GameObject indicator = new GameObject("StunIndicator", typeof(SpriteRenderer), typeof(Animator));
            indicator.transform.SetParent(transform, false);
            _stunRenderer = indicator.GetComponent<SpriteRenderer>();
            _stunAnimator = indicator.GetComponent<Animator>();
            _stunRenderer.sortingLayerID = _bodyRenderer.sortingLayerID;
            _stunRenderer.sortingOrder = _bodyRenderer.sortingOrder + 1;
        }

        _stunAnimator.runtimeAnimatorController = _stunController;
        _stunRenderer.gameObject.SetActive(true);
        PositionStunIndicator();
    }

    public void HideStunIndicator()
    {
        if (_stunRenderer != null)
        {
            _stunRenderer.gameObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (IsStunned && _stunRenderer != null)
        {
            PositionStunIndicator();
        }
    }

    private void PositionStunIndicator()
    {
        Transform indicator = _stunRenderer.transform;
        Bounds anchorBounds = IndicatorAnchorBounds;
        float y = anchorBounds.max.y + _stunRenderer.bounds.extents.y;
        indicator.position = new Vector3(anchorBounds.center.x, y, transform.position.z);
    }

    public virtual void MoveTowardsPosition(Vector2 targetPosition)
    {
        MoveTowardsPositionWithPath(targetPosition);
    }

    private void MoveTowardsPositionWithPath(Vector2 targetPosition)
    {
        if (_enemyMove.IsBeingKnockedBack) return;

        if (_enemySensor.HasLineOfSightToTarget() &&
            _enemyMove.CanMoveDirectly(targetPosition, false))
        {
            if (_enemyMove.CanMoveDirectly(targetPosition))
            {
                _avoidingAgent = false;
                _blockedSince = -1f;
            }
            else if (!_avoidingAgent)
            {
                if (_enemyMove.IsBlocked)
                {
                    if (_blockedSince < 0f) _blockedSince = Time.time;
                    if (Time.time - _blockedSince >= _agentBlockDelay)
                        _avoidingAgent = true;
                }
                else
                {
                    _blockedSince = -1f;
                }
            }

            if (!_avoidingAgent)
            {
                StartMoving(targetPosition - (Vector2)transform.position);
                return;
            }
        }
        else
        {
            _avoidingAgent = false;
            _blockedSince = -1f;
        }

        _enemyMove.MoveUsingAstar(targetPosition);

        if (_enemyMove.IsMoving)
            _enemyAnimation.SetSpriteFlipByDirection(_enemyMove.CurrentDirection);
    }


    public bool TryStartAttack()
    {
        if (!_enemySensor.HasTarget) return false;

        StopMoving();
        _enemyAnimation?.SetSpriteFlipImmediately(_enemySensor.DirectionToTarget());

        return _enemyCombat.TryStartAttack();
    }

    public virtual void PerformChaseBehavior()
    {
        // 1. 공격 범위 안에 들어오면 공격
        if (_enemySensor.IsTargetInAttackRange())
        {
            _stateMachine?.ChangeState(_stateMachine.AttackState);
            return;
        }

        // 2. 아직 공격 범위가 아닌 경우 추격
        MoveTowardsPosition(_enemySensor.TargetTransform.position);
    }

    public virtual void PerformReturnBehavior()
    {
        MoveTowardsPosition(SpawnPosition);
    }

    public virtual void OnChaseInterrupted()
    {
    }

    public void PerformAttackBehavior()
    {
    }

    public void PerformDeathBehavior()
    {
        // 1. 정지 / 공격 중단 / 죽음 Animation 실행
        StopMoving();
        CancelAttack();
        PlayDeathAnimation();

        // 2. Collider 밀림 방지
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>();

        foreach (Collider2D collider in colliders)
        {
            collider.enabled = false;
        }
    }

    public bool CancelAttack()
    {
        return _enemyCombat != null && _enemyCombat.CancelAttack();
    }

    public void SetSpriteAlpha(float alpha)
    {
        _enemyAnimation?.SetSpriteAlpha(alpha);
    }

    public void BeginSpawnReveal(float duration)
    {
        for (int i = 0; i < _colliders.Length; i++)
            _colliders[i].enabled = false;

        SetSpriteAlpha(0f);
        StartCoroutine(SpawnRevealRoutine(duration));
    }

    private IEnumerator SpawnRevealRoutine(float duration)
    {
        RunSession run = GameManager.Instance?.CurrentRun;
        float startTime = run != null ? run.Elapsed : Time.time;

        while (true)
        {
            float elapsed = (run != null ? run.Elapsed : Time.time) - startTime;
            if (elapsed >= duration) break;

            SetSpriteAlpha(elapsed / duration);
            yield return null;
        }

        SetSpriteAlpha(1f);
        for (int i = 0; i < _colliders.Length; i++)
            _colliders[i].enabled = _initialColliderStates[i];

        if (_enemyHealth.IsDead)
            Die();
        else
            _stateMachine.Begin();
    }

    public void Die()
    {
        EnemyLayer.Release(this);
    }

    internal void ResetForPool()
    {
        _enemyMove.StopAstarMovement();
        _enemySensor.ClearTargetTransform();
        _enemySensor.ClearLastSeenPosition();
        _stateMachine.ResetForPool();
        _enemyHealth.ClearRuntimeEvents();
        _enemyHealth.ResetHealth();
        HideStunIndicator();
        _stunController = null;
    }

    // ==================================
    // Enemy 주요 판단 혹은 상태 반환
    // ==================================
    public bool IsOutsideScoutRange()
    {
        return ((Vector2)transform.position - _spawnPosition).sqrMagnitude > _scoutRange * _scoutRange;
    }

    private bool CheckNullOfFSM()
    {
        return _stateMachine == null;
    }

    protected virtual void OnAttackSequenceStarted()
    {
    }

    protected virtual void OnInitialized(EnemyData data)
    {
    }

    protected virtual void OnAttackSequenceCancelled()
    {
    }

    protected void PlayAttackAnimation()
    {
        _enemyAnimation?.PlayAttack();
    }

    protected void PlayDeathAnimation()
    {
        _enemyAnimation?.PlayDeath();
    }

    private Vector2 ResolveKnockbackDirection(DamageInfo damageInfo)
    {
        Vector2 direction = damageInfo.HitDirection;

        if (direction.sqrMagnitude > Mathf.Epsilon)
        {
            return direction.normalized;
        }

        if (damageInfo.Source == null)
        {
            return Vector2.zero;
        }

        direction = transform.position - damageInfo.Source.transform.position;
        return direction.sqrMagnitude > Mathf.Epsilon ? direction.normalized : Vector2.zero;
    }

    // ==================================
    // Enemy 주요 이벤트 관리
    // ==================================
    protected virtual void OnEnable()
    {
        if (_enemyHealth != null)
        {
            _enemyHealth.HitEvent += HandleHitEvent;
            _enemyHealth.DeathEvent += HandleDeathEvent;
        }

        if (_enemyMove != null)
        {
            _enemyMove.MoveEvent += HandleMoveEvent;
        }

        if (_enemySensor != null)
        {
            _enemySensor.TargetDied += HandleTargetDied;
        }

        if (_enemyCombat != null)
        {
            _enemyCombat.AttackStarted += HandleAttackStarted;
            _enemyCombat.AttackFinished += HandleAttackFinished;
            _enemyCombat.AttackCancelled += HandleAttackCancelled;
        }
    }

    protected virtual void OnDisable()
    {
        if (_enemyHealth != null)
        {
            _enemyHealth.HitEvent -= HandleHitEvent;
            _enemyHealth.DeathEvent -= HandleDeathEvent;
        }

        if (_enemyMove != null)
        {
            _enemyMove.MoveEvent -= HandleMoveEvent;
        }

        if (_enemySensor != null)
        {
            _enemySensor.TargetDied -= HandleTargetDied;
        }

        if (_enemyCombat != null)
        {
            _enemyCombat.AttackStarted -= HandleAttackStarted;
            _enemyCombat.AttackFinished -= HandleAttackFinished;
            _enemyCombat.AttackCancelled -= HandleAttackCancelled;
        }
    }

    private void HandleHitEvent(DamageInfo damageInfo)
    {
        _stateMachine?.RefreshScoutChaseGrace();
        if (UsesProceduralHitFeedback)
        {
            _enemyAnimation?.PlayHitFeedback();
        }

        PlayHitSound();

        if (CanBeKnockedBack && !IsStunned)
        {
            _enemyMove.ApplyKnockback(ResolveKnockbackDirection(damageInfo));
        }

        if (CheckNullOfFSM()) return;

        if (_enemyCombat != null && _enemyCombat.IsAttacking && !IsAttackInterruptible)
        {
            return;
        }

        _stateMachine.EnterHit();
    }

    protected virtual void PlayHitSound()
    {
        _enemySound?.PlayHit();
    }

    private void HandleDeathEvent()
    {
        _enemySound?.PlayDeath();
        GameManager.Instance?.CurrentRun?.RegisterKill(_enemyType == EnemyType.EliteSniper);

        if (CheckNullOfFSM()) return;

        _stateMachine.ChangeState(_stateMachine.DeathState);
    }

    // 타깃(플레이어)이 사망하면 진행 중인 공격을 중단하고 Idle로 돌아갑니다.
    private void HandleTargetDied()
    {
        if (CheckNullOfFSM() || _enemyHealth.IsDead) return;

        CancelAttack();
        StopMoving();
        _stateMachine.EnterIdle();
    }

    private void HandleMoveEvent(bool isMoving)
    {
        _enemyAnimation?.PlayMove(isMoving);
        _enemySound?.SetMoving(isMoving);
    }

    private void HandleAttackStarted()
    {
        _stateMachine?.RefreshScoutChaseGrace();
        _enemySound?.PlayAttack();
        if (PlayAnimationWhenAttackStarts)
        {
            PlayAttackAnimation();
        }

        OnAttackSequenceStarted();
    }

    protected virtual void HandleAttackFinished()
    {
        if (CheckNullOfFSM() || IsStunned) return;

        _stateMachine.EnterIdle(IdleDurationAfterAttack);
    }

    private void HandleAttackCancelled()
    {
        _enemyAnimation?.CancelAttack();
        OnAttackSequenceCancelled();
    }
}
