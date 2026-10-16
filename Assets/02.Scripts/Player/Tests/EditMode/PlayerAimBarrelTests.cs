using NUnit.Framework;
using UnityEngine;

namespace ShotgunAndRun.Player.Tests
{
    public sealed class PlayerAimBarrelTests
    {
        // 총열 직선(총구 위치 + t·방향)이 목표에서 떨어진 수직 거리
        private static float DistanceFromBarrelLine(
            Vector2 pivot,
            Vector2 target,
            float sideOffset,
            Vector2 direction)
        {
            Vector2 normal = new(-direction.y, direction.x);
            Vector2 muzzleBase = pivot + normal * sideOffset;
            Vector2 toTarget = target - muzzleBase;
            return Mathf.Abs(toTarget.x * normal.x + toTarget.y * normal.y);
        }

        // 3 · 6 · 9 · 12시와 대각선 방향 (도)
        [TestCase(0f)]
        [TestCase(45f)]
        [TestCase(90f)]
        [TestCase(135f)]
        [TestCase(180f)]
        [TestCase(-135f)]
        [TestCase(-90f)]
        [TestCase(-45f)]
        public void BarrelLine_PassesThroughCursor_ForBothFlips(float cursorAngle)
        {
            Vector2 pivot = new(1.5f, -0.7f);

            foreach (float offset in new[] { 0.177f, -0.177f })
            {
                foreach (float distance in new[] { 1f, 3f, 8f })
                {
                    Vector2 target = pivot + new Vector2(
                        Mathf.Cos(cursorAngle * Mathf.Deg2Rad),
                        Mathf.Sin(cursorAngle * Mathf.Deg2Rad)) * distance;

                    Vector2 direction = PlayerAim.SolveBarrelDirection(pivot, target, offset, Vector2.right);

                    Assert.That(direction.magnitude, Is.EqualTo(1f).Within(0.0001f));
                    Assert.That(
                        DistanceFromBarrelLine(pivot, target, offset, direction),
                        Is.EqualTo(0f).Within(0.001f),
                        $"각도 {cursorAngle}, 오프셋 {offset}, 거리 {distance}");
                }
            }
        }

        [Test]
        public void ZeroOffset_PointsStraightAtCursor()
        {
            Vector2 direction = PlayerAim.SolveBarrelDirection(
                Vector2.zero,
                new Vector2(0f, 5f),
                0f,
                Vector2.right);

            Assert.That(direction.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(direction.y, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void CursorCloserThanOffset_ReturnsFallback()
        {
            Vector2 fallback = Vector2.up;

            Vector2 direction = PlayerAim.SolveBarrelDirection(
                Vector2.zero,
                new Vector2(0.1f, 0f),
                0.177f,
                fallback);

            Assert.That(direction, Is.EqualTo(fallback));
        }
    }
}
