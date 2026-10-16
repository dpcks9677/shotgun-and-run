using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShotgunAndRun.Combat
{
    // 점들을 기본 1픽셀 두께의 도트 선으로 이어 텍스처에 그립니다.
    // 점은 이 오브젝트가 처음 놓인 위치(부모 기준) 기준의 오프셋이며, 그린 영역에 맞춰 오브젝트를 옮깁니다.
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PixelLineRenderer : MonoBehaviour
    {
        private const float PixelsPerUnit = 16f;
        private const int TextureSizeStep = 32;

        private readonly List<Vector2Int> _path = new List<Vector2Int>();
        private SpriteRenderer _renderer;
        private Texture2D _texture;
        private Color32[] _pixels;
        private Vector3 _anchorLocalPosition;
        private Vector2Int _min;
        private int _width;
        private int _pixelScale = 1;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _renderer.enabled = false;
            _anchorLocalPosition = transform.localPosition;
        }

        private void OnDestroy()
        {
            if (_texture != null)
            {
                Destroy(_renderer.sprite);
                Destroy(_texture);
            }
        }

        public void Draw(IReadOnlyList<Vector2> offsets, bool loop, float thicknessPixels = 1f)
        {
            DrawPath(offsets, loop, thicknessPixels, false, Vector2.zero, Vector2.zero, 1f);
        }

        public void DrawGradient(IReadOnlyList<Vector2> offsets, bool loop, float thicknessPixels,
            Vector2 gradientStart, Vector2 gradientEnd, float startAlpha)
        {
            DrawPath(offsets, loop, thicknessPixels, true, gradientStart, gradientEnd, startAlpha);
        }

        private void DrawPath(IReadOnlyList<Vector2> offsets, bool loop, float thicknessPixels,
            bool useGradient, Vector2 gradientStart, Vector2 gradientEnd, float startAlpha)
        {
            int pixelScale = thicknessPixels == Mathf.Floor(thicknessPixels) ? 1 : 2;
            if (_pixelScale != pixelScale && _texture != null)
            {
                Destroy(_renderer.sprite);
                Destroy(_texture);
                _texture = null;
            }
            _pixelScale = pixelScale;
            int strokePixels = Mathf.RoundToInt(thicknessPixels * pixelScale);
            SetPath(offsets, loop, strokePixels / 2);
            Clear();
            Vector2 gradientStartPixel = ToPixel(gradientStart);
            Vector2 gradientDirection = (Vector2)ToPixel(gradientEnd) - gradientStartPixel;
            float gradientLengthSquared = gradientDirection.sqrMagnitude;

            // 앞뒤 픽셀이 대각선으로 맞닿으면 사이의 ㄱ자 모서리 픽셀을 건너뛰어 두께를 1픽셀로 고릅니다.
            Vector2Int previous = _path[0];
            PlotStroke(previous, strokePixels, GetAlpha(previous));
            for (int i = 1; i < _path.Count; i++)
            {
                if (i + 1 < _path.Count && IsDiagonal(previous, _path[i + 1]))
                {
                    continue;
                }

                previous = _path[i];
                PlotStroke(previous, strokePixels, GetAlpha(previous));
            }

            Apply();

            byte GetAlpha(Vector2Int pixel)
            {
                if (!useGradient || gradientLengthSquared <= 0f) return 255;
                float position = Vector2.Dot((Vector2)pixel - gradientStartPixel,
                    gradientDirection) / gradientLengthSquared;
                return (byte)Mathf.RoundToInt(255f * Mathf.Lerp(startAlpha, 1f, Mathf.Clamp01(position)));
            }
        }

        // 경로 픽셀만 계산해 돌려줍니다. 픽셀은 호출하는 쪽이 Clear → Plot → Apply로 골라 찍습니다.
        // padding은 경로 밖으로 찍을 여유 픽셀 수입니다.
        public IReadOnlyList<Vector2Int> SetPath(IReadOnlyList<Vector2> offsets, bool loop, int padding)
        {
            Vector2Int min = ToPixel(offsets[0]);
            Vector2Int max = min;
            for (int i = 1; i < offsets.Count; i++)
            {
                Vector2Int pixel = ToPixel(offsets[i]);
                min = Vector2Int.Min(min, pixel);
                max = Vector2Int.Max(max, pixel);
            }

            min -= Vector2Int.one * padding;
            max += Vector2Int.one * padding;
            EnsureTexture(max.x - min.x + 1, max.y - min.y + 1);
            _min = min;

            _path.Clear();
            _path.Add(ToPixel(offsets[0]));
            for (int i = 1; i < offsets.Count; i++)
            {
                AppendLine(ToPixel(offsets[i]));
            }

            if (loop)
            {
                AppendLine(ToPixel(offsets[0]));
            }

            return _path;
        }

        public void Clear()
        {
            Array.Clear(_pixels, 0, _pixels.Length);
        }

        public void Plot(Vector2Int pixel)
        {
            _pixels[(pixel.y - _min.y) * _width + pixel.x - _min.x] = new Color32(255, 255, 255, 255);
        }

        private void PlotStroke(Vector2Int pixel, int thicknessPixels, byte alpha)
        {
            int start = -thicknessPixels / 2;
            for (int y = 0; y < thicknessPixels; y++)
            {
                for (int x = 0; x < thicknessPixels; x++)
                    _pixels[(pixel.y + start + y - _min.y) * _width +
                            pixel.x + start + x - _min.x] = new Color32(255, 255, 255, alpha);
            }
        }

        public void Apply()
        {
            _texture.SetPixels32(_pixels);
            _texture.Apply(false);

            // 픽셀 중심이 점 위치에 오도록 반 픽셀 당깁니다.
            transform.localPosition = _anchorLocalPosition +
                (Vector3)((Vector2)_min - Vector2.one * 0.5f) / (PixelsPerUnit * _pixelScale);
            _renderer.enabled = true;
        }

        public void Hide()
        {
            _renderer.enabled = false;
        }

        private Vector2Int ToPixel(Vector2 offset)
        {
            return Vector2Int.RoundToInt(offset * PixelsPerUnit * _pixelScale);
        }

        // 텍스처는 커지기만 하므로 조준이 바뀌어도 스프라이트를 매번 새로 만들지 않습니다.
        private void EnsureTexture(int width, int height)
        {
            if (_texture != null && _texture.width >= width && _texture.height >= height)
            {
                return;
            }

            width = Mathf.CeilToInt(Mathf.Max(width, _texture != null ? _texture.width : 0) / (float)TextureSizeStep) * TextureSizeStep;
            height = Mathf.CeilToInt(Mathf.Max(height, _texture != null ? _texture.height : 0) / (float)TextureSizeStep) * TextureSizeStep;

            if (_texture != null)
            {
                Destroy(_renderer.sprite);
                Destroy(_texture);
            }

            _texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            _pixels = new Color32[width * height];
            _width = width;
            _renderer.sprite = Sprite.Create(_texture, new Rect(0f, 0f, width, height),
                Vector2.zero, PixelsPerUnit * _pixelScale);
        }

        private static bool IsDiagonal(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) == 1 && Mathf.Abs(a.y - b.y) == 1;
        }

        // 경로 끝에서 to까지 Bresenham 직선으로 이어 붙입니다.
        private void AppendLine(Vector2Int to)
        {
            Vector2Int from = _path[_path.Count - 1];
            int dx = Mathf.Abs(to.x - from.x);
            int dy = -Mathf.Abs(to.y - from.y);
            int stepX = from.x < to.x ? 1 : -1;
            int stepY = from.y < to.y ? 1 : -1;
            int error = dx + dy;

            while (from != to)
            {
                int doubledError = error * 2;
                if (doubledError >= dy)
                {
                    error += dy;
                    from.x += stepX;
                }

                if (doubledError <= dx)
                {
                    error += dx;
                    from.y += stepY;
                }

                _path.Add(from);
            }
        }
    }
}
