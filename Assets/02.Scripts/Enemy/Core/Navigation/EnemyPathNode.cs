using UnityEngine;

namespace ShotgunAndRun.Enemy
{
    public class EnemyPathNode
    {
        // EnemyPathNode의 정수형 Vector2 좌표
        private Vector2Int _cellPosition;
        public Vector2Int CellPosition => _cellPosition;

        // A * 알고리즘 계산에 필요한 프로퍼티
        public int GCost    // 출발지부터 현재 칸까지의 누적 이동 비용
        {
            get;
            set;
        } = int.MaxValue;
        public int HCost    // 현재 칸에서 목적지까지의 예상 이동 비용
        {
            get;
            set;
        }
        public int FCost => GCost == int.MaxValue ? int.MaxValue : GCost + HCost;
        public EnemyPathNode Parent
        {
            get;
            set;
        }

        public EnemyPathNode(Vector2Int cellPosition)
        {
            _cellPosition = cellPosition;
        }
    }
}
