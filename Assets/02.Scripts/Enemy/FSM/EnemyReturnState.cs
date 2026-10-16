using UnityEngine;

public sealed class EnemyReturnState : EnemyState
{
    public EnemyReturnState(EnemyStateMachine machine) : base(machine)
    {
    }

    public override void Enter()
    {
        Machine.Owner.EnemySensor.ClearLastSeenPosition();
    }

    public override void Tick()
    {
        Enemy owner = Machine.Owner;

        Vector2 directionHome = owner.SpawnPosition - (Vector2)owner.transform.position;

        if (directionHome.sqrMagnitude <=
            owner.HomeArrivalDistance * owner.HomeArrivalDistance)
        {
            owner.StopMoving();
            Machine.EndSearch();
            Machine.ChangeState(Machine.PatrolState);
            return;
        }

        owner.PerformReturnBehavior();
    }
}
