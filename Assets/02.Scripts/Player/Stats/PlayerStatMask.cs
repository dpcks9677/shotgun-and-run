using System;

namespace ShotgunAndRun.Player
{
    // 플레이어 능력치 변동을 비트 플래그로 나타낸다.
    [Flags]
    public enum PlayerStatMask
    {
        None = 0,
        MaxHealth = 1 << 0,
        MoveSpeed = 1 << 1,
        DashSpeed = 1 << 2,
        DashCooldown = 1 << 3
    }
}
