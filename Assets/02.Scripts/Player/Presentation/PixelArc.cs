using System.Collections.Generic;
using UnityEngine;

namespace ShotgunAndRun.Player
{
    // 월드 픽셀 격자 위에 1px 굵기의 호와 양 끝 눈금을 계산합니다. (산탄 범위 인디케이터용)
    // 좌표 단위는 픽셀이며, 픽셀 (x, y)는 [x, x + 1) × [y, y + 1) 영역입니다.
    // 호는 가운데에서 양쪽으로 따로 그리므로, 조준축이 격자와 나란하면 좌우가 정확히 대칭입니다.
    // 계단의 L자 모서리(가로 · 세로로만 붙은 가운데 픽셀)를 빼서 선 굵기를 1px로 유지합니다.
    public static class PixelArc
    {
        // result: 호 가운데 → 한쪽 끝 → 다른 쪽 끝 → 눈금 순서의 픽셀 (중복 없음)
        // scratch: 계산용 임시 버퍼 (GC 없이 재사용)
        public static void Build(
            Vector2 center,
            float radius,
            float aimAngle,
            float halfAngle,
            int tickLength,
            List<Vector2Int> result,
            List<Vector2Int> scratch)
        {
            result.Clear();

            if (radius <= 0f)
            {
                return;
            }

            halfAngle = Mathf.Max(0f, halfAngle);

            SampleHalfArc(center, radius, aimAngle, halfAngle, 1f, scratch);
            RemoveCorners(scratch);
            result.AddRange(scratch);
            Vector2Int endA = result[result.Count - 1];

            if (halfAngle <= 0f)
            {
                AppendTick(center, endA, tickLength, result, scratch);
                return;
            }

            // 가운데 픽셀은 이미 넣었으므로 건너뜁니다.
            int firstHalfCount = result.Count;
            SampleHalfArc(center, radius, aimAngle, halfAngle, -1f, scratch);
            RemoveCorners(scratch);
            for (int i = 1; i < scratch.Count; i++)
            {
                result.Add(scratch[i]);
            }
            Vector2Int endB = result[result.Count - 1];

            // 두 반쪽이 만나는 가운데 픽셀이 L자 모서리가 되면 뺍니다.
            // (조준축이 격자와 나란할 때는 양쪽 이웃이 대각이 될 수 없으므로 대칭이 유지됩니다.)
            if (firstHalfCount >= 2 && result.Count > firstHalfCount &&
                IsCorner(result[1], result[0], result[firstHalfCount]))
            {
                result.RemoveAt(0);
            }

            AppendTick(center, endA, tickLength, result, scratch);
            AppendTick(center, endB, tickLength, result, scratch);
        }

        // 같은 중심을 쓰는 두 호의 가운데 · 양 끝이 가장 많이 움직인 거리 (픽셀)
        // 인디케이터는 이 값이 1px 이상일 때만 다시 그립니다. (반각은 같다고 봄)
        public static float MaxShift(
            float radiusA,
            float aimAngleA,
            float radiusB,
            float aimAngleB,
            float halfAngle)
        {
            float maxShift = 0f;

            for (int side = -1; side <= 1; side++)
            {
                Vector2 pointA = PointOnArc(radiusA, aimAngleA + side * halfAngle);
                Vector2 pointB = PointOnArc(radiusB, aimAngleB + side * halfAngle);
                maxShift = Mathf.Max(maxShift, Vector2.Distance(pointA, pointB));
            }

            return maxShift;
        }

        private static Vector2 PointOnArc(float radius, float angle)
        {
            float radians = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
        }

        // 호 가운데(조준 방향)에서 한쪽 끝까지 약 0.5px 간격으로 샘플링합니다.
        private static void SampleHalfArc(
            Vector2 center,
            float radius,
            float aimAngle,
            float halfAngle,
            float side,
            List<Vector2Int> output)
        {
            output.Clear();

            float aimRadians = aimAngle * Mathf.Deg2Rad;
            Vector2 forward = new(Mathf.Cos(aimRadians), Mathf.Sin(aimRadians));
            Vector2 sideways = new Vector2(-forward.y, forward.x) * side;

            float halfRadians = halfAngle * Mathf.Deg2Rad;
            int steps = Mathf.Max(1, Mathf.CeilToInt(halfRadians * radius * 2f));

            for (int i = 0; i <= steps; i++)
            {
                float offset = halfRadians * i / steps;
                Vector2 point = center +
                    (forward * Mathf.Cos(offset) + sideways * Mathf.Sin(offset)) * radius;
                AddIfNew(output, ToPixel(point));
            }
        }

        // 호 끝 픽셀에서 원의 중심 방향으로 눈금을 긋습니다.
        private static void AppendTick(
            Vector2 center,
            Vector2Int end,
            int tickLength,
            List<Vector2Int> result,
            List<Vector2Int> scratch)
        {
            if (tickLength <= 0)
            {
                return;
            }

            Vector2 start = end + new Vector2(0.5f, 0.5f);
            Vector2 toCenter = center - start;

            if (toCenter.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            toCenter.Normalize();
            scratch.Clear();
            scratch.Add(end);

            for (int i = 1; i <= tickLength * 2; i++)
            {
                AddIfNew(scratch, ToPixel(start + toCenter * (i * 0.5f)));
            }

            RemoveCorners(scratch);

            for (int i = 1; i < scratch.Count; i++)
            {
                if (!result.Contains(scratch[i]))
                {
                    result.Add(scratch[i]);
                }
            }
        }

        // 앞 · 뒤 픽셀과 가로 · 세로로만 붙어 L자를 만드는 가운데 픽셀을 뺍니다.
        private static void RemoveCorners(List<Vector2Int> points)
        {
            if (points.Count < 3)
            {
                return;
            }

            int write = 1;

            for (int read = 1; read < points.Count - 1; read++)
            {
                Vector2Int current = points[read];

                if (!IsCorner(points[write - 1], current, points[read + 1]))
                {
                    points[write++] = current;
                }
            }

            points[write++] = points[points.Count - 1];
            points.RemoveRange(write, points.Count - write);
        }

        private static bool IsCorner(Vector2Int previous, Vector2Int current, Vector2Int next)
        {
            return (previous.x == current.x || previous.y == current.y) &&
                   (next.x == current.x || next.y == current.y) &&
                   previous.x != next.x &&
                   previous.y != next.y;
        }

        private static void AddIfNew(List<Vector2Int> points, Vector2Int pixel)
        {
            if (points.Count == 0 || points[points.Count - 1] != pixel)
            {
                points.Add(pixel);
            }
        }

        private static Vector2Int ToPixel(Vector2 point)
        {
            return new Vector2Int(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y));
        }
    }
}
