using System.Collections;
using System.Collections.Generic;
using Consumable;
using ShotgunAndRun.Combat;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public sealed class EnemyBomb : MonoBehaviour
{
    private const float ExplosionSoundVolumeScale = 0.5f;

    [SerializeField] private ExplosionRangeIndicator _rangeIndicator;
    [SerializeField] private Transform _bombVisual;
    [SerializeField] private Light2D _bombLight;
    [SerializeField] private float _bombLightIntensity = 1.5f;
    [SerializeField] private float _bombLightBlinkInterval = 0.16f;
    [SerializeField] private float _bombLightPulseDuration = 0.04f;

    private float _explosionRadius;
    private float _fuseDuration;
    private int _damage;
    private LayerMask _targetLayer;
    private GameObject _source;
    private GrenadeExplosionVFX _explosionPrefab;
    private float _warningBlinkInterval;

    public void Initialize(
        Vector2 landingPosition,
        float explosionRadius,
        float fuseDuration,
        int damage,
        LayerMask targetLayer,
        GameObject source,
        GrenadeExplosionVFX explosionPrefab = null,
        float warningBlinkInterval = 0.15f)
    {
        transform.position = landingPosition;
        _explosionRadius = Mathf.Max(0.1f, explosionRadius);
        _fuseDuration = Mathf.Max(0.1f, fuseDuration);
        _damage = Mathf.Max(0, damage);
        _targetLayer = targetLayer;
        _source = source;
        _explosionPrefab = explosionPrefab;
        _warningBlinkInterval = warningBlinkInterval;

        _bombVisual.gameObject.SetActive(false);
        _rangeIndicator.Show(_explosionRadius);
        StartCoroutine(FuseRoutine());
    }

    private IEnumerator FuseRoutine()
    {
        float elapsedTime = 0f;
        Color warningColor = new Color(1f, 0.1f, 0f, 1f);

        while (elapsedTime < _fuseDuration)
        {
            elapsedTime += Time.deltaTime;
            float blend = Mathf.PingPong(elapsedTime / _warningBlinkInterval, 1f);
            Color color = Color.Lerp(warningColor, Color.white, blend);
            _rangeIndicator.SetColor(color);
            float lightPhase = Mathf.Repeat(elapsedTime, _bombLightBlinkInterval);
            float lightPulse = 1f - Mathf.Clamp01(lightPhase / _bombLightPulseDuration);
            _bombLight.intensity = _bombLightIntensity * lightPulse;
            yield return null;
        }

        Explode();
        Destroy(gameObject);
    }

    public void PlayFlight(Vector2 startPosition, float flightDuration, float arcHeight,
        float rollDistance, float rollDuration)
    {
        _bombVisual.position = startPosition;
        _bombVisual.gameObject.SetActive(true);
        float rollRadius = _bombVisual.GetComponent<SpriteRenderer>().sprite.bounds.extents.x;

        float durationScale = Mathf.Min(1f, _fuseDuration / (flightDuration + rollDuration));
        StartCoroutine(FlyRoutine(_bombVisual, startPosition,
            flightDuration * durationScale, arcHeight, rollDistance,
            rollDuration * durationScale, rollRadius));
    }

    private IEnumerator FlyRoutine(Transform visual, Vector2 startPosition, float duration, float arcHeight,
        float rollDistance, float rollDuration, float rollRadius)
    {
        Vector2 center = transform.position;
        Vector2 travelDirection = (center - startPosition).normalized;
        rollDistance = Mathf.Min(rollDistance, Vector2.Distance(startPosition, center));
        Vector2 landingPosition = center - travelDirection * rollDistance;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            float progress = elapsedTime / duration;
            Vector2 position = Vector2.Lerp(startPosition, landingPosition, progress);
            position.y += 4f * arcHeight * progress * (1f - progress);
            visual.position = position;

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        visual.position = landingPosition;

        elapsedTime = 0f;
        float rollAngle = -Mathf.Sign(travelDirection.x) * rollDistance / rollRadius * Mathf.Rad2Deg;
        while (elapsedTime < rollDuration)
        {
            float progress = elapsedTime / rollDuration;
            float easedProgress = 1f - (1f - progress) * (1f - progress);
            visual.position = Vector2.Lerp(landingPosition, center, easedProgress);
            visual.rotation = Quaternion.Euler(0f, 0f, rollAngle * easedProgress);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        visual.position = center;
        visual.rotation = Quaternion.Euler(0f, 0f, rollAngle);
    }

    private void Explode()
    {
        if (_explosionPrefab != null)
        {
            GrenadeExplosionVFX effect = Instantiate(_explosionPrefab, transform.position, Quaternion.identity);
            AudioSource explosionSound = effect.GetComponent<AudioSource>();
            explosionSound.Stop();
            explosionSound.volume *= ExplosionSoundVolumeScale;
            explosionSound.Play();
            effect.Play(_explosionRadius);
        }

        List<Collider2D> hits = GetExplosionTargets();
        HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();

        foreach (Collider2D hit in hits)
        {
            IDamageable damageable = hit.GetComponentInParent<IDamageable>();

            if (damageable == null || !damagedTargets.Add(damageable))
            {
                continue;
            }

            Vector2 hitPoint = hit.ClosestPoint(transform.position);
            Vector2 direction = ((Vector2)hit.transform.position - (Vector2)transform.position).normalized;
            DamageInfo damageInfo = new DamageInfo(_damage, hitPoint, direction, _source);
            if (damageable.TryTakeDamage(damageInfo))
            {
                // 던진 적이 이미 파괴됐을 수 있으므로 ?. 대신 Unity null 비교를 씁니다.
                if (_source != null && _source.TryGetComponent(out Enemy enemy))
                {
                    enemy.EnemyCombat.NotifyAttackHit();
                }
            }
        }
    }

    private List<Collider2D> GetExplosionTargets()
    {
        IReadOnlyList<Vector2> rangePoints = _rangeIndicator.Points;
        Vector2[] points = new Vector2[rangePoints.Count];
        for (int index = 0; index < points.Length; index++)
            points[index] = rangePoints[index];

        PolygonCollider2D area = gameObject.AddComponent<PolygonCollider2D>();
        area.isTrigger = true;
        area.points = points;

        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = _targetLayer,
            useTriggers = true
        };
        List<Collider2D> hits = new List<Collider2D>();
        area.Overlap(filter, hits);
        area.enabled = false;
        return hits;
    }
}