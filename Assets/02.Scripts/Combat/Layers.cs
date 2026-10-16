namespace ShotgunAndRun.Combat
{
    /// <summary>
    /// 물리 레이어 번호입니다. ProjectSettings/TagManager의 레이어를 옮기면 여기도 함께 고칩니다.
    /// </summary>
    public static class Layers
    {
        public const int Default = 0;

        // 이동만 막습니다. 총알 · 적 투사체 · 시야는 통과합니다 (물).
        // Unity 내장 레이어라 이름을 바꿀 수 없습니다.
        public const int Water = 4;

        public const int Player = 6;
        public const int Enemy = 7;

        // 이동 · 총알을 막는 엄폐물입니다. 시야는 통과하고 미니맵에는 그려지지 않습니다 (방 프리팹 Block 타일맵의 소품).
        public const int Obstacle = 8;

        public const int PlayerHitbox = 9;

        // 이동 · 총알 · 시야를 모두 막습니다. 미니맵에도 그려집니다.
        public const int Wall = 13;
    }
}
