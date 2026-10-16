using System.Collections;
using ShotgunAndRun.Core;
using ShotgunAndRun.Player;
using UnityEngine;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
public class CameraRecoil : MonoBehaviour
{
    [SerializeField] private PlayerWeapon _weapon;
    [SerializeField, Min(0f)] private float _duration = 0.15f;
    [SerializeField, Min(0f)] private float _magnitude = 0.3f;

    [Header("Critical Hit Zoom")]
    [SerializeField, Min(1f)] private float _criticalHitZoomMultiplier = 1.1f;
    [SerializeField, Min(0f)] private float _criticalHitZoomInDuration = 0.03f;
    [SerializeField, Min(0f)] private float _criticalHitZoomOutDuration = 0.12f;
    [SerializeField, Min(0f)] private float _criticalHitZoomOffset = 0.8f;

    private Camera _camera;
    private Vector3 _restLocalPosition;
    private Vector3 _recoilOffset;
    private Vector3 _zoomOffset;
    private Coroutine _recoilCoroutine;
    private Coroutine _zoomCoroutine;
    private float _baseOrthographicSize;
    private bool _isSubscribed;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _restLocalPosition = transform.localPosition;

        if (_camera != null)
        {
            _baseOrthographicSize = _camera.orthographicSize;
        }
    }

    private void OnEnable()
    {
        ResolveWeapon();
        SubscribeWeapon();
        CameraShakeSignal.Requested += HandleShakeRequested;
    }

    private void Start()
    {
        ResolveWeapon();
        SubscribeWeapon();
    }

    private void OnDisable()
    {
        UnsubscribeWeapon();
        CameraShakeSignal.Requested -= HandleShakeRequested;

        if (_recoilCoroutine != null)
        {
            StopCoroutine(_recoilCoroutine);
            _recoilCoroutine = null;
        }

        if (_zoomCoroutine != null)
        {
            StopCoroutine(_zoomCoroutine);
            _zoomCoroutine = null;
        }

        _recoilOffset = Vector3.zero;
        _zoomOffset = Vector3.zero;
        transform.localPosition = _restLocalPosition;

        if (_camera != null && _baseOrthographicSize > 0f)
        {
            _camera.orthographicSize = _baseOrthographicSize;
        }
    }

    private void ResolveWeapon()
    {
        if (_weapon != null || Player.PlayerInstance == null)
        {
            return;
        }

        _weapon = Player.PlayerInstance.GetComponentInChildren<PlayerWeapon>(true);
    }

    private void SubscribeWeapon()
    {
        if (_isSubscribed || _weapon == null)
        {
            return;
        }

        _weapon.Fired += HandleRecoil;
        _weapon.CriticalHitConfirmed += HandleCriticalHitZoom;
        _isSubscribed = true;
    }

    private void UnsubscribeWeapon()
    {
        if (!_isSubscribed || _weapon == null)
        {
            return;
        }

        _weapon.Fired -= HandleRecoil;
        _weapon.CriticalHitConfirmed -= HandleCriticalHitZoom;
        _isSubscribed = false;
    }

    private void HandleRecoil(bool isCritical)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        StartShake(isCritical ? _magnitude * 1.5f : _magnitude, _duration);
    }

    private void HandleShakeRequested(float magnitude, float seconds)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        StartShake(magnitude, seconds);
    }

    private void StartShake(float magnitude, float seconds)
    {
        if (_recoilCoroutine != null)
        {
            StopCoroutine(_recoilCoroutine);
        }

        _recoilCoroutine = StartCoroutine(RecoilRoutine(magnitude, seconds));
    }

    private void HandleCriticalHitZoom(Vector2 fireDirection)
    {
        if (!isActiveAndEnabled || _camera == null || !_camera.orthographic || _criticalHitZoomMultiplier <= 1f)
        {
            return;
        }

        Vector3 localFireDirection = transform.InverseTransformDirection(
            new Vector3(fireDirection.x, fireDirection.y, 0f));
        if (localFireDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            localFireDirection = Vector3.right;
        }
        else
        {
            localFireDirection.Normalize();
        }

        if (_zoomCoroutine != null)
        {
            StopCoroutine(_zoomCoroutine);
        }
        else
        {
            _baseOrthographicSize = _camera.orthographicSize;
        }

        _zoomCoroutine = StartCoroutine(CriticalHitZoomRoutine(localFireDirection));
    }

    private IEnumerator RecoilRoutine(float magnitude, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float progress = elapsed / duration;
            float decay = 1f - progress;
            Vector2 shake = Random.insideUnitCircle * magnitude * decay;

            _recoilOffset = shake;
            ApplyLocalOffset();

            elapsed += Time.deltaTime;
            yield return null;
        }

        _recoilOffset = Vector3.zero;
        ApplyLocalOffset();
        _recoilCoroutine = null;
    }

    private IEnumerator CriticalHitZoomRoutine(Vector3 localFireDirection)
    {
        float targetOrthographicSize = _baseOrthographicSize / _criticalHitZoomMultiplier;
        float currentOrthographicSize = _camera.orthographicSize;
        Vector3 currentZoomOffset = _zoomOffset;
        Vector3 targetZoomOffset = localFireDirection * _criticalHitZoomOffset;
        float elapsed = 0f;

        while (elapsed < _criticalHitZoomInDuration)
        {
            float progress = elapsed / _criticalHitZoomInDuration;
            _camera.orthographicSize = Mathf.Lerp(
                currentOrthographicSize,
                targetOrthographicSize,
                Mathf.SmoothStep(0f, 1f, progress));
            _zoomOffset = Vector3.Lerp(
                currentZoomOffset,
                targetZoomOffset,
                Mathf.SmoothStep(0f, 1f, progress));
            ApplyLocalOffset();

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        _camera.orthographicSize = targetOrthographicSize;
        _zoomOffset = targetZoomOffset;
        ApplyLocalOffset();
        elapsed = 0f;

        while (elapsed < _criticalHitZoomOutDuration)
        {
            float progress = elapsed / _criticalHitZoomOutDuration;
            _camera.orthographicSize = Mathf.Lerp(
                targetOrthographicSize,
                _baseOrthographicSize,
                Mathf.SmoothStep(0f, 1f, progress));
            _zoomOffset = Vector3.Lerp(
                targetZoomOffset,
                Vector3.zero,
                Mathf.SmoothStep(0f, 1f, progress));
            ApplyLocalOffset();

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        _camera.orthographicSize = _baseOrthographicSize;
        _zoomOffset = Vector3.zero;
        ApplyLocalOffset();
        _zoomCoroutine = null;
    }

    private void ApplyLocalOffset()
    {
        transform.localPosition = _restLocalPosition + _recoilOffset + _zoomOffset;
    }

    private void OnValidate()
    {
        _duration = Mathf.Max(0f, _duration);
        _magnitude = Mathf.Max(0f, _magnitude);
        _criticalHitZoomMultiplier = Mathf.Max(1f, _criticalHitZoomMultiplier);
        _criticalHitZoomInDuration = Mathf.Max(0f, _criticalHitZoomInDuration);
        _criticalHitZoomOutDuration = Mathf.Max(0f, _criticalHitZoomOutDuration);
        _criticalHitZoomOffset = Mathf.Max(0f, _criticalHitZoomOffset);
    }
}
