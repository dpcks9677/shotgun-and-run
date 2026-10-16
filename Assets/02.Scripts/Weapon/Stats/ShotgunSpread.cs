using UnityEngine;

namespace ShotgunAndRun.Weapon
{
    // 펠릿 발사 각도 계산 (조준 방향 기준, 단위: 도)
    // 전체 확산각을 펠릿 수만큼 칸으로 나누고 각 칸 가운데에 펠릿을 하나씩 둡니다.
    // 흔들림(jitterOffset)을 더한 뒤 ±halfAngle로 잘라서 인디케이터 호 밖으로 나가지 않게 합니다.
    public static class ShotgunSpread
    {
        public static float GetPelletAngle(int index, int count, float halfAngle, float jitterOffset)
        {
            halfAngle = Mathf.Max(0f, halfAngle);

            if (count <= 1)
            {
                return Mathf.Clamp(jitterOffset, -halfAngle, halfAngle);
            }

            float slotWidth = 2f * halfAngle / count;
            float baseAngle = -halfAngle + (index + 0.5f) * slotWidth;

            return Mathf.Clamp(baseAngle + jitterOffset, -halfAngle, halfAngle);
        }
    }
}
