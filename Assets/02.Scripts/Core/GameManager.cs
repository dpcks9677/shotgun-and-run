using System;
using System.Collections;
using System.Collections.Generic;
using InventorySystem;
using ShotgunAndRun.Quest;
using ShotgunAndRun.Weapon;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShotgunAndRun.Core
{
    // 게임 흐름(타이틀 → (첫 시작이면 튜토리얼) → 쉘터 → 런 → 결과 → 쉘터)과 진행 데이터를 관리합니다.
    // GameBootstrap이 첫 씬 로드 전에 생성하며, 씬이 바뀌어도 유지됩니다.
    // 씬과 상태의 대응은 SceneRouter, 플레이어 연결은 RunPlayerBinder가 담당합니다.
    // 대응표에 없는 씬(테스트 씬, 샌드박스 씬)에서는 대기 상태(None)로 아무 흐름도 진행하지 않습니다.
    [DisallowMultipleComponent]
    public sealed class GameManager : MonoBehaviour, IWeaponUpgradeContext
    {
        [Header("Scenes")]
        [SerializeField] private SceneRouter _sceneRouter = new SceneRouter();
        [SerializeField] private SceneTransition _sceneTransition;

        [Header("Run")]
        [SerializeField] private RunRules _runRules = new RunRules();
        [SerializeField] private bool _autoReturnToShelter = false;
        [SerializeField, Min(0f)] private float _returnToShelterDelay = 2f;

        [Header("Quest")]
        [SerializeField] private QuestCatalogSO _questCatalog;

        [Header("Save")]
        [SerializeField] private bool _useSaveFileInEditor;

        private readonly RunPlayerBinder _playerBinder = new RunPlayerBinder();
        private SaveSystem _saveSystem;
        private Coroutine _returnRoutine;
        private bool _isTestRun;
        private bool _initialSceneHandled;

        // StartRun으로 스테이지 씬을 불러오는 중인지 여부
        // 쉘터에서 출격한 런만 스폰 포인트에 배치하고, 스테이지 씬에서 바로 Play한 경우는 씬 배치 위치에서 시작합니다.
        private bool _isRunSceneRequested;

        public static GameManager Instance
        {
            get;
            private set;
        }

        // (이전 상태, 다음 상태)
        public event Action<GameState, GameState> StateChanged;
        public event Action<RunSession> RunStarted;
        public event Action<RunResult> RunEnded;

        public GameState State
        {
            get;
            private set;
        }

        // 런 중이 아니면 null입니다.
        public RunSession CurrentRun
        {
            get;
            private set;
        }

        // 씬 전환 연출(페이드 · 로딩) 중인지 여부. 전환 중에는 플레이어 입력과 런 시간이 멈춥니다.
        public bool IsSceneTransitioning
        {
            get;
            private set;
        }

        public RunResult LastRunResult
        {
            get;
            private set;
        }

        public ProgressData Progress
        {
            get;
            private set;
        }

        // 카탈로그가 비어 있으면 null입니다.
        public QuestService Quests
        {
            get;
            private set;
        }

        public WeaponUpgradeState UpgradeState => Progress?.WeaponParts;

        public RunRules Rules => _runRules;

        // 사망 이벤트가 연결된 플레이어가 있는지 여부
        public bool IsPlayerBound => _playerBinder.IsBound;

        private bool UseSaveFile => !Application.isEditor || _useSaveFileInEditor;

        // 불러올 저장 파일이 있는지 여부
        public bool HasSave => UseSaveFile && _saveSystem.HasSave;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (GetComponent<GameDebugTools>() == null)
            {
                gameObject.AddComponent<GameDebugTools>();
            }
#endif

            _saveSystem = SaveSystem.CreateDefault();
            if (_questCatalog == null)
            {
                Debug.LogWarning("GameManager Quest Catalog is not assigned. Quest system is disabled.", this);
            }

            SetProgress(UseSaveFile ? _saveSystem.Load() : new ProgressData());

            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void Start()
        {
            // Enter Play Mode 설정에 따라 첫 씬에서 sceneLoaded가 호출되지 않을 수 있으므로 직접 처리합니다.
            if (!_initialSceneHandled)
            {
                HandleScene(SceneManager.GetActiveScene());
            }
        }

        private void Update()
        {
            _playerBinder.Tick();

            // 런 시간은 페이드 인이 끝난 뒤부터 흐릅니다.
            if (!IsSceneTransitioning)
            {
                CurrentRun?.Tick(Time.deltaTime);
            }
        }

        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            _playerBinder.Detach();
            Instance = null;
        }

        private void OnApplicationQuit()
        {
            if (State == GameState.Shelter)
            {
                SaveProgress();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            foreach (string sceneName in _sceneRouter.FindScenesMissingFromBuild())
            {
                Debug.LogWarning($"GameManager scene route '{sceneName}' is not in Build Settings.", this);
            }
        }
#endif

        public void GoToTitle()
        {
            LoadSceneFor(GameState.Title);
        }

        // 타이틀의 New game: 저장된 진행을 버리고 처음부터 시작합니다. 덮어쓰기 확인은 호출하는 UI가 맡습니다.
        public void StartNewGame()
        {
            if (IsSceneTransitioning)
            {
                return;
            }

            if (UseSaveFile)
            {
                _saveSystem.Delete();
            }

            SetProgress(new ProgressData());
            StartGame();
        }

        // 타이틀의 Load game: 저장 파일의 진행으로 이어합니다. 저장이 없으면 아무 것도 하지 않습니다.
        public void LoadGame()
        {
            if (IsSceneTransitioning || !HasSave)
            {
                return;
            }

            SetProgress(_saveSystem.Load());
            StartGame();
        }

        // 현재 진행에서 튜토리얼을 마치지 않았으면 튜토리얼, 마쳤으면 쉘터로 갑니다.
        private void StartGame()
        {
            LoadSceneFor(Progress.Quests.HasFlag(QuestFlags.TutorialCompleted) ? GameState.Shelter : GameState.Tutorial);
        }

        public void GoToShelter()
        {
            LoadSceneFor(GameState.Shelter);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // 씬 전환 전에 화면만 먼저 어둡게 합니다. 전환 연출이 없는 GameManager는 바로 끝납니다.
        public IEnumerator FadeOut(float duration)
        {
            if (_sceneTransition != null)
            {
                yield return _sceneTransition.FadeOut(duration);
            }
        }

        // 스테이지 씬을 불러오며, 씬 로드가 끝나면 런이 시작됩니다.
        public void StartRun()
        {
            // 전환 중 재요청이 아래 _isRunSceneRequested를 false로 덮어쓰지 않도록 여기서 막습니다.
            if (IsRunActive() || IsSceneTransitioning)
            {
                return;
            }

            // 쉘터 플레이어의 소모품 퀵슬롯을 던전 진입 전에 백업
            _playerBinder.SaveConsumablesToProgress(Progress);
            SaveProgress();

            _isRunSceneRequested = LoadSceneFor(GameState.Run);
        }

        public const string DeathCauseAbandoned = "작전 포기";

        public void AbandonRun()
        {
            if (!IsRunActive())
            {
                return;
            }

            if (!_playerBinder.TryKillPlayer(DeathCauseAbandoned))
            {
                EndRun(RunOutcome.Died, DeathCauseAbandoned, true);
            }
        }

        public void EndRun(RunOutcome outcome)
        {
            EndRun(outcome, null, false);
        }

        public void EndRun(RunOutcome outcome, string deathCause)
        {
            EndRun(outcome, deathCause, false);
        }

        public void EndRun(RunOutcome outcome, string deathCause, bool isAbandoned)
        {
            if (!IsRunActive())
            {
                return;
            }

            IReadOnlyList<RunItemRecord> inventorySnapshots = _playerBinder.GetInventorySnapshots();
            IReadOnlyList<RunItemRecord> securedItems = null;
            IReadOnlyList<RunItemRecord> lostItems = null;

            QuestReadyReport questReport = null;

            if (outcome == RunOutcome.Extracted)
            {
                // 결과창(RunResult.SecuredItems)에는 던전에서 무사히 획득한 모든 아이템(재료 및 퀘스트 아이템)을 표시합니다.
                securedItems = inventorySnapshots;

                // 퀘스트 완료 가능 판정은 창고 지급 전에 합니다. 소비된 퀘스트 아이템은 창고에 넣지 않습니다.
                // 런 중 방문한 존 기록은 아직 없어 빈 목록을 넘깁니다.
                if (Quests != null)
                {
                    questReport = Quests.CompleteRun(BuildCarriedAmounts(inventorySnapshots), Array.Empty<string>());
                }

                // 쉘터 창고(Storage)에는 퀘스트에 소모된 아이템 및 퀘스트 아이템을 제외한 일반 보관 전리품만 적재합니다.
                List<RunItemRecord> storageItems = RemoveConsumedQuestItems(
                    inventorySnapshots,
                    questReport != null ? questReport.ConsumedItemIds : Array.Empty<string>());

                if (storageItems != null)
                {
                    for (int i = 0; i < storageItems.Count; i++)
                    {
                        RunItemRecord record = storageItems[i];
                        if (record.Item != null && record.Amount > 0 && record.Item.ItemType != ItemType.Quest)
                        {
                            Progress.AddToStorage(record.Item.Id, record.Amount);
                        }
                    }
                }

                // 탈출 성공 시 남은 소모품 백업
                _playerBinder.SaveConsumablesToProgress(Progress);
                _playerBinder.ClearPlayerInventory();
                SaveProgress();
            }
            else if (outcome == RunOutcome.Died)
            {
                lostItems = inventorySnapshots;
                Quests?.FailRun();
                if (Progress != null && Progress.QuickSlots != null)
                {
                    Progress.QuickSlots.Clear();
                }

                _playerBinder.ClearPlayerInventory();
                SaveProgress();
            }

            RunResult result = CurrentRun.End(outcome, deathCause, securedItems, lostItems, isAbandoned);
            Progress.PlayerXp += result.FinalXp;
            result.QuestReport = questReport;
            LastRunResult = result;
            _playerBinder.Detach();

            SetState(GameState.Result);
            RunEnded?.Invoke(result);

            if (_autoReturnToShelter && !_isTestRun)
            {
                _returnRoutine = StartCoroutine(ReturnToShelterAfterDelay());
            }
        }

        // 퀘스트 보고(완료 처리): 완료 가능 상태일 때만 성공하며 XP · 보상 아이템을 지급하고 저장합니다.
        // 의뢰는 게시판의 완료 버튼이, 스토리는 NPC 보고 컷씬이 호출합니다.
        public bool TryTurnInQuest(QuestDefinitionSO quest)
        {
            if (Quests == null ||
                Progress == null ||
                !Quests.TryTurnIn(quest, out int rewardXp, out List<QuestRewardItem> rewardItems))
            {
                return false;
            }

            Progress.PlayerXp += rewardXp;
            foreach (QuestRewardItem reward in rewardItems)
            {
                Progress.AddToStorage(reward.Item.Id, reward.Amount);
            }

            SaveProgress();
            return true;
        }

        // 컷씬이 재생되는 동안 저장을 보류합니다. 컷씬 도중 종료돼도 퀘스트만 저장되고 완료 플래그는 빠지는 일을 막습니다.
        public void HoldSave(bool hold)
        {
            _saveSystem.IsHeld = hold;
        }

        public void SaveProgress()
        {
            if (!UseSaveFile)
            {
                return;
            }

            if (State == GameState.Shelter && Progress != null)
            {
                _playerBinder.SaveConsumablesToProgress(Progress);
            }

            try
            {
                _saveSystem.Save(Progress);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to save progress. {exception.Message}", this);
            }
        }

        public int GetItemCount(ItemData itemData)
        {
            return itemData == null || Progress == null
                ? 0
                : Progress.GetStoredAmount(itemData.Id);
        }

        public bool HasItem(ItemData itemData, int amount)
        {
            return itemData != null &&
                   amount > 0 &&
                   GetItemCount(itemData) >= amount;
        }

        public bool TryRemoveItem(ItemData itemData, int amount)
        {
            return itemData != null &&
                   Progress != null &&
                   Progress.TryRemoveFromStorage(itemData.Id, amount);
        }

        public void CommitUpgrade()
        {
            SaveProgress();
        }

        // 등록되지 않은 씬에서 타이머 UI 등을 확인하기 위한 테스트 런입니다.
        // 테스트 런은 종료되어도 쉘터로 이동하지 않습니다.
        [ContextMenu("Start Test Run")]
        public void StartTestRun()
        {
            if (IsRunActive())
            {
                return;
            }

            BeginRun(true, false);
        }

        [ContextMenu("Extract (Debug)")]
        private void ExtractForDebug()
        {
            EndRun(RunOutcome.Extracted);
        }

        [ContextMenu("Delete Save")]
        private void DeleteSave()
        {
            (_saveSystem ?? SaveSystem.CreateDefault()).Delete();
        }

        private static Dictionary<string, int> BuildCarriedAmounts(IReadOnlyList<RunItemRecord> records)
        {
            Dictionary<string, int> amounts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < records.Count; i++)
            {
                RunItemRecord record = records[i];
                if (record.Item != null && record.Amount > 0)
                {
                    amounts.TryGetValue(record.Item.Id, out int current);
                    amounts[record.Item.Id] = current + record.Amount;
                }
            }

            return amounts;
        }

        // 퀘스트에 소비된 아이템 수만큼 기록에서 뺍니다.
        private static List<RunItemRecord> RemoveConsumedQuestItems(
            IReadOnlyList<RunItemRecord> records,
            IReadOnlyList<string> consumedItemIds)
        {
            Dictionary<string, int> toConsume = new Dictionary<string, int>(StringComparer.Ordinal);
            if (consumedItemIds != null)
            {
                for (int i = 0; i < consumedItemIds.Count; i++)
                {
                    toConsume.TryGetValue(consumedItemIds[i], out int current);
                    toConsume[consumedItemIds[i]] = current + 1;
                }
            }

            List<RunItemRecord> remaining = new List<RunItemRecord>(records.Count);
            for (int i = 0; i < records.Count; i++)
            {
                RunItemRecord record = records[i];
                int amount = record.Amount;
                if (record.Item != null && toConsume.TryGetValue(record.Item.Id, out int pending) && pending > 0)
                {
                    int used = Math.Min(pending, amount);
                    toConsume[record.Item.Id] = pending - used;
                    amount -= used;
                }

                if (amount > 0)
                {
                    remaining.Add(new RunItemRecord(record.Item, amount));
                }
            }

            return remaining;
        }

        private void SetProgress(ProgressData progress)
        {
            Progress = progress;
            Quests = _questCatalog != null ? new QuestService(progress.Quests, _questCatalog) : null;
        }

        private bool IsRunActive()
        {
            return CurrentRun != null && !CurrentRun.IsEnded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single)
            {
                return;
            }

            HandleScene(scene);
        }

        private void HandleScene(Scene scene)
        {
            _initialSceneHandled = true;

            // 새 씬이 로드되면 이전 씬의 일시정지 상태와 관계없이 항상 정상 속도로 복원합니다.
            Time.timeScale = 1f;

            // 새 씬의 플레이어도 페이드 인이 끝날 때까지 입력을 막습니다.
            if (IsSceneTransitioning)
            {
                _playerBinder.SetPlayerInputLocked(true);
            }

            GameState state = _sceneRouter.Resolve(scene.name);
            bool isRunSceneRequested = _isRunSceneRequested;
            _isRunSceneRequested = false;

            if (state == GameState.Run)
            {
                BeginRun(false, isRunSceneRequested);
                return;
            }

            ClearRun();
            SetState(state);
            _playerBinder.ApplyCurrentSceneState(state);

            if (state == GameState.Shelter)
            {
                SaveProgress();
            }
        }

        private void BeginRun(bool isTestRun, bool placeAtSpawn)
        {
            _isTestRun = isTestRun;
            CurrentRun = new RunSession(_runRules);
            Quests?.BeginRun();
            _playerBinder.Attach(
                CurrentRun,
                _runRules,
                Progress.WeaponParts,
                HandlePlayerDied,
                placeAtSpawn);

            SetState(GameState.Run);
            RunStarted?.Invoke(CurrentRun);
        }

        private void ClearRun()
        {
            // EndRun 없이 런 씬을 떠난 경우(메뉴로 쉘터 · 타이틀 이동): 이번 런 진행만 버리고 수주는 유지합니다.
            // 남겨 두면 쉘터에서 수주 · 포기 · 보고가 모두 런 중으로 막힙니다.
            if (Quests != null && Quests.IsRunActive)
            {
                Quests.FailRun();
            }

            _playerBinder.Detach();
            CurrentRun = null;
            _isTestRun = false;
        }

        private void HandlePlayerDied(string deathCause)
        {
            bool isAbandoned = deathCause == DeathCauseAbandoned;
            EndRun(RunOutcome.Died, deathCause, isAbandoned);
        }

        private void SetState(GameState next)
        {
            if (State == next)
            {
                return;
            }

            GameState previous = State;
            State = next;
            StateChanged?.Invoke(previous, next);
        }

        // 씬 로드를 시작했으면 true
        private bool LoadSceneFor(GameState state)
        {
            if (IsSceneTransitioning)
            {
                return false;
            }

            if (!_sceneRouter.TryGetScene(state, out string sceneName))
            {
                Debug.LogWarning($"No scene is routed to {state}.", this);
                return false;
            }

            if (_returnRoutine != null)
            {
                StopCoroutine(_returnRoutine);
                _returnRoutine = null;
            }

            // 프리팹 없이 생성된 GameManager(GameBootstrap 기본값)는 연출 없이 바로 전환합니다.
            if (_sceneTransition == null)
            {
                SceneManager.LoadScene(sceneName);
                return true;
            }

            StartCoroutine(TransitionTo(sceneName));
            return true;
        }

        private IEnumerator TransitionTo(string sceneName)
        {
            IsSceneTransitioning = true;
            _playerBinder.SetPlayerInputLocked(true);

            yield return _sceneTransition.Play(sceneName);

            IsSceneTransitioning = false;
            _playerBinder.SetPlayerInputLocked(false);
        }

        private IEnumerator ReturnToShelterAfterDelay()
        {
            yield return new WaitForSecondsRealtime(_returnToShelterDelay);
            _returnRoutine = null;
            GoToShelter();
        }

        // Start Test Run으로 RunPlayerBinder를 연결한 뒤 실행합니다.
        // 씬을 로드하지 않고 실제 StateChanged 경로만 검사합니다.
        [ContextMenu("Test/Set Shelter State")]
        private void SetShelterStateForDebug()
        {
            SetState(GameState.Shelter);
        }

        [ContextMenu("Test/Set Run State")]
        private void SetRunStateForDebug()
        {
            SetState(GameState.Run);
        }
    }
}
