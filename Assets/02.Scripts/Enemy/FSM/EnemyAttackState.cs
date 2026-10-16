public class EnemyAttackState : EnemyState
{
    private const float IdleDurationAfterAttack = 1.0f;
    public float IdleDurationOfAttack => IdleDurationAfterAttack;

    public EnemyAttackState(EnemyStateMachine machine) : base(machine)
    {
    }

    public override void Enter()
    {
        Machine.Owner.PerformAttackBehavior();


        if (!Machine.Owner.TryStartAttack())
        {
            Machine.EnterIdle(Machine.Owner.IdleDurationAfterAttack);
        }
    }

    public override void Tick()
    {
    }
}
