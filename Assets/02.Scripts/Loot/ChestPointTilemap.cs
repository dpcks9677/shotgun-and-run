using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ShotgunAndRun.Loot
{
    // 상자 스폰 지점을 칠하는 타일맵. 타일맵 하나가 존 하나 또는 구간 하나를 맡습니다.
    // Owner Id를 비워 두면 GameObject 이름을 씁니다 (예: "Z1", "Z1-Z9").
    [RequireComponent(typeof(Tilemap))]
    public sealed class ChestPointTilemap : MonoBehaviour
    {
        [Tooltip("비워 두면 GameObject 이름을 사용")]
        [SerializeField] private string _ownerId;

        private Tilemap _tilemap;

        public string OwnerId => string.IsNullOrEmpty(_ownerId) ? name : _ownerId;

        private Tilemap Tilemap => _tilemap != null ? _tilemap : _tilemap = GetComponent<Tilemap>();

        private void Awake()
        {
            // 마커는 에디터에서 배치할 때만 보이면 됩니다.
            if (TryGetComponent(out TilemapRenderer tilemapRenderer))
            {
                tilemapRenderer.enabled = false;
            }
        }

        public void CollectPoints(List<ChestPointPlacement> points)
        {
            Tilemap tilemap = Tilemap;
            foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
            {
                if (tilemap.GetTile(cell) is ChestPointTile tile)
                {
                    points.Add(new ChestPointPlacement(tile.Kind, OwnerId, tilemap.GetCellCenterWorld(cell), tile.Size));
                }
            }
        }

#if UNITY_EDITOR
        // 타일맵을 선택하면 씬의 모든 지점을 검사해 표시합니다 (배치할 때 확인용).
        // 빨강 = 상자 자리가 막힘, 주황 = 커넥터(홀) 상자가 벽 앞이 아님, 초록 = 통과
        private void OnDrawGizmosSelected()
        {
            List<ChestPointPlacement> points = ChestPointValidator.CollectScenePoints();
            HashSet<int> blocked = new HashSet<int>(ChestPointValidator.FindBlocked(points));
            HashSet<int> offWall = new HashSet<int>(ChestPointValidator.FindNotAgainstWall(points));
            for (int i = 0; i < points.Count; i++)
            {
                Rect footprint = points[i].Footprint;
                Gizmos.color = blocked.Contains(i) ? Color.red
                    : offWall.Contains(i) ? new Color(1f, 0.55f, 0.1f)
                    : new Color(0.3f, 1f, 0.4f, 0.6f);
                Gizmos.DrawWireCube(footprint.center, footprint.size * 0.9f);
            }
        }

        [ContextMenu("상자 자리 검사")]
        private void ValidateInScene()
        {
            List<ChestPointPlacement> points = ChestPointValidator.CollectScenePoints();
            List<int> blocked = ChestPointValidator.FindBlocked(points);
            foreach (int index in blocked)
            {
                ChestPointPlacement point = points[index];
                Debug.LogWarning($"[ChestPointTilemap] {point.OwnerId} {point.Kind} {point.Size} 지점 {point.Position}: 상자 자리가 벽 · 소품 · 다른 지점과 겹칩니다.", this);
            }

            List<int> offWall = ChestPointValidator.FindNotAgainstWall(points);
            foreach (int index in offWall)
            {
                ChestPointPlacement point = points[index];
                Debug.LogWarning($"[ChestPointTilemap] {point.OwnerId} {point.Size} 지점 {point.Position}: 커넥터(홀) 상자가 벽 앞에 붙어 있지 않습니다.", this);
            }

            Debug.Log($"[ChestPointTilemap] 상자 자리 검사: 지점 {points.Count}곳 중 막힘 {blocked.Count}곳, 벽 앞 아님 {offWall.Count}곳", this);
        }
#endif
    }
}
