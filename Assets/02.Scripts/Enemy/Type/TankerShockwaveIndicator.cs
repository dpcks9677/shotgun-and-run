using ShotgunAndRun.Combat;
using UnityEngine;

public sealed class TankerShockwaveIndicator : MonoBehaviour
{
    private const float WarningLineThicknessPixels = 1.5f;
    private static readonly Vector2[] Directions = { Vector2.right, Vector2.left, Vector2.up, Vector2.down };
    private static Sprite _whiteSprite;

    private readonly Vector2[] _outlinePoints = new Vector2[12];
    private readonly SpriteRenderer[] _fills = new SpriteRenderer[5];
    private PixelLineRenderer _outline;
    private bool _visible;

    public void Initialize(SpriteRenderer bodyRenderer, float range, float width)
    {
        if (_whiteSprite == null)
            _whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f, 1f);

        _outline = CreateLine("CrossOutline", bodyRenderer, new Color(0.75f, 0f, 0f));
        float halfWidth = width * 0.5f;
        _outlinePoints[0] = new Vector2(-halfWidth, -range);
        _outlinePoints[1] = new Vector2(halfWidth, -range);
        _outlinePoints[2] = new Vector2(halfWidth, -halfWidth);
        _outlinePoints[3] = new Vector2(range, -halfWidth);
        _outlinePoints[4] = new Vector2(range, halfWidth);
        _outlinePoints[5] = new Vector2(halfWidth, halfWidth);
        _outlinePoints[6] = new Vector2(halfWidth, range);
        _outlinePoints[7] = new Vector2(-halfWidth, range);
        _outlinePoints[8] = new Vector2(-halfWidth, halfWidth);
        _outlinePoints[9] = new Vector2(-range, halfWidth);
        _outlinePoints[10] = new Vector2(-range, -halfWidth);
        _outlinePoints[11] = new Vector2(-halfWidth, -halfWidth);
        _outline.Draw(_outlinePoints, true, WarningLineThicknessPixels);

        for (int i = 0; i < _fills.Length; i++)
        {
            GameObject fillObject = new GameObject(i == 0 ? "CrossCenter" : "CrossArm", typeof(SpriteRenderer));
            fillObject.transform.SetParent(transform, false);
            _fills[i] = fillObject.GetComponent<SpriteRenderer>();
            _fills[i].sprite = _whiteSprite;
            _fills[i].color = new Color(1f, 0f, 0f, 0.5f);
            _fills[i].sortingLayerID = bodyRenderer.sortingLayerID;
            _fills[i].sortingOrder = bodyRenderer.sortingOrder - 2;
        }
        _fills[0].transform.localScale = new Vector3(width, width, 1f);

        for (int directionIndex = 0; directionIndex < Directions.Length; directionIndex++)
        {
            Vector2 direction = Directions[directionIndex];
            Vector2 side = new Vector2(-direction.y, direction.x) * (width * 0.2f);
            for (int arrowIndex = 0; arrowIndex < 2; arrowIndex++)
            {
                PixelLineRenderer arrow = CreateLine("DirectionArrow", bodyRenderer, Color.red);
                Vector2 tip = direction * (range * (0.65f + arrowIndex * 0.1f));
                Vector2 back = tip - direction * (width * 0.25f);
                arrow.Draw(new[] { back + side, tip, back - side }, false,
                    WarningLineThicknessPixels);
            }
        }

        Hide();
    }

    public void Show(Vector2 center, float range, float width, float progress)
    {
        transform.position = center;
        if (!_visible)
        {
            _outline.GetComponent<SpriteRenderer>().enabled = true;
            foreach (PixelLineRenderer arrow in GetComponentsInChildren<PixelLineRenderer>())
                arrow.GetComponent<SpriteRenderer>().enabled = true;
            _visible = true;
        }

        _fills[0].enabled = true;
        float armLength = Mathf.Max(0f, (range - width * 0.5f) * Mathf.Clamp01(progress));
        for (int i = 0; i < Directions.Length; i++)
        {
            Vector2 direction = Directions[i];
            Transform arm = _fills[i + 1].transform;
            arm.localPosition = direction * (width * 0.5f + armLength * 0.5f);
            arm.localScale = new Vector3(direction.x != 0f ? armLength : width,
                direction.y != 0f ? armLength : width, 1f);
            _fills[i + 1].enabled = armLength > 0f;
        }
    }

    public void Hide()
    {
        foreach (SpriteRenderer fill in _fills) fill.enabled = false;
        foreach (PixelLineRenderer line in GetComponentsInChildren<PixelLineRenderer>()) line.Hide();
        _visible = false;
    }

    private PixelLineRenderer CreateLine(string name, SpriteRenderer bodyRenderer, Color color)
    {
        GameObject lineObject = new GameObject(name);
        lineObject.transform.SetParent(transform, false);
        SpriteRenderer renderer = lineObject.AddComponent<SpriteRenderer>();
        renderer.sortingLayerID = bodyRenderer.sortingLayerID;
        renderer.sortingOrder = bodyRenderer.sortingOrder - 1;
        renderer.color = color;
        return lineObject.AddComponent<PixelLineRenderer>();
    }
}
