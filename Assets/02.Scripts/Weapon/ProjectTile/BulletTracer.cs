using System;
using System.Collections.Generic;
using ShotgunAndRun.Combat;
using UnityEngine;

namespace ShotgunAndRun.Weapon
{
    // 총구에서 명중 지점까지 도트 선 한 줄을 그리고, 섬광 프레임에 맞춰 단계마다 대시로 부서지며 사라지는 연출 (프로토타입)
    // 선 픽셀 i(총구 0 → 끝 n-1)는 u = i / n 이 cut 이상이고 hash(i / 대시 길이) < keep 일 때만 켠다.
    // 어두운 연기 대시는 별도 렌더러(_smoke)에 선 옆으로 찍는다. 단계가 바뀔 때만 다시 그린다.
    // 규칙은 Docs/tools/spritegen/vfx/magic_vfx.py 와 같다.
    public sealed class BulletTracer : MonoBehaviour
    {
        [Serializable]
        private struct LineStage
        {
            public Color Color;
            public Color CriticalColor;
            public int DashLength;
            public float Keep;
            public float Cut;

            public LineStage(Color32 color, Color32 criticalColor, int dashLength, float keep, float cut)
            {
                Color = color;
                CriticalColor = criticalColor;
                DashLength = dashLength;
                Keep = keep;
                Cut = cut;
            }
        }

        [Serializable]
        private struct SmokeStage
        {
            public bool Enabled;
            public Vector2 Range;
            public int DashLength;
            public float Keep;
            public int Rise;

            public SmokeStage(bool enabled, float from, float to, int dashLength, float keep, int rise)
            {
                Enabled = enabled;
                Range = new Vector2(from, to);
                DashLength = dashLength;
                Keep = keep;
                Rise = rise;
            }
        }

        private static readonly Color32 White = new(255, 255, 255, 255);
        private static readonly Color32 Yellow = new(255, 195, 25, 255);
        private static readonly Color32 Orange = new(229, 138, 0, 255);
        private static readonly Color32 Red = new(184, 66, 6, 255);
        private static readonly Color32 CriticalOrange = new(255, 140, 10, 255);

        private ProjectTilePool _pool;

        [SerializeField] private PixelLineRenderer _line;
        [SerializeField] private PixelLineRenderer _smoke;

        [Tooltip("초당 단계 수. 섬광 프레임 재생 속도와 맞춘다.")]
        [SerializeField] private float _framesPerSecond = 20f;

        [Tooltip("단계마다 선 색 · 대시 길이 · 남길 확률 · 총구 쪽 잘림. 단계 수 = 선이 지속되는 프레임 수")]
        [SerializeField]
        private LineStage[] _lineStages =
        {
            new(White, Yellow, 1, 1f, 0f),
            new(Yellow, CriticalOrange, 4, 0.75f, 0.15f),
            new(Yellow, CriticalOrange, 3, 0.55f, 0.28f),
            new(Orange, Orange, 2, 0.35f, 0.4f),
            new(Orange, Orange, 1, 0.18f, 0.45f),
            new(Red, Red, 1, 0.07f, 0.5f),
        };

        [Tooltip("크리티컬일 때 앞에서부터 이 단계 수만큼 선을 2px 두께로 그린다.")]
        [SerializeField] private int _criticalThickStages = 2;

        [Tooltip("연기 대시 단계. 선 단계와 같은 순서이며 Rise는 위로 뜬 픽셀 수")]
        [SerializeField]
        private SmokeStage[] _smokeStages =
        {
            new(false, 0f, 0f, 1, 0f, 0),
            new(true, 0.05f, 0.45f, 3, 0.55f, 0),
            new(true, 0.05f, 0.5f, 3, 0.5f, 1),
            new(true, 0.05f, 0.6f, 3, 0.4f, 1),
            new(true, 0.08f, 0.6f, 2, 0.3f, 2),
            new(true, 0.1f, 0.6f, 2, 0.2f, 3),
        };

        private readonly Vector2[] _linePoints = new Vector2[2];
        private SpriteRenderer _lineSprite;
        private IReadOnlyList<Vector2Int> _path;
        private bool _isCritical;
        private bool _isHorizontal;
        private int _seed;
        private int _stage;
        private float _age;

        private void Awake()
        {
            _lineSprite = _line.GetComponent<SpriteRenderer>();
        }

        public void Play(Vector2 start, Vector2 end, PelletImpactKind impactKind, bool isCritical)
        {
            transform.position = start;
            _isCritical = isCritical;
            _seed = UnityEngine.Random.Range(0, 100000);
            _age = 0f;

            // 두꺼운 선이 1px 더, 연기가 위로 3px · 옆으로 2px까지 번지므로 여유를 둔다.
            _linePoints[1] = end - start;
            _path = _line.SetPath(_linePoints, false, 1);
            _smoke.SetPath(_linePoints, false, 5);
            Vector2Int last = _path[^1];
            _isHorizontal = Mathf.Abs(last.x) >= Mathf.Abs(last.y);

            DrawStage(0);

            if (impactKind != PelletImpactKind.None)
            {
                _pool.SpawnImpact(impactKind, end);
            }
        }

        private void Update()
        {
            _age += Time.deltaTime;
            int stage = Mathf.FloorToInt(_age * _framesPerSecond);

            if (stage >= _lineStages.Length)
            {
                _pool.Release(this);
                return;
            }

            if (stage != _stage)
            {
                DrawStage(stage);
            }
        }

        private void DrawStage(int stage)
        {
            _stage = stage;
            LineStage line = _lineStages[stage];
            bool thick = _isCritical && stage < _criticalThickStages;
            int n = _path.Count;

            _lineSprite.color = _isCritical ? line.CriticalColor : line.Color;
            _line.Clear();
            for (int i = 0; i < n; i++)
            {
                if (i / (float)n < line.Cut || Hash01(i / line.DashLength, _seed * 8 + stage) >= line.Keep)
                {
                    continue;
                }

                _line.Plot(_path[i]);
                if (thick)
                {
                    _line.Plot(_path[i] + (_isHorizontal ? Vector2Int.up : Vector2Int.right));
                }
            }

            _line.Apply();
            DrawSmoke(_smokeStages[stage], stage);
        }

        // 대시마다 해시로 옆쪽(±)과 거리(1 · 2px)를 정한다.
        private void DrawSmoke(SmokeStage smoke, int stage)
        {
            if (!smoke.Enabled)
            {
                _smoke.Hide();
                return;
            }

            int n = _path.Count;
            _smoke.Clear();
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                int dash = i / smoke.DashLength;
                if (u < smoke.Range.x || u >= smoke.Range.y || Hash01(dash, _seed * 8 + stage + 100) >= smoke.Keep)
                {
                    continue;
                }

                int side = (Hash01(dash, _seed + 200) < 0.5f ? 2 : 1) * (Hash01(dash, _seed + 300) < 0.5f ? 1 : -1);
                Vector2Int pixel = _path[i];
                pixel += _isHorizontal ? new Vector2Int(0, side + smoke.Rise) : new Vector2Int(side, smoke.Rise);
                _smoke.Plot(pixel);
            }

            _smoke.Apply();
        }

        private static float Hash01(int a, int b)
        {
            return (unchecked((uint)(a * 73856093) ^ (uint)(b * 19349663)) % 1024) / 1024f;
        }

        public void Bind(ProjectTilePool pool)
        {
            _pool = pool;
        }

        public void ResetState()
        {
            _age = 0f;
            _stage = 0;

            _line.Hide();
            _smoke.Hide();
        }
    }
}
