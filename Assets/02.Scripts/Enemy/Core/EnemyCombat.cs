using System;
using UnityEngine;

public class EnemyCombat : MonoBehaviour
{
    // Field
    private bool _isAttacking;
    private float _attackStartedTime;
    private float _attackTimeout = 5f;

    // Property
    public bool IsAttacking => _isAttacking;

    // Event
    public event Action AttackStarted;
    public event Action AttackHit;
    public event Action AttackFinished;
    public event Action AttackCancelled;

    public void Initialize(float attackTimeout)
    {
        _attackTimeout = Mathf.Max(0.1f, attackTimeout);
        ResetCombat();
    }

    public bool TryStartAttack()
    {
        if (_isAttacking) return false;

        _isAttacking = true;
        _attackStartedTime = Time.time;
        AttackStarted?.Invoke();

        return true;
    }

    public void NotifyAttackHit()
    {
        if (!_isAttacking)
        {
            return;
        }

        AttackHit?.Invoke();
    }

    public void NotifyAttackFinished()
    {
        if (!_isAttacking)
        {
            return;
        }

        _isAttacking = false;
        AttackFinished?.Invoke();
    }

    public bool CancelAttack()
    {
        if (!_isAttacking) return false;

        _isAttacking = false;
        AttackCancelled?.Invoke();

        return true;
    }

    public void ResetCombat()
    {
        _isAttacking = false;
        _attackStartedTime = 0f;
    }

    private void Update()
    {
        if (!_isAttacking || Time.time - _attackStartedTime < _attackTimeout)
        {
            return;
        }

        Debug.LogWarning(
            $"[EnemyCombat] {name}의 공격 종료 이벤트가 {_attackTimeout:0.##}초 동안 오지 않아 공격을 강제 종료합니다.",
            this);

        _isAttacking = false;
        AttackCancelled?.Invoke();
        AttackFinished?.Invoke();
    }
}
