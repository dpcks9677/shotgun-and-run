using UnityEngine;

public class EnemyDeathState : EnemyState
{
    private float _elapsedTime;
    private const float DeathDuration = 2.0f;

    public EnemyDeathState(EnemyStateMachine machine) : base(machine)
    {
    }

    public override void Enter()
    {
        _elapsedTime = 0.0f;
        Machine.Owner.PerformDeathBehavior();
    }

    public override void Tick()
    {
        _elapsedTime += Time.deltaTime;

        AnimateFadeOut();

        if (_elapsedTime >= DeathDuration)
        {
            Machine.Owner.Die();
        }
    }

    private void AnimateFadeOut()
    {
        float ratio = Mathf.Clamp01((DeathDuration - _elapsedTime) / DeathDuration);
        Machine.Owner.SetSpriteAlpha(ratio);
    }
}
