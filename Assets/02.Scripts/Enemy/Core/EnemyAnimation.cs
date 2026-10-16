using System.Collections;
using ShotgunAndRun.Combat;
using UnityEngine;

public class EnemyAnimation : MonoBehaviour
{
    private const string SolidOutlineShaderResourcePath = "Enemy/SpriteSolidColor";

    private static readonly Vector2[] OutlineDirections =
    {
        Vector2.up,
        Vector2.down,
        Vector2.left,
        Vector2.right,
        new Vector2(1f, 1f),
        new Vector2(1f, -1f),
        new Vector2(-1f, 1f),
        new Vector2(-1f, -1f)
    };

    // Components Field
    [Header("Components")]
    [SerializeField] private Animator _animator;
    [SerializeField] private SpriteRenderer _spriteRenderer;

    // Animator Parameters: Bool & Trigger Hash Field
    [Header("Animator Parameters")]
    [SerializeField] private string[] _attackTriggers;
    [SerializeField] private string _idleStateName = "Idle";
    [SerializeField] private string _hitStateName = "Hit";
    private static readonly int MovingParameter = Animator.StringToHash("isMoving");
    private static readonly int HitTrigger = Animator.StringToHash("hit");
    private static readonly int DeathTrigger = Animator.StringToHash("death");

    [Header("Facing")]
    [Tooltip("이 값보다 작은 수평 이동은 현재 좌우 방향을 유지합니다.")]
    [SerializeField, Range(0f, 1f)] private float _horizontalFlipThreshold = 0.15f;
    [Tooltip("반대 방향이 이 시간 이상 유지되어야 스프라이트를 뒤집습니다.")]
    [SerializeField, Min(0f)] private float _flipConfirmationDuration = 0.12f;

    [Header("Hit Feedback")]
    [SerializeField] private Color _hitOutlineColor = Color.red;
    [SerializeField] private float _hitOutlinePixels = 0.8f;
    [SerializeField, Min(0.01f)] private float _hitFeedbackDuration = 0.18f;
    [SerializeField, Range(0f, 1f)] private float _hitAlphaMultiplier = 0.55f;
    [SerializeField, Min(1)] private int _hitPulseCount = 2;
    
    // Field
    private bool _initialized;
    private SpriteRenderer[] _outlineRenderers;
    private Coroutine _hitFeedbackCoroutine;
    private Color _feedbackBaseColor;
    private bool _hasFeedbackBaseColor;
    private Coroutine _hitAnimationCoroutine;
    private Color _hitAnimationOutlineColor;
    private bool? _pendingFlipX;
    private float _pendingFlipStartedAt;
    private static Material _solidOutlineMaterial;
    private bool _hasDefenceOutline;
    private Color _defenceOutlineColor;
    private float _defenceOutlineStartedTime;
    [SerializeField] private Color _defenceOutlineLightColor = new Color(0.8f, 0.93f, 1f, 1f);
    [SerializeField] private float _defenceOutlinePeriod = 0.8f;

    public void SetDefenceOutline(bool active, Color color)
    {
        if (active && !_hasDefenceOutline) _defenceOutlineStartedTime = Time.time;
        _hasDefenceOutline = active;
        _defenceOutlineColor = color;
        if (!active) DisableOutlineRenderers();
    }

    public void PlayDefence()
    {
        if (_initialized) _animator.Play("Defence", 0, 0f);
    }

    private void LateUpdate()
    {
        if (!_initialized || !_hasDefenceOutline) return;
        EnsureOutlineRenderers();
        float elapsed = Time.time - _defenceOutlineStartedTime;
        float blend = (1f - Mathf.Cos(elapsed * Mathf.PI * 2f / _defenceOutlinePeriod)) * 0.5f;
        Color color = Color.Lerp(_defenceOutlineColor, _defenceOutlineLightColor, blend);
        SyncOutlineRenderers(color, 1f, _spriteRenderer.color.a);
    }

    public void Initialize()
    {
        if (_animator == null || _spriteRenderer == null)
        {
            Debug.Log("[EnemyAnimation.cs : Awake] - Animator or SpriteRender is null");
            return;
        }
        _initialized = true;
        ResetAnimation();
    }

    public void PlayMove(bool isMoving)
    {
        if (!_initialized) return;

        _animator.SetBool(MovingParameter, isMoving);
    }

    public void PlayAttack()
    {
        if (!_initialized) return;

        if (_attackTriggers == null || _attackTriggers.Length == 0)
        {
            return;
        }

        int randomIndex = UnityEngine.Random.Range(0, _attackTriggers.Length);

        _animator.SetTrigger(_attackTriggers[randomIndex]);
    }

    public void CancelAttack()
    {
        if (!_initialized) return;

        if (_attackTriggers != null)
        {
            foreach (string attackTrigger in _attackTriggers)
            {
                if (!string.IsNullOrWhiteSpace(attackTrigger))
                {
                    _animator.ResetTrigger(attackTrigger);
                }
            }
        }

        int idleStateHash = Animator.StringToHash(_idleStateName);

        if (!string.IsNullOrWhiteSpace(_idleStateName) && _animator.HasState(0, idleStateHash))
        {
            _animator.Play(idleStateHash, 0, 0f);
            _animator.Update(0f);
        }
    }

    public void ShowChargeOutline(Color color)
    {
        if (!_initialized) return;

        EnsureOutlineRenderers();
        SyncOutlineRenderers(color, 1f, _spriteRenderer.color.a);
    }

    public void HideChargeOutline()
    {
        DisableOutlineRenderers();
    }

    public void PlayHitFeedback()
    {
        if (!_initialized) return;

        StopHitAnimation();
        StopHitFeedback(true);
        EnsureOutlineRenderers();

        _feedbackBaseColor = _spriteRenderer.color;
        _hasFeedbackBaseColor = true;
        _hitFeedbackCoroutine = StartCoroutine(HitFeedbackRoutine());
    }

    public void PlayHitAnimation(Color hitColor)
    {
        if (!_initialized) return;

        StopHitFeedback(true);
        StopHitAnimation();
        EnsureOutlineRenderers();

        _hitAnimationOutlineColor = hitColor;
        SyncOutlineRenderers(_hitAnimationOutlineColor, 1f, _spriteRenderer.color.a);

        _animator.ResetTrigger(HitTrigger);
        _animator.SetTrigger(HitTrigger);
        _hitAnimationCoroutine = StartCoroutine(HitAnimationRoutine());
    }

    public void PlayDeath()
    {
        if (!_initialized) return;

        _animator.SetTrigger(DeathTrigger);
    }

    public void SetSpriteFlipByDirection(Vector3 direction)
    {
        if (!_initialized || Mathf.Abs(direction.x) < _horizontalFlipThreshold)
        {
            return;
        }

        bool desiredFlipX = direction.x < 0f;

        if (desiredFlipX == _spriteRenderer.flipX)
        {
            ClearPendingFlip();
            return;
        }

        if (!_pendingFlipX.HasValue || _pendingFlipX.Value != desiredFlipX)
        {
            _pendingFlipX = desiredFlipX;
            _pendingFlipStartedAt = Time.unscaledTime;
            return;
        }

        if (Time.unscaledTime - _pendingFlipStartedAt < _flipConfirmationDuration)
        {
            return;
        }

        _spriteRenderer.flipX = desiredFlipX;
        ClearPendingFlip();
    }

    public void SetSpriteFlipImmediately(Vector3 direction)
    {
        if (!_initialized || Mathf.Abs(direction.x) <= Mathf.Epsilon)
        {
            return;
        }

        _spriteRenderer.flipX = direction.x < 0f;
        ClearPendingFlip();
    }

    public void SetSpriteAlpha(float alpha)
    {
        if (!_initialized) return;

        Color color = _spriteRenderer.color;
        color.a = Mathf.Clamp01(alpha);
        _spriteRenderer.color = color;
    }

    public void ResetAnimation()
    {
        if (!_initialized) return;

        ClearPendingFlip();
        StopHitAnimation();
        StopHitFeedback(true);
        _animator.Rebind();
        _animator.Update(0f);

        SetSpriteAlpha(1f);
    }

    public bool GetSpriteFlipX()
    {
        return _spriteRenderer.flipX;
    }

    public Color GetSpriteColor()
    {
        return _spriteRenderer.color;
    }

    public void SetSpriteColor(Color color)
    {
        _spriteRenderer.color = color;
    }

    private IEnumerator HitFeedbackRoutine()
    {
        float elapsedTime = 0f;
        float duration = Mathf.Max(0.01f, _hitFeedbackDuration);
        int pulseCount = Mathf.Max(1, _hitPulseCount);

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsedTime / duration);
            float pulse = 0.5f + 0.5f * Mathf.Cos(ratio * pulseCount * Mathf.PI * 2f);

            Color spriteColor = _feedbackBaseColor;
            spriteColor.a *= Mathf.Lerp(1f, _hitAlphaMultiplier, pulse);
            _spriteRenderer.color = spriteColor;

            SyncOutlineRenderers(_hitOutlineColor, pulse, _feedbackBaseColor.a);
            yield return null;
        }

        _hitFeedbackCoroutine = null;
        RestoreHitFeedbackVisuals(true);
    }

    private IEnumerator HitAnimationRoutine()
    {
        int hitStateHash = Animator.StringToHash(_hitStateName);
        float stateEnterWait = 0f;

        // Trigger가 Animator에 반영될 때까지 기다린다. 실제 색상 유지 시간은 Hit 상태가 결정한다.
        while (!IsAnimatorStateActive(hitStateHash) && stateEnterWait < 0.25f)
        {
            SyncOutlineRenderers(_hitAnimationOutlineColor, 1f, _spriteRenderer.color.a);
            stateEnterWait += Time.unscaledDeltaTime;
            yield return null;
        }

        while (IsAnimatorStateActive(hitStateHash))
        {
            SyncOutlineRenderers(_hitAnimationOutlineColor, 1f, _spriteRenderer.color.a);
            yield return null;
        }

        _hitAnimationCoroutine = null;
        DisableOutlineRenderers();
    }

    private bool IsAnimatorStateActive(int stateHash)
    {
        AnimatorStateInfo currentState = _animator.GetCurrentAnimatorStateInfo(0);

        if (currentState.shortNameHash == stateHash)
        {
            return true;
        }

        if (!_animator.IsInTransition(0))
        {
            return false;
        }

        AnimatorStateInfo nextState = _animator.GetNextAnimatorStateInfo(0);
        return nextState.shortNameHash == stateHash;
    }

    private void EnsureOutlineRenderers()
    {
        if (_outlineRenderers != null && _outlineRenderers.Length == OutlineDirections.Length)
        {
            return;
        }

        _outlineRenderers = new SpriteRenderer[OutlineDirections.Length];
        Material outlineMaterial = GetSolidOutlineMaterial();

        for (int index = 0; index < OutlineDirections.Length; index++)
        {
            GameObject outlineObject = new GameObject($"HitOutline_{index}")
            {
                hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor
            };
            outlineObject.layer = _spriteRenderer.gameObject.layer;

            Transform outlineTransform = outlineObject.transform;
            outlineTransform.SetParent(_spriteRenderer.transform, false);

            SpriteRenderer outlineRenderer = outlineObject.AddComponent<SpriteRenderer>();
            outlineRenderer.sharedMaterial = outlineMaterial;
            outlineRenderer.enabled = false;
            _outlineRenderers[index] = outlineRenderer;
        }
    }

    private void SyncOutlineRenderers(Color outlineColor, float pulse, float sourceAlpha)
    {
        if (_outlineRenderers == null) return;

        outlineColor.a *= sourceAlpha * Mathf.Clamp01(pulse);
        float outlineOffset = GetOutlineOffset();
        Vector3 spriteScale = _spriteRenderer.transform.lossyScale;
        Material outlineMaterial = GetSolidOutlineMaterial();

        for (int index = 0; index < _outlineRenderers.Length; index++)
        {
            SpriteRenderer outlineRenderer = _outlineRenderers[index];

            if (outlineRenderer == null) continue;

            Vector2 direction = OutlineDirections[index];
            outlineRenderer.transform.localPosition = new Vector3(
                direction.x * outlineOffset / Mathf.Abs(spriteScale.x),
                direction.y * outlineOffset / Mathf.Abs(spriteScale.y));
            outlineRenderer.sprite = _spriteRenderer.sprite;
            outlineRenderer.sharedMaterial = outlineMaterial;
            outlineRenderer.color = outlineColor;
            outlineRenderer.flipX = _spriteRenderer.flipX;
            outlineRenderer.flipY = _spriteRenderer.flipY;
            outlineRenderer.drawMode = _spriteRenderer.drawMode;
            outlineRenderer.size = _spriteRenderer.size;
            outlineRenderer.maskInteraction = _spriteRenderer.maskInteraction;
            outlineRenderer.spriteSortPoint = _spriteRenderer.spriteSortPoint;
            outlineRenderer.sortingLayerID = _spriteRenderer.sortingLayerID;
            outlineRenderer.sortingOrder = _spriteRenderer.sortingOrder - 1;
            outlineRenderer.enabled =
                pulse > 0.01f && _spriteRenderer.enabled && _spriteRenderer.sprite != null;
        }
    }

    private float GetOutlineOffset()
    {
        Sprite sprite = _spriteRenderer.sprite;

        if (sprite == null || sprite.pixelsPerUnit <= 0f)
        {
            return 0f;
        }

        return _hitOutlinePixels / sprite.pixelsPerUnit;
    }

    private Material GetSolidOutlineMaterial()
    {
        if (_solidOutlineMaterial != null)
        {
            return _solidOutlineMaterial;
        }

        Shader solidColorShader = Resources.Load<Shader>(SolidOutlineShaderResourcePath);

        if (solidColorShader == null)
        {
            Debug.LogError(
                $"[EnemyAnimation] 외곽선 셰이더를 찾을 수 없습니다: " +
                $"Resources/{SolidOutlineShaderResourcePath}.shader");
            return _spriteRenderer.sharedMaterial;
        }

        _solidOutlineMaterial = new Material(solidColorShader)
        {
            name = "Enemy Hit Solid Outline (Runtime)",
            hideFlags = HideFlags.HideAndDontSave
        };
        _solidOutlineMaterial.SetFloat("_AlphaCutoff", 0.5f);

        return _solidOutlineMaterial;
    }

    private void StopHitFeedback(bool restoreColor)
    {
        if (_hitFeedbackCoroutine != null)
        {
            StopCoroutine(_hitFeedbackCoroutine);
            _hitFeedbackCoroutine = null;
        }

        RestoreHitFeedbackVisuals(restoreColor);
    }

    private void RestoreHitFeedbackVisuals(bool restoreColor)
    {
        if (restoreColor && _hasFeedbackBaseColor && _spriteRenderer != null)
        {
            _spriteRenderer.color = _feedbackBaseColor;
        }

        DisableOutlineRenderers();

        _hasFeedbackBaseColor = false;
    }

    private void StopHitAnimation()
    {
        if (_hitAnimationCoroutine != null)
        {
            StopCoroutine(_hitAnimationCoroutine);
            _hitAnimationCoroutine = null;
        }

        DisableOutlineRenderers();
    }

    private void DisableOutlineRenderers()
    {
        if (_outlineRenderers == null) return;

        foreach (SpriteRenderer outlineRenderer in _outlineRenderers)
        {
            if (outlineRenderer != null)
            {
                outlineRenderer.enabled = false;
            }
        }
    }

    private void OnDisable()
    {
        _hasDefenceOutline = false;
        ClearPendingFlip();
        StopHitAnimation();
        StopHitFeedback(true);
    }

    private void ClearPendingFlip()
    {
        _pendingFlipX = null;
        _pendingFlipStartedAt = 0f;
    }
}
