using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyAwarenessBubble : MonoBehaviour
{
    [SerializeField] private Sprite _alertSprite;
    [SerializeField] private Sprite _returnSprite;
    [SerializeField] private float _scale = 1.35f;

    private const float AlertDuration = 0.7f;
    private const float OtherDuration = 0.8f;
    private const float PopDuration = 0.12f;

    private Enemy _enemy;
    private SpriteRenderer _bubbleRenderer;
    private float _shownAt;
    private float _duration;

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
    }

    public void ShowAlert() => Show(_alertSprite, AlertDuration);
    public void ShowReturn() => Show(_returnSprite, OtherDuration);

    private void Show(Sprite sprite, float duration)
    {
        if (sprite == null) return;

        if (_bubbleRenderer == null)
        {
            var bubble = new GameObject("AwarenessBubble");
            bubble.transform.SetParent(transform, false);
            _bubbleRenderer = bubble.AddComponent<SpriteRenderer>();
            _bubbleRenderer.sortingLayerName = "UI";
            _bubbleRenderer.sortingOrder = 500;
        }

        _bubbleRenderer.sprite = sprite;
        _bubbleRenderer.gameObject.SetActive(true);
        _shownAt = Time.time;
        _duration = duration;
        UpdateBubble();
    }

    private void LateUpdate()
    {
        if (_bubbleRenderer == null || !_bubbleRenderer.gameObject.activeSelf) return;
        UpdateBubble();
    }

    private void UpdateBubble()
    {
        float elapsed = Time.time - _shownAt;
        if (elapsed >= _duration)
        {
            Hide();
            return;
        }

        float popIn = Mathf.Clamp01(elapsed / PopDuration);
        float popOut = Mathf.Clamp01((_duration - elapsed) / PopDuration);
        _bubbleRenderer.transform.localScale = Vector3.one * (_scale * Mathf.Min(popIn, popOut));

        Bounds anchorBounds = _enemy.IndicatorAnchorBounds;
        _bubbleRenderer.transform.position = new Vector3(
            anchorBounds.center.x,
            anchorBounds.max.y,
            transform.position.z);
    }

    public void Hide()
    {
        if (_bubbleRenderer != null) _bubbleRenderer.gameObject.SetActive(false);
    }

    private void OnDisable() => Hide();
}
