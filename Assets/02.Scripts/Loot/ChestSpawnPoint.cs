using UnityEngine;

namespace ShotgunAndRun.Loot
{
    // 상자가 생성될 수 있는 위치. 매 런 LootGenerator가 이 중 일부에만 상자를 만듭니다.
    // Owner Id: Zone · RecoveryFixed = 존 ID (예: "Z1"), Connector = 구간 ID (예: "Z1-Z9")
    public sealed class ChestSpawnPoint : MonoBehaviour
    {
        [SerializeField] private ChestPointKind _kind = ChestPointKind.Zone;
        [SerializeField] private string _ownerId;
        [SerializeField] private ChestPointSize _size = ChestPointSize.Small;

        public ChestPointKind Kind => _kind;
        public string OwnerId => _ownerId;

        public ChestPointInfo ToInfo()
        {
            return new ChestPointInfo(_kind, _ownerId);
        }

        public ChestPointPlacement ToPlacement()
        {
            return new ChestPointPlacement(_kind, _ownerId, transform.position, _size);
        }

        private void OnDrawGizmos()
        {
            switch (_kind)
            {
                case ChestPointKind.Zone:
                    Gizmos.color = new Color(1f, 0.8f, 0.2f);
                    break;
                case ChestPointKind.Connector:
                    Gizmos.color = new Color(0.3f, 0.8f, 1f);
                    break;
                default:
                    Gizmos.color = new Color(1f, 0.4f, 0.8f);
                    break;
            }

            Rect footprint = ToPlacement().Footprint;
            Gizmos.DrawWireCube(footprint.center, footprint.size * 0.8f);
        }
    }
}
