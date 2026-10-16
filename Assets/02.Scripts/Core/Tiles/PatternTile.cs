using UnityEngine;
using UnityEngine.Tilemaps;

namespace ShotgunAndRun.Core
{
    // 큰 반복 패턴을 칸 크기 조각으로 나눠, 칸 좌표에 맞는 조각을 골라 칠하는 타일입니다.
    // 칸 (x, y)에는 항상 같은 조각이 들어가므로, 칠하는 범위를 늘리거나 줄여도 패턴이 끊기지 않습니다.
    [CreateAssetMenu(fileName = "PatternTile", menuName = "ShotgunAndRun/Tiles/Pattern Tile")]
    public class PatternTile : TileBase
    {
        // 조각은 패턴의 왼쪽 위부터 한 줄씩 순서대로 넣습니다.
        [SerializeField] private int _columns = 4;
        [SerializeField] private Sprite[] _sprites;
        [SerializeField] private Tile.ColliderType _colliderType = Tile.ColliderType.None;

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            if (_sprites == null || _columns <= 0 || _sprites.Length < _columns)
            {
                return;
            }

            int rows = _sprites.Length / _columns;
            int column = Mod(position.x, _columns);
            // 타일맵 y는 위로 갈수록 커지므로 조각 줄 순서를 뒤집습니다.
            int row = rows - 1 - Mod(position.y, rows);

            tileData.sprite = _sprites[row * _columns + column];
            tileData.color = Color.white;
            tileData.transform = Matrix4x4.identity;
            tileData.flags = TileFlags.LockColor;
            tileData.colliderType = _colliderType;
        }

        private static int Mod(int value, int divisor)
        {
            return (value % divisor + divisor) % divisor;
        }
    }
}
