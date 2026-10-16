using UnityEngine;
using UnityEngine.Tilemaps;

namespace ShotgunAndRun.Environment
{
    // 미니맵 전용 채움 타일맵. 미니맵 카메라는 Wall 레이어 타일만 그려서, 타일이 없는 맵 바깥과 벽 속 빈 영역이 배경색 그대로 보인다.
    // 이 오브젝트(Minimap 레이어, 충돌 없음)는 어떤 타일맵에도 타일이 없는 칸을 시작할 때 어둠 조각으로 채운다.
    // 메인 카메라는 Minimap 레이어를 그리지 않으므로 인게임 화면 · 충돌에는 영향이 없다.
    // 씬에 구워 두면 칸 수(약 12만)만큼 씬 파일이 수십 MB 늘어서 런타임에 채운다.
    [RequireComponent(typeof(Tilemap))]
    public class MinimapFillTilemap : MonoBehaviour
    {
        [SerializeField] private TileBase _tile;
        [Tooltip("타일이 있는 영역 바깥으로 더 채울 칸 수. 미니맵 카메라 시야 반경보다 크게 잡는다.")]
        [SerializeField] private int _margin = 30;

        private void Start()
        {
            var fill = GetComponent<Tilemap>();
            var maps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);

            var min = new Vector3Int(int.MaxValue, int.MaxValue, 0);
            var max = new Vector3Int(int.MinValue, int.MinValue, 1);
            foreach (var map in maps)
            {
                if (map == fill) continue;
                var bounds = map.cellBounds;
                var offset = fill.WorldToCell(map.transform.position);
                min.x = Mathf.Min(min.x, bounds.xMin + offset.x);
                min.y = Mathf.Min(min.y, bounds.yMin + offset.y);
                max.x = Mathf.Max(max.x, bounds.xMax + offset.x);
                max.y = Mathf.Max(max.y, bounds.yMax + offset.y);
            }

            var area = new BoundsInt(min.x - _margin, min.y - _margin, 0,
                max.x - min.x + _margin * 2, max.y - min.y + _margin * 2, 1);
            var tiles = new TileBase[area.size.x * area.size.y];
            for (int i = 0; i < tiles.Length; i++) tiles[i] = _tile;

            foreach (var map in maps)
            {
                if (map == fill) continue;
                var bounds = map.cellBounds;
                var offset = fill.WorldToCell(map.transform.position);
                var block = map.GetTilesBlock(bounds);
                for (int y = 0; y < bounds.size.y; y++)
                    for (int x = 0; x < bounds.size.x; x++)
                    {
                        if (block[x + y * bounds.size.x] == null) continue;
                        int ax = bounds.xMin + offset.x + x - area.xMin;
                        int ay = bounds.yMin + offset.y + y - area.yMin;
                        tiles[ax + ay * area.size.x] = null;
                    }
            }

            fill.SetTilesBlock(area, tiles);
        }
    }
}
