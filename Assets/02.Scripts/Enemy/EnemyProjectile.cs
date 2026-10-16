using ShotgunAndRun.Combat;
using UnityEngine;

public sealed class EnemyProjectile : MonoBehaviour
{
    private const float VisualLength = 1f;
    private const float VisualWidth = 0.1f;

    private Vector2 _direction;
    private float _speed;
    private int _damage;
    private float _remainingLifetime;
    private LayerMask _collisionMask;
    private GameObject _source;
    private LineRenderer _lineRenderer;
    private static Material _projectileMaterial;

    public void Initialize(
        Vector2 position,
        Vector2 direction,
        float speed,
        int damage,
        float lifetime,
        LayerMask collisionMask,
        GameObject source)
    {
        transform.position = position;
        _direction = direction.normalized;
        _speed = Mathf.Max(0f, speed);
        _damage = Mathf.Max(0, damage);
        _remainingLifetime = Mathf.Max(0.1f, lifetime);
        _collisionMask = collisionMask;
        _source = source;

        EnsureVisual();
        UpdateVisual();
    }

    private void Update()
    {
        float moveDistance = _speed * Time.deltaTime;

        if (TryHit(moveDistance))
        {
            Destroy(gameObject);
            return;
        }

        transform.position += (Vector3)(_direction * moveDistance);
        UpdateVisual();

        _remainingLifetime -= Time.deltaTime;

        if (_remainingLifetime <= 0f)
        {
            Destroy(gameObject);
        }
    }

    private bool TryHit(float distance)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(
            transform.position,
            _direction,
            distance,
            _collisionMask);

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null ||
                (_source != null && hit.collider.transform.IsChildOf(_source.transform)))
            {
                continue;
            }

            if (hit.collider.GetComponentInParent<Enemy>() != null)
            {
                continue;
            }

            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();

            if (damageable != null)
            {
                DamageInfo damageInfo = new DamageInfo(
                    _damage,
                    hit.point,
                    _direction,
                    _source);
                if (damageable.TryTakeDamage(damageInfo))
                {
                    // 쏜 적이 이미 파괴됐을 수 있으므로 ?. 대신 Unity null 비교를 씁니다.
                    if (_source != null && _source.TryGetComponent(out Enemy enemy))
                    {
                        enemy.EnemyCombat.NotifyAttackHit();
                    }
                }

                return true;
            }

            if (!hit.collider.isTrigger)
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureVisual()
    {
        if (_lineRenderer != null)
        {
            return;
        }

        _lineRenderer = gameObject.AddComponent<LineRenderer>();
        _lineRenderer.useWorldSpace = true;
        _lineRenderer.positionCount = 2;
        _lineRenderer.startWidth = VisualWidth;
        _lineRenderer.endWidth = VisualWidth;
        _lineRenderer.startColor = Color.white;
        _lineRenderer.endColor = Color.white;
        _lineRenderer.sharedMaterial = GetProjectileMaterial();
        _lineRenderer.sortingLayerName = "Bullet";
    }

    private void UpdateVisual()
    {
        if (_lineRenderer == null) return;

        Vector2 head = transform.position;
        Vector2 tail = head - _direction * VisualLength;
        _lineRenderer.SetPosition(0, tail);
        _lineRenderer.SetPosition(1, head);
    }

    private static Material GetProjectileMaterial()
    {
        if (_projectileMaterial != null)
        {
            return _projectileMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        _projectileMaterial = new Material(shader)
        {
            name = "Enemy Projectile (Runtime)",
            hideFlags = HideFlags.HideAndDontSave
        };
        return _projectileMaterial;
    }
}
