using UnityEngine;

namespace ShotgunAndRun.Loot
{
    // 씬에 놓인 상자 스폰 지점 하나 (ChestSpawnPoint 또는 ChestPointTilemap의 한 칸)
    public readonly struct ChestPointPlacement
    {
        // anchor: 지점 칸 중심. Large는 오른쪽 칸, Tall은 위 칸까지 차지하므로 상자 위치가 반 칸 옮겨집니다.
        public ChestPointPlacement(ChestPointKind kind, string ownerId, Vector3 anchor, ChestPointSize size)
        {
            Kind = kind;
            OwnerId = ownerId;
            Size = size;
            Vector2 cells = CellSize(size);
            Position = anchor + new Vector3((cells.x - 1f) * 0.5f, (cells.y - 1f) * 0.5f, 0f);
        }

        public ChestPointKind Kind
        {
            get;
        }
        public string OwnerId
        {
            get;
        }
        public ChestPointSize Size
        {
            get;
        }

        // 상자를 놓을 위치 (상자 칸들의 가운데)
        public Vector3 Position
        {
            get;
        }

        // 상자가 차지하는 칸 영역
        public Rect Footprint
        {
            get
            {
                Vector2 size = CellSize(Size);
                return new Rect((Vector2)Position - size * 0.5f, size);
            }
        }

        // 크기별 칸 수 (가로, 세로)
        public static Vector2 CellSize(ChestPointSize size)
        {
            switch (size)
            {
                case ChestPointSize.Large:
                    return new Vector2(2f, 1f);
                case ChestPointSize.TallEast:
                case ChestPointSize.TallWest:
                    return new Vector2(1f, 2f);
                default:
                    return Vector2.one;
            }
        }

        public ChestPointInfo ToInfo()
        {
            return new ChestPointInfo(Kind, OwnerId);
        }
    }
}
