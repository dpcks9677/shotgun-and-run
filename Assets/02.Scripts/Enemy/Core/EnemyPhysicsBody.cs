using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyPhysicsBody : MonoBehaviour
{
    private static PhysicsMaterial2D _frictionlessMaterial;

    [Header("Physics Body")]
    [Tooltip("몸체 충돌에서 제외할 레이어입니다. Player는 접촉 공격을 위해 통과시킵니다.")]
    [SerializeField] private LayerMask _bodyCollisionExclusions = 1 << 6;

    private Rigidbody2D _rigidbody;
    private Collider2D _bodyCollider;

    public Rigidbody2D Rigidbody => _rigidbody;
    public Collider2D BodyCollider => _bodyCollider;

    public void Initialize(float bodyRadius)
    {
        _rigidbody = GetComponent<Rigidbody2D>();

        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody2D>();
        }

        ConfigureRigidbody();
        EnsureBodyCollider(bodyRadius);
    }

    public void SetImmovable(bool isImmovable)
    {
        if (_rigidbody == null)
        {
            return;
        }

        _rigidbody.linearVelocity = Vector2.zero;
        _rigidbody.bodyType = isImmovable
            ? RigidbodyType2D.Kinematic
            : RigidbodyType2D.Dynamic;
    }

    private void ConfigureRigidbody()
    {
        _rigidbody.bodyType = RigidbodyType2D.Dynamic;
        _rigidbody.gravityScale = 0f;
        _rigidbody.freezeRotation = true;
        _rigidbody.linearDamping = 0f;
        _rigidbody.angularDamping = 0f;
        _rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
        _rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _rigidbody.sleepMode = RigidbodySleepMode2D.NeverSleep;
    }

    private void EnsureBodyCollider(float bodyRadius)
    {
        foreach (Collider2D collider in GetComponents<Collider2D>())
        {
            if (!collider.isTrigger)
            {
                _bodyCollider = collider;
                break;
            }
        }

        if (_bodyCollider == null)
        {
            CircleCollider2D circle = gameObject.AddComponent<CircleCollider2D>();
            float scale = Mathf.Max(
                Mathf.Abs(transform.lossyScale.x),
                Mathf.Abs(transform.lossyScale.y));
            circle.radius = bodyRadius / Mathf.Max(0.0001f, scale);
            _bodyCollider = circle;
        }

        _bodyCollider.isTrigger = false;
        _bodyCollider.excludeLayers = _bodyCollisionExclusions;
        _bodyCollider.sharedMaterial = GetFrictionlessMaterial();
    }

    private static PhysicsMaterial2D GetFrictionlessMaterial()
    {
        if (_frictionlessMaterial != null)
        {
            return _frictionlessMaterial;
        }

        _frictionlessMaterial = new PhysicsMaterial2D("Enemy Body (Runtime)")
        {
            friction = 0f,
            bounciness = 0f,
            hideFlags = HideFlags.HideAndDontSave
        };

        return _frictionlessMaterial;
    }
}
