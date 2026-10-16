using UnityEngine;

public class EnemyStateMachine : MonoBehaviour
{
    // Enemy State 필드
    private EnemyState _currentState;
    private EnemyStateMode _mode = EnemyStateMode.Standard;
    private bool _begun;
    private const float InitialIdleDuration = 1.5f;
    [SerializeField] private float _scoutChaseGraceDuration = 10f;
    private float _scoutChaseGraceDeadline;
    private bool _scoutChaseGraceActive;
    private EnemyState _stateBeforeHit;
    private EnemyState _stateBeforeStun;
    private EnemyAwarenessBubble _awarenessBubble;
    private bool _hasDetectedTarget;
    private bool _hasShownReturn;
    private bool _investigatingShot;

    private Enemy _owner;
    private EnemyIdleState _idleState;
    private EnemyPatrolState _patrolState;
    private EnemyChaseState _chaseState;
    private EnemyReturnState _returnState;
    private EnemyAttackState _attackState;
    private EnemyHitState _hitState;
    private EnemyDeathState _deathState;
    private EnemyStunState _stunState;

    // 프로퍼티
    public Enemy Owner => _owner;
    public EnemyIdleState IdleState => _idleState;
    public EnemyPatrolState PatrolState => _patrolState;
    public EnemyChaseState ChaseState => _chaseState;
    public EnemyReturnState ReturnState => _returnState;
    public EnemyAttackState AttackState => _attackState;
    public EnemyHitState HitState => _hitState;
    public EnemyDeathState DeathState => _deathState;
    public bool IsStunned => _currentState == _stunState;
    public EnemyState CurrentState => _currentState;
    public bool HasBegun => _begun;
    public EnemyStateMode Mode => _mode;
    public bool IsInvestigatingShot => _investigatingShot;
    public bool ScoutChaseGraceActive => _scoutChaseGraceActive;

    private void Awake()
    {
        _owner = GetComponent<Enemy>();
        _awarenessBubble = GetComponent<EnemyAwarenessBubble>();
        _idleState = new EnemyIdleState(this);
        _patrolState = new EnemyPatrolState(this);
        _chaseState = new EnemyChaseState(this);
        _returnState = new EnemyReturnState(this);
        _attackState = new EnemyAttackState(this);
        _hitState = new EnemyHitState(this);
        _deathState = new EnemyDeathState(this);
        _stunState = new EnemyStunState(this);
    }

    public void SetMode(EnemyStateMode stateMode)
    {
        if (_begun) return;

        _mode = stateMode;
    }

    internal void ResetForPool()
    {
        _begun = false;
        _currentState = null;
        _stateBeforeHit = null;
        _stateBeforeStun = null;
        _stunState.Reset();
        _hasDetectedTarget = false;
        _hasShownReturn = false;
        _investigatingShot = false;
        _scoutChaseGraceActive = false;
        _awarenessBubble?.Hide();
    }

    public void Begin()
    {
        if (_begun) return;

        if (_owner == null)
        {
            Debug.LogError($"[EnemyStateMachine.cs : Begin] - Enemy is null");
            return;
        }

        _begun = true;

        switch (_mode)
        {
            case EnemyStateMode.Standard:
                EnterIdle();
                break;

            case EnemyStateMode.Destruction:
                ChangeState(_chaseState);
                break;
        }
    }

    private void Update()
    {
        if (_begun)
        {
            _currentState?.Tick();
        }
    }

    public void ChangeState(EnemyState nextState)
    {
        if (!_begun || nextState == null || _currentState == nextState) return;

        if (_mode == EnemyStateMode.Standard && nextState == _returnState)
        {
            _scoutChaseGraceActive = false;
            _investigatingShot = false;
            if (!_hasShownReturn)
            {
                _awarenessBubble?.ShowReturn();
                _hasShownReturn = true;
            }
        }
        else if (nextState == _deathState)
        {
            _awarenessBubble?.Hide();
        }

        // Debug.Log($"상태를 변환합니다 : {_currentState?.GetType().Name ?? "없음"} --> {nextState.GetType().Name}");
        _currentState?.Exit();
        _currentState = nextState;
        _currentState.Enter();
    }

    public void TargetVisible()
    {
        if (_mode != EnemyStateMode.Standard) return;

        if (_hasDetectedTarget) return;

        _hasDetectedTarget = true;
        _awarenessBubble?.ShowAlert();
    }

    public void EndSearch()
    {
        _hasDetectedTarget = false;
        _hasShownReturn = false;
        _scoutChaseGraceActive = false;
        _investigatingShot = false;
    }

    public void InvestigateShot()
    {
        if (_mode != EnemyStateMode.Standard || !_begun || !_owner.EnemySensor.HasTarget || _owner.IsDead ||
            (_currentState != _idleState && _currentState != _patrolState)) return;

        _owner.EnemySensor.RememberTargetPosition();
        _investigatingShot = true;
        _hasDetectedTarget = true;
        _awarenessBubble?.ShowAlert();
        RefreshScoutChaseGrace();
        ChangeState(_chaseState);
    }

    public void CompleteShotInvestigation()
    {
        _investigatingShot = false;
    }

    public bool ScoutChaseGraceExpired()
    {
        if (_mode != EnemyStateMode.Standard) return false;

        if (!_owner.IsOutsideScoutRange())
        {
            _scoutChaseGraceActive = false;
            return false;
        }

        if (!_scoutChaseGraceActive)
        {
            _scoutChaseGraceActive = true;
            _scoutChaseGraceDeadline = Time.time + _scoutChaseGraceDuration;
        }

        return Time.time >= _scoutChaseGraceDeadline;
    }

    public void RefreshScoutChaseGrace()
    {
        if (_mode == EnemyStateMode.Standard && _scoutChaseGraceActive && _owner.IsOutsideScoutRange())
            _scoutChaseGraceDeadline = Time.time + _scoutChaseGraceDuration;
    }

    // =========================
    // Idle
    // =========================
    public void EnterIdle(float duration = InitialIdleDuration)
    {
        _idleState.SetDuration(duration);
        ChangeState(_idleState);
    }

    public void EnterHit()
    {
        if (_currentState == _hitState ||
            _currentState == _deathState ||
            IsStunned)
        {
            return;
        }

        // Patrol State 중 맞았다면 Chase State로 전환
        if (_currentState == _idleState || _currentState == _patrolState)
        {
            _owner.EnemySensor.RememberTargetPosition();
            _stateBeforeHit = _chaseState;
            TargetVisible();
        }
        else
        {
            _stateBeforeHit = _currentState;
        }

        ChangeState(_hitState);
    }

    public void ReturnFromHit()
    {
        if (_stateBeforeHit == null)
        {
            EnterIdle();
            return;
        }

        EnemyState previousState = _stateBeforeHit;
        _stateBeforeHit = null;

        ChangeState(previousState);
    }

    public void EnterStun(float duration)
    {
        if (!_begun || _currentState == _deathState || duration <= 0f) return;

        _stunState.Extend(duration);
        if (IsStunned) return;

        _stateBeforeStun = _currentState == _hitState ? _stateBeforeHit : _currentState;
        _stateBeforeHit = null;
        ChangeState(_stunState);
    }

    public void ReturnFromStun()
    {
        EnemyState previousState = _stateBeforeStun;
        _stateBeforeStun = null;

        if (previousState == _attackState || previousState == _hitState || previousState == null)
        {
            previousState = _mode == EnemyStateMode.Destruction || _owner.EnemySensor.HasTarget
                ? _chaseState
                : _patrolState;
        }

        ChangeState(previousState);
    }
}
