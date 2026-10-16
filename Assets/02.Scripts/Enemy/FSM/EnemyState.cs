public abstract class EnemyState
{
    protected readonly EnemyStateMachine Machine;

    protected EnemyState(EnemyStateMachine machine)
    {
        Machine = machine;
    }

    public virtual void Enter()
    {
    }

    public abstract void Tick();

    public virtual void Exit()
    {
    }
}
