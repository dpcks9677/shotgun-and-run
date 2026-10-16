using UnityEngine;
using UnityEngine.Tilemaps;

namespace ShotgunAndRun.Loot
{
    // 상자 스폰 지점 마커 타일. 칠한 칸이 곧 상자 후보 지점이 됩니다.
    // 소속(존 · 구간 ID)은 타일이 아니라 칠한 타일맵(ChestPointTilemap)이 정합니다.
    // Size가 Large면 칠한 칸과 그 오른쪽 칸에 2x1, Tall이면 칠한 칸과 그 위 칸에 1x2 상자가 놓입니다.
    [CreateAssetMenu(fileName = "ChestPointTile", menuName = "ShotgunAndRun/Loot/Chest Point Tile")]
    public class ChestPointTile : Tile
    {
        [SerializeField] private ChestPointKind _kind = ChestPointKind.Zone;
        [SerializeField] private ChestPointSize _size = ChestPointSize.Small;

        public ChestPointKind Kind => _kind;
        public ChestPointSize Size => _size;

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            base.GetTileData(position, tilemap, ref tileData);
            tileData.colliderType = ColliderType.None;
        }
    }
}
