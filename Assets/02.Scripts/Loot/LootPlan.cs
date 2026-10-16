using System.Collections.Generic;
using InventorySystem;

namespace ShotgunAndRun.Loot
{
    // 씬의 상자 스폰 지점 하나. LootGenerator는 인덱스로만 지점을 구분합니다.
    public readonly struct ChestPointInfo
    {
        public ChestPointInfo(ChestPointKind kind, string ownerId)
        {
            Kind = kind;
            OwnerId = ownerId;
        }

        public ChestPointKind Kind
        {
            get;
        }

        // Zone · RecoveryFixed = 존 ID, Connector = 구간 ID
        public string OwnerId
        {
            get;
        }
    }

    // 이번 런에 수주한 퀘스트가 요구하는 아이템
    public readonly struct QuestLootRequest
    {
        public QuestLootRequest(QuestLootKind kind, string itemId, string zoneA, string zoneB = null)
        {
            Kind = kind;
            ItemId = itemId;
            ZoneA = zoneA;
            ZoneB = zoneB;
        }

        public QuestLootKind Kind
        {
            get;
        }
        public string ItemId
        {
            get;
        }
        public string ZoneA
        {
            get;
        }

        // 보물찾기만 사용 (인접한 두 번째 존)
        public string ZoneB
        {
            get;
        }
    }

    public sealed class PlannedChest
    {
        private readonly List<MaterialStack> _materials = new List<MaterialStack>();
        private readonly List<string> _questItemIds = new List<string>();

        public PlannedChest(int pointIndex, ChestPointKind kind, string ownerId)
        {
            PointIndex = pointIndex;
            Kind = kind;
            OwnerId = ownerId;
        }

        public int PointIndex
        {
            get;
        }
        public ChestPointKind Kind
        {
            get;
        }
        public string OwnerId
        {
            get;
        }

        public IReadOnlyList<MaterialStack> Materials => _materials;
        public IReadOnlyList<string> QuestItemIds => _questItemIds;

        public int CountOf(MaterialType material)
        {
            foreach (MaterialStack stack in _materials)
            {
                if (stack.Material == material)
                {
                    return stack.Amount;
                }
            }

            return 0;
        }

        // 같은 재료는 한 항목으로 합칩니다.
        internal void AddMaterial(MaterialType material, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            for (int i = 0; i < _materials.Count; i++)
            {
                if (_materials[i].Material == material)
                {
                    _materials[i] = new MaterialStack(material, _materials[i].Amount + amount);
                    return;
                }
            }

            _materials.Add(new MaterialStack(material, amount));
        }

        internal void AddQuestItem(string itemId)
        {
            _questItemIds.Add(itemId);
        }
    }

    // LootGenerator의 결과. 씬에 상자를 만들 때 이 목록만 보면 됩니다.
    public sealed class LootPlan
    {
        private readonly List<PlannedChest> _chests = new List<PlannedChest>();
        private readonly List<string> _warnings = new List<string>();

        public LootPlan(int seed)
        {
            Seed = seed;
        }

        public int Seed
        {
            get;
        }
        public IReadOnlyList<PlannedChest> Chests => _chests;

        // 데이터 누락 등으로 규칙대로 만들지 못한 항목 (런은 계속 진행)
        public IReadOnlyList<string> Warnings => _warnings;

        public int TotalOf(MaterialType material)
        {
            int total = 0;
            foreach (PlannedChest chest in _chests)
            {
                total += chest.CountOf(material);
            }

            return total;
        }

        internal void Add(PlannedChest chest)
        {
            _chests.Add(chest);
        }

        internal void Warn(string message)
        {
            _warnings.Add(message);
        }
    }
}
