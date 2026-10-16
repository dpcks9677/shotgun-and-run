using System.Collections;
using ShotgunAndRun.Combat;
using UnityEngine;

public class SniperEnemy : Enemy
{
    [Header("Sniper Pattern")]
    [SerializeField, Min(0f)] protected float _trackingDuration = 1.5f;
    [SerializeField, Min(0f)] private float _blinkDuration = 0.5f;
    [SerializeField, Min(0f)] private float _lockedDelay = 0.3f;
    [SerializeField, Min(0.1f)] protected float _attackInterval = 4f;
    [SerializeField, Min(0.1f)] private float _projectileSpeed = 70f;
    [SerializeField, Min(0.1f)] private float _projectileLifetime = 3f;
    [SerializeField, Min(0.01f)] private float _aimLineWidth = 0.035f;
    [SerializeField] private Color _aimLineColor = new Color(1f, 0f, 0f, 0.8f);
    [SerializeField] private LayerMask _projectileCollisionMask = ~(1 << Layers.Water);

    private LineRenderer _aimLine;
    private Coroutine _attackCoroutine;
    private static Material _lineMaterial;

    public override bool IsAttackInterruptible => false;
    protected override bool PlayAnimationWhenAttackStarts => false;

    protected override void Awake()
    {
        base.Awake();
        EnsureAimLine();
    }

    public override void PerformChaseBehavior()
    {
        if (!EnemySensor.HasLineOfSightToTarget())
        {
            MoveTowardsPosition(EnemySensor.TargetTransform.position);
            return;
        }

        StateMachine.ChangeState(StateMachine.AttackState);
    }

    protected override void OnAttackSequenceStarted()
    {
        StopAttackRoutine();
        _attackCoroutine = StartCoroutine(SniperAttackRoutine());
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

    private IEnumerator SniperAttackRoutine()
    {
        float attackStartedTime = Time.time;

        TryUsePreChargeGimmick(out _);

        Vector2 aimPosition = GetTargetPosition();
        float elapsedTime = 0f;

        SetAimLineVisible(true);
        EnemySound?.PlayDraw();

        while (elapsedTime < _trackingDuration)
        {
            aimPosition = GetTargetPosition();
            FaceAimPosition(aimPosition);
            UpdateAimLine(aimPosition);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        elapsedTime = 0f;

        while (elapsedTime < _blinkDuration)
        {
            aimPosition = GetTargetPosition();
            FaceAimPosition(aimPosition);
            UpdateAimLine(aimPosition);
            SetAimLineVisible(Mathf.FloorToInt(elapsedTime / 0.1f) % 2 == 0);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        SetAimLineVisible(true);
        FaceAimPosition(aimPosition, true);
        UpdateAimLine(aimPosition);
        PlayAttackAnimation();

        elapsedTime = 0f;

        while (elapsedTime < _lockedDelay)
        {
            aimPosition = GetTargetPosition();
            FaceAimPosition(aimPosition, true);
            UpdateAimLine(aimPosition);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        aimPosition = GetTargetPosition();
        FaceAimPosition(aimPosition, true);
        UpdateAimLine(aimPosition);
        FireProjectile(aimPosition);
        EnemySound?.PlayShot();
        SetAimLineVisible(false);

        yield return WaitForAttackInterval(attackStartedTime);
        FinishAttack();
    }

    protected virtual bool TryUsePreChargeGimmick(out bool shouldSkipShot)
    {
        shouldSkipShot = false;
        return false;
    }

    private IEnumerator WaitForAttackInterval(float attackStartedTime)
    {
        float recoveryDuration = Mathf.Max(0f, _attackInterval - (Time.time - attackStartedTime));

        if (recoveryDuration > 0f)
        {
            yield return new WaitForSeconds(recoveryDuration);
        }
    }

    private void FinishAttack()
    {
        _attackCoroutine = null;
        EnemyCombat.NotifyAttackFinished();
    }

    protected Vector2 GetTargetPosition()
    {
        return EnemySensor.HasTarget
            ? EnemySensor.TargetTransform.position
            : transform.position + transform.right;
    }

    private void FaceAimPosition(Vector2 aimPosition, bool immediately = false)
    {
        Vector2 direction = aimPosition - (Vector2)transform.position;

        if (direction.sqrMagnitude > Mathf.Epsilon)
        {
            if (immediately)
            {
                EnemyAnimation.SetSpriteFlipImmediately(direction);
            }
            else
            {
                EnemyAnimation.SetSpriteFlipByDirection(direction);
            }
        }
    }

    private void FireProjectile(Vector2 targetPosition)
    {
        Vector2 origin = transform.position;
        Vector2 direction = (targetPosition - origin).normalized;

        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            direction = Vector2.right;
        }

        GameObject projectileObject = new GameObject("SniperProjectile");
        EnemyProjectile projectile = projectileObject.AddComponent<EnemyProjectile>();
        projectile.Initialize(
            origin,
            direction,
            _projectileSpeed,
            Power,
            _projectileLifetime,
            _projectileCollisionMask,
            gameObject);
    }

    private void EnsureAimLine()
    {
        if (_aimLine != null)
        {
            return;
        }

        GameObject lineObject = new GameObject("SniperAimLine");
        lineObject.transform.SetParent(transform, false);
        _aimLine = lineObject.AddComponent<LineRenderer>();
        _aimLine.useWorldSpace = true;
        _aimLine.positionCount = 2;
        _aimLine.startWidth = _aimLineWidth;
        _aimLine.endWidth = _aimLineWidth;
        _aimLine.startColor = _aimLineColor;
        _aimLine.endColor = _aimLineColor;
        _aimLine.numCapVertices = 0;
        _aimLine.sharedMaterial = GetLineMaterial();
        _aimLine.sortingLayerName = "Bullet";
        _aimLine.enabled = false;
    }

    private void UpdateAimLine(Vector2 targetPosition)
    {
        if (_aimLine == null) return;

        Vector2 origin = transform.position;
        RaycastHit2D wallHit = Physics2D.Linecast(
            origin,
            targetPosition,
            (1 << Layers.Obstacle) | (1 << Layers.Wall));

        Vector2 lineEnd = wallHit.collider != null
            ? wallHit.point
            : targetPosition;

        _aimLine.SetPosition(0, origin);
        _aimLine.SetPosition(1, lineEnd);
    }

    private void SetAimLineVisible(bool isVisible)
    {
        if (_aimLine != null)
        {
            _aimLine.enabled = isVisible;
        }
    }

    private void StopAttackRoutine()
    {
        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
            _attackCoroutine = null;
        }

        SetAimLineVisible(false);
    }

    private static Material GetLineMaterial()
    {
        if (_lineMaterial != null)
        {
            return _lineMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        _lineMaterial = new Material(shader)
        {
            name = "Enemy Line (Runtime)",
            hideFlags = HideFlags.HideAndDontSave
        };
        return _lineMaterial;
    }
}
