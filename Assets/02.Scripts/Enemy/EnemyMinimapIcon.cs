using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class EnemyMinimapIcon : MonoBehaviour
{
    private SpriteRenderer _spriteRenderer;
    private EnemyHealth _enemyHealth;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _enemyHealth = GetComponentInParent<EnemyHealth>();
    }

    private void OnEnable()
    {
        if (_enemyHealth == null)
        {
            _enemyHealth = GetComponentInParent<EnemyHealth>();
        }

        if (_enemyHealth != null)
        {
            _enemyHealth.DeathEvent -= HandleDeath;
            _enemyHealth.DeathEvent += HandleDeath;

            if (_spriteRenderer != null)
            {
                _spriteRenderer.enabled = !_enemyHealth.IsDead;
            }
        }
        else if (_spriteRenderer != null)
        {
            _spriteRenderer.enabled = true;
        }
    }

    private void OnDisable()
    {
        if (_enemyHealth != null)
        {
            _enemyHealth.DeathEvent -= HandleDeath;
        }
    }

    private void HandleDeath()
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.enabled = false;
        }
    }
}
