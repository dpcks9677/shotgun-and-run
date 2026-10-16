using System;
using System.Collections.Generic;
using ShotgunAndRun.Core;
using ShotgunAndRun.Loot;
using UnityEngine;

namespace InventorySystem
{
    /// <summary>
    /// 런이 시작되면 LootGenerator 결과대로 씬의 상자 스폰 지점(ChestSpawnPoint · ChestPointTilemap 칸)에 LootChest를 생성하고 채웁니다 (game-spec 7.2).
    /// 재료는 MaterialType, 퀘스트 아이템은 ID를 이용해 ItemCatalogSO에서 찾습니다.
    /// </summary>
    public class LootSpawner : MonoBehaviour
    {
        [Serializable]
        private sealed class DebugQuest
        {
            public QuestLootKind Kind;
            public string ItemId;
            public string ZoneA;
            public string ZoneB;
        }

        [Header("Rules")]
        [SerializeField] private StageLootConfig _config;
        [SerializeField] private LootChest _chestPrefab;
        [SerializeField] private LootChest _smallChestPrefab;
        [SerializeField] private LootChest _tallEastChestPrefab;
        [SerializeField] private LootChest _tallWestChestPrefab;
        [SerializeField] private Transform _chestRoot;

        [Header("Items")]
        [SerializeField] private ItemCatalogSO _itemCatalog;

        [Header("Seed")]
        [SerializeField] private bool _useFixedSeed;
        [SerializeField] private int _fixedSeed;

        [Header("Debug")]
        [SerializeField] private List<DebugQuest> _debugQuests = new();

        [SerializeField] private bool _spawnWithoutRun = true;

        private readonly List<LootChest> _spawnedChests = new();
        private bool _hasSpawned;
        private bool _subscribedToRun;

        public LootPlan LastPlan
        {
            get;
            private set;
        }
        public IReadOnlyList<LootChest> SpawnedChests => _spawnedChests;

        private void OnEnable()
        {
            SubscribeToRun();
        }

        private void OnDisable()
        {
            if (_subscribedToRun && GameManager.Instance != null)
            {
                GameManager.Instance.RunStarted -= HandleRunStarted;
            }

            _subscribedToRun = false;
        }

        private void Start()
        {
            // OnEnable 시점에 GameManager가 아직 없었을 수 있으므로 한 번 더 시도합니다.
            SubscribeToRun();

            if (_hasSpawned) return;

            RunSession currentRun = GameManager.Instance != null ? GameManager.Instance.CurrentRun : null;
            if (currentRun != null || (_spawnWithoutRun && GameManager.Instance == null))
            {
                Spawn();
            }
        }

        private void SubscribeToRun()
        {
            if (_subscribedToRun || GameManager.Instance == null) return;

            GameManager.Instance.RunStarted += HandleRunStarted;
            _subscribedToRun = true;
        }

        private void HandleRunStarted(RunSession run)
        {
            if (!_hasSpawned)
            {
                Spawn();
            }
        }

        private void Spawn()
        {
            if (_config == null || _chestPrefab == null || _smallChestPrefab == null
                || _tallEastChestPrefab == null || _tallWestChestPrefab == null || _itemCatalog == null)
            {
                Debug.LogWarning(
                    "[LootSpawner] StageLootConfig, 상자 프리팹 또는 " +
                    "ItemCatalogSO가 비어 있어 상자를 생성하지 않습니다.",
                    this);
                return;
            }

            _hasSpawned = true;

            List<ChestPointPlacement> points = ChestPointValidator.CollectScenePoints();
            List<ChestPointInfo> infos = new List<ChestPointInfo>(points.Count);
            foreach (ChestPointPlacement point in points)
            {
                infos.Add(point.ToInfo());
            }

            int seed = _useFixedSeed ? _fixedSeed : Environment.TickCount;
            LootPlan plan = new LootGenerator(_config.Rules).Generate(seed, infos, BuildQuestRequests());
            LastPlan = plan;

            Debug.Log($"[LootSpawner] 시드 {seed}: 상자 {plan.Chests.Count}개 생성 (지점 {points.Count}곳)", this);
            foreach (string warning in plan.Warnings)
            {
                Debug.LogWarning($"[LootSpawner] {warning}", this);
            }

            // 겉모습 변형도 같은 시드로 재현되게 합니다.
            System.Random random = new System.Random(seed);
            foreach (PlannedChest planned in plan.Chests)
            {
                ChestPointPlacement point = points[planned.PointIndex];
                LootChest prefab = PrefabFor(point.Size);
                LootChest chest = Instantiate(prefab, point.Position, Quaternion.identity, _chestRoot);
                chest.name = $"LootChest_{planned.Kind}_{planned.OwnerId}_{planned.PointIndex}";
                if (chest.LookCount > 0)
                {
                    chest.ApplyLook(random.Next(chest.LookCount));
                }

                chest.DisableAutoLoot();

                foreach (MaterialStack stack in planned.Materials)
                {
                    if (_itemCatalog.TryGetByMaterialType(
                            stack.Material,
                            out ItemData item))
                    {
                        AddToChest(chest, item, stack.Amount);
                    }
                    else
                    {
                        Debug.LogWarning($"[LootSpawner] 카탈로그에 {stack.Material} 재료 ItemData가 없습니다.", this);
                    }
                }

                foreach (string itemId in planned.QuestItemIds)
                {
                    if (_itemCatalog.TryGetById(itemId, out ItemData item))
                    {
                        AddToChest(chest, item, 1);
                    }
                    else
                    {
                        Debug.LogWarning($"[LootSpawner] 카탈로그에 퀘스트 아이템 '{itemId}'가 없습니다.", this);
                    }
                }

                _spawnedChests.Add(chest);
            }
        }

        private LootChest PrefabFor(ChestPointSize size)
        {
            switch (size)
            {
                case ChestPointSize.Large:
                    return _chestPrefab;
                case ChestPointSize.TallEast:
                    return _tallEastChestPrefab;
                case ChestPointSize.TallWest:
                    return _tallWestChestPrefab;
                default:
                    return _smallChestPrefab;
            }
        }

        private void AddToChest(LootChest chest, ItemData item, int amount)
        {
            if (!chest.ChestInventory.AddItem(item, amount))
            {
                Debug.LogWarning($"[LootSpawner] {chest.name}의 칸이 부족해 '{item.ItemName}' {amount}개를 모두 넣지 못했습니다.",
                    chest);
            }
        }

        private List<QuestLootRequest> BuildQuestRequests()
        {
            List<QuestLootRequest> requests = GameManager.Instance?.Quests?.GetLootRequests()
                                              ?? new List<QuestLootRequest>();
            foreach (DebugQuest quest in _debugQuests)
            {
                requests.Add(new QuestLootRequest(quest.Kind, quest.ItemId, quest.ZoneA, quest.ZoneB));
            }

            return requests;
        }
    }
}
