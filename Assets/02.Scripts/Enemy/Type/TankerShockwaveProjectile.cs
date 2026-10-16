using System.Collections.Generic;
using ShotgunAndRun.Combat;
using UnityEngine;

public sealed class TankerShockwaveProjectile : MonoBehaviour
{
    [SerializeField] private float _speed = 6f;
    [SerializeField] private float _range = 12f;
    [SerializeField] private float _warningWidth = 2.5f;
    [SerializeField] private float _flipInterval = 0.12f;
    [SerializeField] private LayerMask _collisionMask = ~0;

    private Vector2 _direction;
    private float _remainingDistance;
    private CapsuleCollider2D _hitCollider;
    private float _nextFlipTime;
    private int _damage;
    private GameObject _source;
    private Enemy _owner;
    private HashSet<IDamageable> _hitTargets;
    private SpriteRenderer _renderer;

    public float Range => _range;
    public float WarningWidth => _warningWidth;

    private void Awake()
    {
        _hitCollider = GetComponent<CapsuleCollider2D>();
        _renderer = GetComponentInChildren<SpriteRenderer>();
    }

    public void Initialize(Vector2 direction, int damage, GameObject source,
        HashSet<IDamageable> hitTargets, SpriteRenderer bodyRenderer)
    {
        _direction = direction;
        if (direction.y != 0f)
            _hitCollider.size = Vector2.one * _hitCollider.size.y;
        float frontReach = (direction.x != 0f ? _hitCollider.size.x : _hitCollider.size.y) * 0.5f;
        _remainingDistance = _range - frontReach - Vector2.Dot(_hitCollider.offset, direction);
        _nextFlipTime = Time.time + _flipInterval;
        _damage = damage;
        _source = source;
        _owner = source.GetComponent<Enemy>();
        _hitTargets = hitTargets;

        _renderer.sortingLayerID = bodyRenderer.sortingLayerID;
        _renderer.sortingOrder = bodyRenderer.sortingOrder + 1;
        _renderer.flipX = direction == Vector2.left;
    }

    private void FixedUpdate()
    {
        float step = Mathf.Min(_speed * Time.fixedDeltaTime, _remainingDistance);
        Vector2 origin = transform.TransformPoint(_hitCollider.offset);
        float frontReach = (_direction.x != 0f ? _hitCollider.size.x : _hitCollider.size.y) * 0.5f;
        RaycastHit2D[] obstacles = Physics2D.RaycastAll(origin, _direction,
            frontReach + step, _collisionMask);
        foreach (RaycastHit2D obstacle in obstacles)
        {
            Collider2D collider = obstacle.collider;
            if (collider == _hitCollider || collider.isTrigger ||
                (_source != null && collider.transform.IsChildOf(_source.transform)) ||
                collider.GetComponentInParent<Enemy>() != null ||
                collider.GetComponentInParent<IDamageable>() != null) continue;

            if (obstacle.distance == 0f) continue;
            Destroy(gameObject);
            return;
        }

        RaycastHit2D[] hits = Physics2D.BoxCastAll(origin,
            _hitCollider.size, 0f, _direction, step, _collisionMask);

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == _hitCollider) continue;
            if ((_source != null && hit.collider.transform.IsChildOf(_source.transform)) ||
                hit.collider.GetComponentInParent<Enemy>() != null) continue;

            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null && IsTargetCenterInPath(((Component)target).transform.position, step))
            {
                if (_hitTargets.Add(target))
                {
                    DamageInfo damage = new DamageInfo(_damage, hit.point, _direction, _source);
                    if (target.TryTakeDamage(damage) && _owner != null)
                        _owner.EnemyCombat.NotifyAttackHit();
                }
            }
        }

        transform.position += (Vector3)(_direction * step);
        _remainingDistance -= step;
        if (_remainingDistance <= 0f) Destroy(gameObject);

        if (_direction.y != 0f && Time.time >= _nextFlipTime)
        {
            _renderer.flipX = !_renderer.flipX;
            _nextFlipTime += _flipInterval;
        }
    }

    private bool IsTargetCenterInPath(Vector2 targetCenter, float step)
    {
        Vector2 offset = targetCenter - (Vector2)transform.TransformPoint(_hitCollider.offset);
        float forward = Vector2.Dot(offset, _direction);
        float halfLength = (_direction.x != 0f ? _hitCollider.size.x : _hitCollider.size.y) * 0.5f;
        float halfWidth = (_direction.x != 0f ? _hitCollider.size.y : _hitCollider.size.x) * 0.5f;
        Vector2 side = new Vector2(-_direction.y, _direction.x);
        return forward >= -halfLength && forward <= halfLength + step &&
               Mathf.Abs(Vector2.Dot(offset, side)) <= halfWidth;
    }
}