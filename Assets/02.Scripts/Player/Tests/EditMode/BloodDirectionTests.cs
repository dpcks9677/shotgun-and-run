using NUnit.Framework;
using ShotgunAndRun.Combat;
using UnityEngine;

namespace ShotgunAndRun.Player.Tests
{
    public sealed class BloodDirectionTests
    {
        // 방향 0 = 동, 반시계 45° 단위 (blood_vfx.py의 시트 행 순서와 같다)
        [TestCase(1f, 0f, 0)]
        [TestCase(1f, 1f, 1)]
        [TestCase(0f, 1f, 2)]
        [TestCase(-1f, 1f, 3)]
        [TestCase(-1f, 0f, 4)]
        [TestCase(-1f, -1f, 5)]
        [TestCase(0f, -1f, 6)]
        [TestCase(1f, -1f, 7)]
        [TestCase(1f, 0.3f, 0)]
        [TestCase(1f, 0.5f, 1)]
        [TestCase(1f, -0.3f, 0)]
        [TestCase(1f, -0.5f, 7)]
        public void DirectionIndex_RoundsToNearestOfEightDirections(float x, float y, int expected)
        {
            Assert.AreEqual(expected, Blood.DirectionIndex(new Vector2(x, y)));
        }
    }
}
