using UnityEngine;

public class EnemyPatrolState : EnemyState
{
    private const float MinimumPatrolDuration = 1.5f;
    private const float MaximumPatrolDuration = 3.5f;
    private const float MinimumIdleDurationAfterPatrol = 0.5f;
    private const float MaximumIdleDurationAfterPatrol = 2.5f;

    private float _elapsedTime;
    private float _patrolDuration;

    public EnemyPatrolState(EnemyStateMachine machine) : base(machine)
    {
    }

    public override void Enter()
    {
        _elapsedTime = 0.0f;
        _patrolDuration = Random.Range(MinimumPatrolDuration, MaximumPatrolDuration);
        Machine.Owner.EnemyMove.SetPatrolDirection();
    }

    public override void Tick()
    {
        if (Machine.ScoutChaseGraceActive)
        {
            if (!Machine.Owner.EnemySensor.HasTarget)
                Machine.EndSearch();
            else
            {
                if (Machine.ScoutChaseGraceExpired())
                {
                    Machine.ChangeState(Machine.ReturnState);
                    return;
                }

                if (Machine.ScoutChaseGraceActive)
                {
                    Machine.ChangeState(Machine.ChaseState);
                    return;
                }
            }
        }

        // 1. 정찰 중 정찰 범위를 벗어난 경우 => Return State로 복귀
        if (Machine.Owner.IsOutsideScoutRange())
        {
            Machine.ChangeState(Machine.ReturnState);
            return;
        }

        // 2. 탐지 거리 반경 + LOS의 경우 추격
        if (Machine.Owner.EnemySensor.IsTargetInDetectionRange() && Machine.Owner.EnemySensor.HasLineOfSightToTarget())
        {
            Machine.TargetVisible();
            Machine.ChangeState(Machine.ChaseState);
            return;
        }

        EnemyMove move = Machine.Owner.EnemyMove;

        // 물리 충돌로 완전히 멈춘 경우에는 새 방향을 고른 뒤,
        // 전방 장애물 조향으로 실제 이동 가능한 방향을 다시 계산합니다.
        if (move.IsBlocked)
        {
            move.SetPatrolDirection();
        }

        Machine.Owner.StartMoving(move.GetPatrolSteeringDirection());

        _elapsedTime += Time.deltaTime;

        if (_elapsedTime >= _patrolDuration)
        {
            float duration = Random.Range(MinimumIdleDurationAfterPatrol, MaximumIdleDurationAfterPatrol);
            Machine.EnterIdle(duration);
        }
    }
}
