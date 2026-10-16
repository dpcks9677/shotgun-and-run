using UnityEngine;

public class EnemyIdleState : EnemyState
{
    private float _elapsedTime;
    private float _idleDuration;

    public EnemyIdleState(EnemyStateMachine machine) : base(machine)
    {
    }

    public override void Enter()
    {
        _elapsedTime = 0.0f;
        Machine.Owner.StopMoving();
    }

    // 대상을 발견하면 대기 시간을 끝내고 바로 추격합니다.
    public override void Tick()
    {
        EnemySensor sensor = Machine.Owner.EnemySensor;
        if (Machine.ScoutChaseGraceActive)
        {
            if (!sensor.HasTarget)
            {
                Machine.EndSearch();
                if (Machine.Owner.IsOutsideScoutRange())
                {
                    Machine.ChangeState(Machine.ReturnState);
                    return;
                }
            }
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

        if (sensor.IsTargetInDetectionRange() && sensor.HasLineOfSightToTarget())
        {
            Machine.TargetVisible();
            Machine.ChangeState(Machine.ChaseState);
            return;
        }

        _elapsedTime += Time.deltaTime;

        if (_elapsedTime >= _idleDuration)
        {
            ChangeNextState();
        }
    }

    private void ChangeNextState()
    {
        switch (Machine.Mode)
        {
            case EnemyStateMode.Standard:
                Machine.ChangeState(Machine.PatrolState);
                break;
            case EnemyStateMode.Destruction:
                Machine.ChangeState(Machine.ChaseState);
                break;
        }
    }

    public void SetDuration(float duration)
    {
        _idleDuration = Mathf.Max(0f, duration);
    }
}
