using UnityEngine;

public sealed class EnemyStunState : EnemyState
{
    private float _endTime;

    public EnemyStunState(EnemyStateMachine machine) : base(machine)
    {
    }

    public void Extend(float duration)
    {
        _endTime = Mathf.Max(_endTime, Time.time + duration);
    }

    internal void Reset()
    {
        _endTime = 0f;
    }

    public override void Enter()
    {
        Machine.Owner.CancelAttack();
        Machine.Owner.EnemyMove.CancelKnockback();
        Machine.Owner.StopMoving();
        Machine.Owner.ShowStunIndicator();
    }

    public override void Tick()
    {
        if (Time.time >= _endTime)
        {
            Machine.ReturnFromStun();
        }
    }

    public override void Exit()
    {
        Machine.Owner.HideStunIndicator();
    }
}
