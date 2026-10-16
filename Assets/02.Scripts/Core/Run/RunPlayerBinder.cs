using System;
using System.Collections.Generic;
using InventorySystem;
using ShotgunAndRun.Player;
using ShotgunAndRun.Weapon;
using UnityEngine;
using PlayerComponent = ShotgunAndRun.Player.Player;
using Random = UnityEngine.Random;

namespace ShotgunAndRun.Core
{
    // 런과 플레이어를 연결합니다. Core에서 Player 코드를 직접 다루는 곳은 이 클래스뿐입니다.
    // - 스폰 포인트 배치
    // - 플레이어 사망 → 런 종료 콜백
    // - 제한 시간 초과 → 페널티 적용 (game-spec 3.4)
    // 플레이어가 런 시작 후에 생성되면 Tick에서 연결될 때까지 다시 시도합니다.
    public sealed class RunPlayerBinder
    {
        private const string OverTimePenaltySourceId = "RunOverTime";

        private GameManager _gameManager;
        private RunSession _run;
        private RunRules _rules;
        private WeaponUpgradeState _upgradeState;
        private Action<string> _onPlayerDied;
        private PlayerComponent _player;
        private bool _placeAtSpawn;

        public bool IsBound => _player != null;

        // placeAtSpawn: 플레이어를 연결할 때 스폰 포인트로 옮길지 여부 (쉘터에서 출격한 런만 true)
        public void Attach(
            RunSession run,
            RunRules rules,
            WeaponUpgradeState upgradeState,
            Action<string> onPlayerDied,
            bool placeAtSpawn)
        {
            Detach();

            _gameManager = GameManager.Instance;
            _run = run ?? throw new ArgumentNullException(nameof(run));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _upgradeState = upgradeState ?? throw new ArgumentNullException(nameof(upgradeState));
            _onPlayerDied = onPlayerDied;
            _placeAtSpawn = placeAtSpawn;
            _run.TimeLimitExceeded += ApplyOverTimePenalty;
            _gameManager.StateChanged += ApplyStateRule;

            TryBindPlayer();
        }

        public void Tick()
        {
            if (_run != null && !_run.IsEnded && _player == null)
            {
                TryBindPlayer();
            }
        }

        public void Detach()
        {
            if (_run != null)
            {
                _run.TimeLimitExceeded -= ApplyOverTimePenalty;
            }

            if (_player != null && _player.Health != null)
            {
                _player.Health.DiedWithCause -= HandlePlayerDied;
            }

            if (_gameManager != null)
            {
                _gameManager.StateChanged -= ApplyStateRule;
            }

            _run = null;
            _rules = null;
            _upgradeState = null;
            _onPlayerDied = null;
            _player = null;
            _placeAtSpawn = false;
        }

        private void TryBindPlayer()
        {
            PlayerComponent player = PlayerComponent.PlayerInstance;
            if (player == null || player.Health == null)
            {
                return;
            }

            _player = player;
            _player.SetWeaponUpgradeState(_upgradeState);
            // 던전 입장 시 첫발은 일반탄으로 시작하고, 이후 크리티컬 주기는 유지합니다.
            _player.RefillWeaponMagazine(skipFirstCriticalShot: true);
            int playerLevel = _gameManager != null && _gameManager.Progress != null
                ? _gameManager.Progress.PlayerLevel
                : 1;
            _player.ApplyPlayerLevel(playerLevel);
            RestorePlayerConsumables();
            _player.Health.DiedWithCause += HandlePlayerDied;

            // 이벤트가 Player 연결보다 먼저 발생했을 때도 현재 상태를 반영합니다.
            if (_gameManager != null)
            {
                _player.SetShelterMode(
                    _gameManager.State == GameState.Shelter);
                _player.SetDungeonLight(_gameManager.State == GameState.Run);
            }

            if (_placeAtSpawn)
            {
                PlaceAtRandomSpawn();
            }

            // 제한 시간이 지난 뒤에 연결된 경우에도 페널티를 적용합니다.
            if (_run.IsOverTime)
            {
                ApplyOverTimePenalty();
            }
        }

        // 스폰 포인트 3곳 중 1곳 랜덤 (game-spec 4.1). 스폰 포인트가 없는 씬이면 그대로 둡니다.
        private void PlaceAtRandomSpawn()
        {
            RunSpawnPoint spawn = null;
            if (_gameManager != null && _gameManager.Quests != null && _gameManager.Quests.TryGetFixedSpawnId(out string fixedId))
            {
                spawn = RunSpawnPoint.FindById(fixedId);
                if (spawn == null)
                {
                    Debug.LogWarning($"[Run] Quest fixed spawn '{fixedId}' not found. Using random spawn.");
                }
            }

            spawn ??= RunSpawnPoint.PickRandom();
            if (spawn == null)
            {
                return;
            }

            PlayerPlacement.MoveTo(_player, spawn.transform.position);
            Debug.Log($"[Run] Spawned at {spawn.SpawnId}.", spawn);
        }

        public bool TryKillPlayer(string deathCause)
        {
            if (_player != null && _player.Health != null && !_player.Health.IsDead)
            {
                _player.Health.Kill(deathCause);
                return true;
            }

            return false;
        }

        private void HandlePlayerDied(string cause)
        {
            _onPlayerDied?.Invoke(cause);
        }

        public IReadOnlyList<RunItemRecord> GetInventorySnapshots()
        {
            if (_player == null)
            {
                return Array.Empty<RunItemRecord>();
            }

            IInventorySnapshotProvider provider = _player.GetComponent<IInventorySnapshotProvider>();
            if (provider == null)
            {
                return Array.Empty<RunItemRecord>();
            }

            IReadOnlyList<ItemAmountSnapshot> rawSnapshots = provider.GetItemSnapshots();
            if (rawSnapshots == null || rawSnapshots.Count == 0)
            {
                return Array.Empty<RunItemRecord>();
            }

            List<RunItemRecord> records = new List<RunItemRecord>(rawSnapshots.Count);
            for (int i = 0; i < rawSnapshots.Count; i++)
            {
                var s = rawSnapshots[i];
                if (s.Item != null && s.Amount > 0)
                {
                    records.Add(new RunItemRecord(s.Item, s.Amount));
                }
            }

            return records;
        }

        public void ClearPlayerInventory()
        {
            if (_player != null)
            {
                _player.GetComponent<IInventorySnapshotProvider>()?.ClearAllItems();
            }
        }

        // 체력 1칸 고정은 PlayerHealth에 해당 기능이 추가되면 여기에 연결합니다.
        private void ApplyOverTimePenalty()
        {
            if (_player == null || _player.RuntimeStats == null)
            {
                return;
            }

            _player.RuntimeStats.SetMoveSpeedMultiplier(
                OverTimePenaltySourceId,
                _rules.OverTimeMoveSpeedMultiplier);

            _player.RuntimeStats.SetMaxHealthMultiplier(
                OverTimePenaltySourceId,
                _rules.OverTimeMaxHealthDebuffMultiplier);
        }

        private void ApplyStateRule(GameState before, GameState after)
        {
            if (_player == null)
            {
                return;
            }

            _player.SetShelterMode(after == GameState.Shelter);
            _player.SetDungeonLight(after == GameState.Run);
        }

        public void SaveConsumablesToProgress(ProgressData progress)
        {
            PlayerComponent player = _player != null ? _player : PlayerComponent.PlayerInstance;
            if (player == null || progress == null || player.ConsumableInventory == null)
            {
                return;
            }

            var snapshots = player.ConsumableInventory.GetSlotSnapshots();
            if (snapshots == null)
            {
                return;
            }

            progress.QuickSlots.Clear();
            for (int i = 0; i < snapshots.Length; i++)
            {
                progress.QuickSlots.Add(new QuickSlotRecord(snapshots[i].typeName, snapshots[i].amount));
            }
        }

        public void RestorePlayerConsumables()
        {
            PlayerComponent player = _player != null ? _player : PlayerComponent.PlayerInstance;
            GameManager gameManager = _gameManager != null ? _gameManager : GameManager.Instance;
            if (player == null || gameManager == null || gameManager.Progress == null ||
                player.ConsumableInventory == null)
            {
                return;
            }

            var quickSlots = gameManager.Progress.QuickSlots;
            var snapshots = new List<(string typeName, int amount)>();
            if (quickSlots != null)
            {
                for (int i = 0; i < quickSlots.Count; i++)
                {
                    snapshots.Add((quickSlots[i].ConsumableTypeName, quickSlots[i].Amount));
                }
            }

            while (snapshots.Count < player.ConsumableInventory.SlotCount)
            {
                snapshots.Add((string.Empty, 0));
            }

            player.ConsumableInventory.RestoreFromSnapshots(snapshots);
        }

        // 씬 전환 중 이동 · 대시 · 사격 · 재장전 입력을 막습니다.
        public void SetPlayerInputLocked(bool locked)
        {
            PlayerComponent player = _player != null ? _player : PlayerComponent.PlayerInstance;
            if (player != null)
            {
                player.GetComponent<PlayerInputReader>().SetActionsEnabled(!locked);
            }
        }

        public void ApplyCurrentSceneState(GameState state)
        {
            PlayerComponent player =
                _player != null ? _player : PlayerComponent.PlayerInstance;

            player?.SetShelterMode(state == GameState.Shelter);
            player?.SetDungeonLight(state == GameState.Run);

            if (state == GameState.Shelter)
            {
                RestorePlayerConsumables();
            }
        }
    }
}
