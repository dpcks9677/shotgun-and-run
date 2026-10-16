using System.Collections.Generic;
using NUnit.Framework;
using ShotgunAndRun.Weapon;
using UnityEngine;

namespace ShotgunAndRun.Player.Tests
{
    public sealed class ShotgunSpreadTests
    {
        private ShotgunDataSO _data;

        [TearDown]
        public void TearDown()
        {
            if (_data != null)
            {
                Object.DestroyImmediate(_data);
            }
        }

        [TestCase(1, 36f)]
        [TestCase(3, 28f)]
        [TestCase(5, 20f)]
        public void SpreadAngle_DecreasesByBarrelLevel(int barrelLevel, float expected)
        {
            _data = ScriptableObject.CreateInstance<ShotgunDataSO>();
            _data.SpreadAngle = 36f;

            ShotgunRuntimeStats stats = new(_data, new WeaponUpgradeState { BarrelLevel = barrelLevel });

            Assert.That(stats.SpreadAngle, Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void PelletAngle_WithoutJitter_PlacesPelletsAtSlotCenters()
        {
            float[] expected = { -14.4f, -7.2f, 0f, 7.2f, 14.4f };

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(
                    ShotgunSpread.GetPelletAngle(i, 5, 18f, 0f),
                    Is.EqualTo(expected[i]).Within(0.001f));
            }
        }

        [Test]
        public void PelletAngle_WithJitter_StaysInsideHalfAngle()
        {
            for (int i = 0; i < 5; i++)
            {
                Assert.That(ShotgunSpread.GetPelletAngle(i, 5, 10f, 2f), Is.InRange(-10f, 10f));
                Assert.That(ShotgunSpread.GetPelletAngle(i, 5, 10f, -2f), Is.InRange(-10f, 10f));
            }

            Assert.That(ShotgunSpread.GetPelletAngle(4, 5, 10f, 2f), Is.EqualTo(10f).Within(0.001f));
            Assert.That(ShotgunSpread.GetPelletAngle(0, 5, 10f, -2f), Is.EqualTo(-10f).Within(0.001f));
        }

        [Test]
        public void PelletAngle_SinglePellet_FiresStraight()
        {
            Assert.That(ShotgunSpread.GetPelletAngle(0, 1, 18f, 0f), Is.EqualTo(0f));
        }
    }

    public sealed class PixelArcTests
    {
        private readonly List<Vector2Int> _pixels = new();
        private readonly List<Vector2Int> _scratch = new();

        [Test]
        public void Arc_AlignedToGrid_IsMirrorSymmetric()
        {
            // 픽셀 (0, 0) 한가운데에서 위쪽(90도)을 조준 → x = 0 열을 기준으로 좌우 대칭
            PixelArc.Build(new Vector2(0.5f, 0.5f), 40f, 90f, 18f, 3, _pixels, _scratch);
            HashSet<Vector2Int> set = new(_pixels);

            foreach (Vector2Int pixel in _pixels)
            {
                Assert.That(set.Contains(new Vector2Int(-pixel.x, pixel.y)), Is.True, $"{pixel}의 대칭 픽셀이 없습니다.");
            }
        }

        [TestCase(0f)]
        [TestCase(30f)]
        [TestCase(45f)]
        [TestCase(137f)]
        [TestCase(-100f)]
        public void Arc_IsOnePixelThick_AndConnected(float aimAngle)
        {
            PixelArc.Build(new Vector2(0.3f, 0.7f), 60f, aimAngle, 18f, 0, _pixels, _scratch);
            HashSet<Vector2Int> set = new(_pixels);

            Assert.That(set.Count, Is.EqualTo(_pixels.Count), "중복 픽셀이 있습니다.");

            // 2x2 칸 안에 픽셀이 3개 이상이면 L자 모서리(굵은 선)입니다.
            foreach (Vector2Int pixel in _pixels)
            {
                int count = 0;
                for (int dx = 0; dx <= 1; dx++)
                {
                    for (int dy = 0; dy <= 1; dy++)
                    {
                        if (set.Contains(pixel + new Vector2Int(dx, dy)))
                        {
                            count++;
                        }
                    }
                }

                Assert.That(count, Is.LessThanOrEqualTo(2), $"{pixel} 부근이 1px보다 굵습니다.");
            }

            Assert.That(CountComponents(set), Is.EqualTo(1), "호가 끊겼습니다.");
        }

        [Test]
        public void Arc_EndsNearHalfAngleDirections()
        {
            Vector2 center = new(0.5f, 0.5f);
            const float radius = 50f;
            const float aimAngle = 20f;
            const float halfAngle = 15f;

            PixelArc.Build(center, radius, aimAngle, halfAngle, 0, _pixels, _scratch);

            foreach (float angle in new[] { aimAngle + halfAngle, aimAngle - halfAngle })
            {
                float radians = angle * Mathf.Deg2Rad;
                Vector2 expected = center + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;

                float nearest = float.MaxValue;
                foreach (Vector2Int pixel in _pixels)
                {
                    nearest = Mathf.Min(nearest, Vector2.Distance(pixel + new Vector2(0.5f, 0.5f), expected));
                }

                Assert.That(nearest, Is.LessThanOrEqualTo(1f));
            }
        }

        [Test]
        public void Arc_DoubleRadius_DoublesLength()
        {
            PixelArc.Build(Vector2.zero, 40f, 0f, 18f, 0, _pixels, _scratch);
            int shortCount = _pixels.Count;

            PixelArc.Build(Vector2.zero, 80f, 0f, 18f, 0, _pixels, _scratch);
            int longCount = _pixels.Count;

            Assert.That((float)longCount / shortCount, Is.InRange(1.8f, 2.2f));
        }

        [Test]
        public void MaxShift_SameShape_IsZero_AndDetectsRadiusAndAngleChange()
        {
            Assert.That(PixelArc.MaxShift(60f, 30f, 60f, 30f, 18f), Is.EqualTo(0f).Within(0.0001f));

            // 반지름 1px 증가 → 가운데 · 양 끝 모두 1px 이동
            Assert.That(PixelArc.MaxShift(60f, 30f, 61f, 30f, 18f), Is.EqualTo(1f).Within(0.001f));

            // 반지름 60px에서 방향 1도 회전 → 약 60 × 0.01745 ≈ 1.05px
            Assert.That(PixelArc.MaxShift(60f, 30f, 60f, 31f, 18f), Is.EqualTo(1.047f).Within(0.01f));

            // 0.5도 회전은 1px 미만 → 다시 그리지 않음
            Assert.That(PixelArc.MaxShift(60f, 30f, 60f, 30.5f, 18f), Is.LessThan(1f));
        }

        [Test]
        public void Ticks_PointTowardCenter()
        {
            PixelArc.Build(new Vector2(0.5f, 0.5f), 40f, 90f, 18f, 0, _pixels, _scratch);
            int arcCount = _pixels.Count;

            PixelArc.Build(new Vector2(0.5f, 0.5f), 40f, 90f, 18f, 3, _pixels, _scratch);

            Assert.That(_pixels.Count - arcCount, Is.InRange(4, 6), "눈금 픽셀 수가 양쪽 합 약 3px × 2가 아닙니다.");

            // 눈금은 호(반지름 40px)에서 중심 쪽으로 약 3px 들어옵니다.
            float nearest = float.MaxValue;
            for (int i = arcCount; i < _pixels.Count; i++)
            {
                float distance = Vector2.Distance(_pixels[i] + new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                Assert.That(distance, Is.LessThan(40.8f), "눈금이 바깥쪽으로 그려졌습니다.");
                nearest = Mathf.Min(nearest, distance);
            }

            Assert.That(nearest, Is.InRange(36f, 38.5f));
        }

        private static int CountComponents(HashSet<Vector2Int> set)
        {
            HashSet<Vector2Int> visited = new();
            Queue<Vector2Int> queue = new();
            int components = 0;

            foreach (Vector2Int start in set)
            {
                if (!visited.Add(start))
                {
                    continue;
                }

                components++;
                queue.Enqueue(start);

                while (queue.Count > 0)
                {
                    Vector2Int current = queue.Dequeue();
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            Vector2Int next = current + new Vector2Int(dx, dy);
                            if (set.Contains(next) && visited.Add(next))
                            {
                                queue.Enqueue(next);
                            }
                        }
                    }
                }
            }

            return components;
        }
    }
}
