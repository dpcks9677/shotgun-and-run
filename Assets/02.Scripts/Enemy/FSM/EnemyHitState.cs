public class EnemyHitState : EnemyState
{
    public EnemyHitState(EnemyStateMachine machine) : base(machine)
    {
    }

    public override void Enter()
    {
        Machine.Owner.CancelAttack();
        Machine.Owner.StopMoving();
    }

    public override void Tick()
    {
        if (Machine.Owner.EnemyMove.IsBeingKnockedBack) return;

        Machine.ReturnFromHit();

    }

    public override void Exit()
    {
    }
}
