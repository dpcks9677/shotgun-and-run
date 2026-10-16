using ShotgunAndRun.Combat;
using UnityEngine;

public sealed class TankerChargeIndicator : MonoBehaviour
{
    private const float WarningLineThicknessPixels = 1.5f;
    private const float WarningStartAlpha = 0.7f;
    private static Sprite _whiteSprite;
    private SpriteRenderer _fill;
    private PixelLineRenderer _border;
    private readonly Vector2[] _borderCorners = new Vector2[4];
    private bool _borderVisible;

    public void Initialize(SpriteRenderer bodyRenderer)
    {
        if (_whiteSprite == null)
            _whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f), 1f);
        GameObject fillObject = new GameObject("ChargeFill", typeof(SpriteRenderer));
        fillObject.transform.SetParent(transform, false);
        _fill = fillObject.GetComponent<SpriteRenderer>();
        _fill.sprite = _whiteSprite;
        _fill.sortingLayerID = bodyRenderer.sortingLayerID;
        _fill.sortingOrder = bodyRenderer.sortingOrder - 2;

        GameObject borderObject = new GameObject("ChargeBorder");
        borderObject.transform.SetParent(transform, false);
        SpriteRenderer borderRenderer = borderObject.AddComponent<SpriteRenderer>();
        borderRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
        borderRenderer.sortingOrder = bodyRenderer.sortingOrder - 1;
        borderRenderer.color = new Color(0.75f, 0f, 0f, 1f);
        _border = borderObject.AddComponent<PixelLineRenderer>();
        Hide();
    }

    public void Show(Vector2 start, Vector2 direction, float length, float width, float progress)
    {
        if (!_borderVisible)
        {
            DrawBorder(start, direction, 0f, length, width);
        }

        float fillLength = length * Mathf.Clamp01(progress);
        SetFill(start + direction * (fillLength * 0.5f), direction, fillLength, width);
    }

    public void ShowRemaining(Vector2 start, Vector2 direction, float length, float width,
        float travelled)
    {
        travelled = Mathf.Clamp(travelled, 0f, length);
        DrawBorder(start, direction, travelled, length, width);
        float remainingLength = length - travelled;
        SetFill(start + direction * (travelled + remainingLength * 0.5f),
            direction, remainingLength, width);
    }

    private void DrawBorder(Vector2 start, Vector2 direction, float from, float to, float width)
    {
        transform.position = start;
        Vector2 side = new Vector2(-direction.y, direction.x) * (width * 0.5f);
        Vector2 near = direction * from;
        Vector2 far = direction * to;
        _borderCorners[0] = near - side;
        _borderCorners[1] = far - side;
        _borderCorners[2] = far + side;
        _borderCorners[3] = near + side;
        _border.DrawGradient(_borderCorners, false, WarningLineThicknessPixels,
            near, far, WarningStartAlpha);
        _borderVisible = true;
    }

    private void SetFill(Vector2 center, Vector2 direction, float fillLength, float width)
    {
        _fill.transform.position = center;
        _fill.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        _fill.transform.localScale = new Vector3(fillLength, width, 1f);
        _fill.color = new Color(1f, 0f, 0f, 0.5f);
        _fill.enabled = fillLength > 0f;
    }

    public void Hide()
    {
        _fill.enabled = false;
        _border.Hide();
        _borderVisible = false;
    }
}
