using System;
using ShotgunAndRun.Combat;
using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private GameObject _bloodPrefab;
        
    // Field
    private int _currentHealth;
    private int _maxHealth;
    private bool _isImmortal;
    private bool _isDead;

    // Property
    public int CurrentHealth => _currentHealth;
    public int MaxHealth => _maxHealth;
    public bool IsImmortal => _isImmortal;
    public bool IsDead => _isDead;

    // Action Event
    public event Action<DamageInfo> HitEvent;
    public event Action DeathEvent;
    public Func<DamageInfo, int> DamageModifier { get; set; }

    public void Initialize(int maxHealth, bool isImmortal = false)
    {
        _maxHealth = maxHealth;
        _currentHealth = maxHealth;
        _isImmortal = isImmortal;
        _isDead = false;
    }

    public void ResetHealth()
    {
        _currentHealth = _maxHealth;
        _isDead = false;
    }

    internal void ClearRuntimeEvents()
    {
        HitEvent = null;
        DeathEvent = null;
        DamageModifier = null;
    }

    private void TakeDamage(DamageInfo damageInfo)
    {
        int damage = damageInfo.Damage;

        if (damage <= 0) return; // 1. 음수 입력 처리

        int minimumHealth = IsImmortal ? 1 : 0; // 2. 무적 처리 반영

        _currentHealth = Mathf.Max(minimumHealth, _currentHealth - damage);

        GameObject bloodObject = Instantiate(_bloodPrefab, transform.position, Quaternion.identity);
                    
        bloodObject.GetComponent<Blood>().Initialize(damageInfo.HitDirection);
        bloodObject.transform.localScale = transform.localScale;
        
        if (_currentHealth > 0)
        {
            HitEvent?.Invoke(damageInfo); // Hit Event 실행
        }
        else
        {
            _isDead = true;
            DeathEvent?.Invoke();
        }
    }

    public bool TryTakeDamage(DamageInfo damageInfo)
    {
        if (IsDead || damageInfo.Damage <= 0) return false;

        if (DamageModifier != null)
        {
            damageInfo = new DamageInfo(DamageModifier(damageInfo), damageInfo.HitPoint,
                damageInfo.HitDirection, damageInfo.Source);
        }
        TakeDamage(damageInfo);

        return true;
    }

    public bool TryHeal(int amount)
    {
        if (IsDead || amount <= 0) return false;

        _currentHealth = Mathf.Min(_maxHealth, CurrentHealth + amount);

        return true;
    }
}
