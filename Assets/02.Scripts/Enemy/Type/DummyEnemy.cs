using ShotgunAndRun.Combat;
using UnityEngine;

// 훈련용 더미: 이동/공격 없이 피격만 받으며, HP가 1 아래로 떨어지지 않음
public class DummyEnemy : Enemy
{
    [Header("Dummy")]
    [SerializeField, Min(1)] private int _dummyMaxHp = 999;
    [Tooltip("마지막 피격 후 이 시간 동안 맞지 않으면 HP를 전부 회복합니다. 단위: 초")]
    [SerializeField, Min(0f)] private float _regenDelay = 5.0f;
    [SerializeField] private Color _flashColor = Color.red;

    private float _lastHitTime;

    public override bool UsesProceduralHitFeedback => false;
    public override bool CanBeKnockedBack => false;

    protected override void Awake()
    {
        base.Awake();

        // 스포너 없이 씬에 직접 배치해도 동작하도록 스스로 초기화
        Initialize(new EnemyData
        {
            enemyType = EnemyType.Dummy,
            maxHp = _dummyMaxHp,
            isImmortal = true,
            moveSpeed = 0.0f,
            power = 0,
            knockbackDistance = 0f,
            knockbackDuration = 0f
        });

        EnemyMove.SetImmovable(true);
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (EnemyHealth != null)
        {
            EnemyHealth.HitEvent +=
                HandleDummyHit;
        }
    }

    protected override void OnDisable()
    {
        if (EnemyHealth != null)
        {
            EnemyHealth.HitEvent -=
                HandleDummyHit;
        }

        base.OnDisable();
    }

    private void Update()
    {
        if (EnemyHealth.CurrentHealth >= EnemyHealth.MaxHealth) return;

        if (Time.time - _lastHitTime < _regenDelay) return;

        EnemyHealth.TryHeal(EnemyHealth.MaxHealth);

        Debug.Log($"[DummyEnemy.cs : Update] - HP 회복 {EnemyHealth.CurrentHealth}/{EnemyHealth.MaxHealth}");
    }

    private void HandleDummyHit(DamageInfo damageInfo)
    {
        _lastHitTime = Time.time;
        EnemyAnimation?.PlayHitAnimation(_flashColor);

        Debug.Log(
            $"[DummyEnemy] 피격, HP " +
            $"{EnemyHealth.CurrentHealth}/" +
            $"{EnemyHealth.MaxHealth}");
    }
}
