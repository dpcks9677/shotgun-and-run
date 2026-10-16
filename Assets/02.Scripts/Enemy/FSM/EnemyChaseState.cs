using UnityEngine;

public class EnemyChaseState : EnemyState
{
    private const float LastSeenArrivalDistance = 0.2f;

    public EnemyChaseState(EnemyStateMachine machine) : base(machine)
    {
    }

    public override void Enter()
    {

    }

    public override void Exit()
    {
        Machine.Owner.OnChaseInterrupted();
    }

    public override void Tick()
    {
        Enemy owner = Machine.Owner;
        EnemySensor sensor = owner.EnemySensor;

        switch (Machine.Mode)
        {
            case EnemyStateMode.Standard:
                TickStandard(owner, sensor);
                break;

            case EnemyStateMode.Destruction:
                TickDestruction(owner, sensor);
                break;
        }
    }

    private void TickStandard(Enemy owner, EnemySensor sensor)
    {
        if (!sensor.HasTarget)
        {
            if (owner.IsOutsideScoutRange())
                Machine.ChangeState(Machine.ReturnState);
            else
                FinishChaseState();
            return;
        }

        if (Machine.ScoutChaseGraceExpired())
        {
            Machine.ChangeState(Machine.ReturnState);
            return;
        }

        // 3. 탐지 범위 밖이거나 시야를 잃은 경우 마지막 목격 지점으로 이동합니다.
        // Straight Enemy는 시야가 막혀도 현재 플레이어 위치를 향해 우회합니다.
        bool inDetectionRange = sensor.IsTargetInDetectionRange();
        bool hasLineOfSight = (inDetectionRange || Machine.ScoutChaseGraceActive) &&
                              sensor.HasLineOfSightToTarget();
        if (Machine.IsInvestigatingShot)
        {
            if (!inDetectionRange || !hasLineOfSight)
            {
                owner.PerformChaseBehavior();
                return;
            }

            Machine.CompleteShotInvestigation();
        }

        if ((!inDetectionRange && !Machine.ScoutChaseGraceActive) ||
            (!hasLineOfSight && (!owner.PursueTargetWithoutLineOfSight || Machine.ScoutChaseGraceActive)))
        {
            MoveToLastSeenPosition(owner, sensor);
            return;
        }

        // 4. 시야에 보이면 발견을 알리고 추격 행동을 수행합니다.
        if (hasLineOfSight) Machine.TargetVisible();
        owner.PerformChaseBehavior(); // 추격 행동 수행
    }

    private void TickDestruction(Enemy owner, EnemySensor sensor)
    {
        if (!sensor.HasTarget)
        {
            owner.OnChaseInterrupted();
            owner.StopMoving();
            return;
        }

        owner.PerformChaseBehavior();
    }

    private void MoveToLastSeenPosition(Enemy owner, EnemySensor sensor)
    {
        owner.OnChaseInterrupted();
        // 1. 마지막 본 위치 없으면 정찰
        if (!sensor.HasLastSeenPosition)
        {
            FinishOrWait(owner);
            return;
        }

        // 2. 적 현재 위치, 플레이어 마지막으로 본 위치
        Vector2 currentPosition = owner.transform.position;
        Vector2 directionToLastSeen = sensor.LastSeenPosition - currentPosition;

        float arrivalDistance = Mathf.Max(LastSeenArrivalDistance, owner.EnemyMove.BodyRadius * 0.5f);

        if (directionToLastSeen.sqrMagnitude <= arrivalDistance * arrivalDistance)
        {
            FinishOrWait(owner);
            return;
        }

        owner.MoveTowardsPosition(sensor.LastSeenPosition);
    }

    private void FinishChaseState()
    {
        Machine.EndSearch();
        Machine.Owner.EnemySensor.ClearLastSeenPosition();
        Machine.ChangeState(Machine.PatrolState);
    }

    private void FinishOrWait(Enemy owner)
    {
        if (Machine.ScoutChaseGraceActive)
            owner.StopMoving();
        else
            FinishChaseState();
    }
}
