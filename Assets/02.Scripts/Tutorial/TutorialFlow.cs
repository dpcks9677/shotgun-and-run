using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using InventorySystem;
using ShotgunAndRun.Core;
using ShotgunAndRun.Player;
using ShotgunAndRun.Cutscene;
using ShotgunAndRun.Quest;
using UnityEngine;
using PlayerComponent = ShotgunAndRun.Player.Player;

namespace ShotgunAndRun.Tutorial
{
    /// <summary>
    /// (프로토타입) 튜토리얼 진행 관리자. 컷씬(탈출 → 샷건 → 적 등장 → 상자 발견 → 적 무리 → 기절 → 추격)과 그 사이의 자유 조작 · 전투 · 안내 구간을 순서대로 이어 줍니다.
    /// 대본: Docs/quests/CS_TUTORIAL.md. 씬에 하나만 두며, 씬이 시작되면 자동으로 처음부터 진행합니다.
    /// 진행 순서는 Step 목록(RunStep)에 순서대로 적혀 있고, 각 단계가 끝나는 조건과 안내 문구도 그 코드에 있습니다.
    /// 컷씬 디버거(ICutsceneDebugHost)가 특정 컷씬을 고르면 흐름을 멈추고, 그 컷씬이 시작되기 직전의 상태(StateFor)를 만들어 줍니다.
    /// Step을 추가하거나 컷씬의 사전 상태가 바뀌면 StateFor도 함께 고치세요.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TutorialFlow : MonoBehaviour, ICutsceneDebugHost
    {
        [Header("컷씬 (CutsceneDirector에 등록되어 있어야 합니다)")]
        [SerializeField] private CutsceneAsset _escape;
        [SerializeField] private CutsceneAsset _shotgunSpot;
        [SerializeField] private CutsceneAsset _shotgunGet;
        [SerializeField] private CutsceneAsset _skyIntro;
        [SerializeField] private CutsceneAsset _skyClear;
        [SerializeField] private CutsceneAsset _chestSpot;
        [SerializeField] private CutsceneAsset _chestFirst;
        [SerializeField] private CutsceneAsset _arenaIntro;
        [SerializeField] private CutsceneAsset _faintDefeat;
        [SerializeField] private CutsceneAsset _faintCleared;
        [SerializeField] private CutsceneAsset _chase;

        [Header("씬 오브젝트")]
        [SerializeField] private TutorialPopup _popup;
        [SerializeField] private TutorialPickup _shotgun;
        [SerializeField] private GameObject _octagonGate;
        [SerializeField] private LootChest _chest;
        [SerializeField] private CutsceneEnemySpawner _spawner;

        [Header("안내 문구")]
        [SerializeField] private string _shootGuide = "발사 : <sprite name=\"Mouse_L\">";
        [SerializeField] private string _reloadGuide = "장전 : <sprite name=\"Key_R\">";
        [SerializeField] private string _chestOpenGuide = "상자 열기 : <sprite name=\"Key_E\"> 길게 누르기";
        [SerializeField] private string _chestTakeGuide = "담기 : <sprite name=\"Mouse_L\"> 더블 클릭";
        [SerializeField] private string _healGuide = "회복 아이템 : <sprite name=\"Key_1\">~<sprite name=\"Key_4\"> → <sprite name=\"Mouse_L\">";
        [SerializeField] private string _pickupGuide = "줍기 : <sprite name=\"Key_E\">";
        [SerializeField] private string _arenaHint = "남은 적을 모두 처치하세요";
        [SerializeField] private string _lethalBlockedLine = "아직은... 안 돼!";

        [Header("전투 수치")]
        [SerializeField, Min(0f)] private float _healGuideHoldSeconds = 1.2f;
        [Tooltip("치명 피해를 막은 직후 주는 무적 시간(초). 체력 1에서 연타로 맞는 것을 막습니다.")]
        [SerializeField, Min(0f)] private float _lethalInvulnerableSeconds = 2.5f;
        [Tooltip("보라 전투에서 쓰러지는 순간 시간을 멈추는 시간(초, 실시간)")]
        [SerializeField, Min(0f)] private float _hitStopSeconds = 0.2f;
        [Tooltip("하늘색 전투에서 치명 피해를 처음 막았을 때 혼잣말 말풍선을 보여 주는 시간(초)")]
        [SerializeField, Min(0f)] private float _lethalLineSeconds = 2f;
        [Tooltip("하늘색 전투에서 첫 적이 사거리 안에 들어오기를 기다리는 최대 시간(초). 넘으면 그냥 발사 안내를 띄웁니다.")]
        [SerializeField, Min(0f)] private float _shootGuideMaxWait = 6f;
        [Tooltip("보라 전투에서 적을 푼 뒤 이 시간(초)이 지나도 끝나지 않으면 남은 적 수를 안내합니다.")]
        [SerializeField, Min(1f)] private float _arenaHintSeconds = 25f;

        /// <summary>
        /// 진행 단계. 위에서 아래 순서대로 이어집니다.
        /// </summary>
        private enum Step
        {
            Escape,
            ShotgunSpot,
            ShotgunPickup,
            ShotgunGet,
            SkyIntro,
            SkyFight,
            SkyClear,
            ChestSpot,
            ArenaIntro,
            ArenaFight,
            Faint,
            HealGuide,
            Chase,
        }

        /// <summary>
        /// 디버거로 단독 재생할 수 있는 컷씬(= 시작 직전 상태가 정의된 지점)
        /// </summary>
        private enum DebugStage
        {
            Escape,
            ShotgunSpot,
            ShotgunGet,
            SkyIntro,
            SkyClear,
            ChestSpot,
            ArenaIntro,
            FaintDefeat,
            FaintCleared,
            Chase,
        }

        private enum EnemyState
        {
            None,
            Held,
            Alive,
        }

        /// <summary>
        /// 컷씬이 시작되기 직전의 게임 상태
        /// </summary>
        private struct StageState
        {
            public bool Armed;
            public int Health;
            // -1이면 탄창 가득
            public int Magazine;
            public string[] Huds;
            public bool DeathBlocked;
            public bool GateOpen;
            public bool PickupVisible;
            public bool Collapsed;
            public EnemyState Sky;
            public EnemyState Arena;
        }

        private static readonly string[] NoHuds = { };
        private static readonly string[] FlightHuds = { "Health", "Dash" };
        private static readonly string[] ArmedHuds = { "Health", "Dash", "Ammo" };
        private static readonly string[] AllHuds = { "Health", "Dash", "Ammo", "QuickSlot" };

        private const string SkyGroupKey = "SkyEnemies";

        private readonly Dictionary<string, CutsceneMarker> _markers = new Dictionary<string, CutsceneMarker>();
        private readonly Dictionary<string, TutorialZone> _zones = new Dictionary<string, TutorialZone>();
        private readonly Dictionary<string, TutorialWallGuide> _guides = new Dictionary<string, TutorialWallGuide>();
        private readonly List<Action> _undo = new List<Action>();
        private readonly List<Action> _sideUndo = new List<Action>();
        private CutsceneEnemyGroup _skyEnemies;
        private CutsceneEnemyGroup _arenaEnemies;
        private CutsceneShakeDebris _debris;
        private bool _arenaDefeated;
        private bool _resolved;
        private bool _gateClosedActive;
        private PlayerComponent _player;
        private PlayerWeapon _weapon;
        private PlayerInputReader _input;

        private Coroutine _flow;
        private Coroutine _side;
        private string _guideKey;
        private string _guideMessage;
        private bool _flowRunning;
        private Step _step;
        private DebugStage? _debugStage;

        private void Start()
        {
            StartFlow(Run());
        }

        private void OnDisable()
        {
            Time.timeScale = 1f;
        }

        // ───────────────────────────── 컷씬 신호 (CutsceneSignal에서 호출) ─────────────────────────────

        /// <summary>
        /// 기절 컷씬의 암전 중에 남은 보라 방 적을 정리합니다.
        /// </summary>
        public void ClearArenaEnemies()
        {
            _arenaEnemies?.Clear();
        }

        /// <summary>
        /// 추격 컷씬이 끝날 때 호출됩니다. 화면을 검게 유지한 채 완료 플래그를 저장하고 쉘터로 넘어갑니다. (쉘터에서 프롤로그 컷씬이 이어집니다)
        /// 컷씬 디버그 재생 중에는 세이브와 씬 이동에 영향을 주지 않도록 아무것도 하지 않습니다.
        /// </summary>
        public void Complete()
        {
            var director = CutsceneDirector.Current;
            if (director != null && director.IsDebugRun)
            {
                Debug.Log("[TutorialFlow] 디버그 재생이라 튜토리얼 완료 처리와 씬 이동을 건너뜁니다.");
                return;
            }

            _popup.SetBlackout(true);

            var gameManager = GameManager.Instance;
            gameManager.Progress.Quests.AddFlag(QuestFlags.TutorialCompleted);
            gameManager.SaveProgress();
            gameManager.GoToShelter();
        }

        // ───────────────────────────── 진행 ─────────────────────────────

        // 컷씬 감독의 화면 덮개(ScreenFader)가 씬 시작부터 검게 덮여 있을 수 있다. 컷씬이 안 도는 경로에서는 걷어 검은 화면에 갇히지 않게 한다.
        private static void ClearStartBlack()
        {
            var director = CutsceneDirector.Current;
            if (director != null && director.Screen != null)
            {
                director.Screen.SetOverlay(Color.black, 0f);
            }
        }

        private void StartFlow(IEnumerator body)
        {
            StopFlow();
            _flowRunning = true;
            _flow = StartCoroutine(Tracked(body));
        }

        private IEnumerator Tracked(IEnumerator body)
        {
            yield return body;
            _flowRunning = false;
        }

        // 진행 코루틴을 멈추고, 코루틴이 걸어 둔 이벤트 구독과 시간 정지를 모두 푼다
        private void StopFlow()
        {
            if (_flow != null)
            {
                StopCoroutine(_flow);
                _flow = null;
            }

            _flowRunning = false;
            StopSideGuide();

            var undo = _undo.ToArray();
            _undo.Clear();
            foreach (var action in undo)
            {
                action();
            }

            Time.timeScale = 1f;
        }

        // 이벤트 구독을 걸고, 코루틴이 중간에 멈춰도 StopFlow가 풀 수 있게 기록해 둔다. 정상 종료 때는 Unhook으로 푼다
        // bag을 주면 그 목록에 기록한다. (보조 안내는 자기 목록을 쓴다)
        private Action Hook(Action subscribe, Action unsubscribe, List<Action> bag = null)
        {
            subscribe();
            (bag ?? _undo).Add(unsubscribe);
            return unsubscribe;
        }

        private void Unhook(Action unsubscribe, List<Action> bag = null)
        {
            if ((bag ?? _undo).Remove(unsubscribe))
            {
                unsubscribe();
            }
        }

        // ───────────────────────────── 보조 안내 ─────────────────────────────
        // 진행 단계를 막지 않고 옆에서 돌다가 조건이 되면 스스로 닫는 안내(상자). 동시에 하나만 돈다.

        private void StartSideGuide(IEnumerator body)
        {
            StopSideGuide();
            _side = StartCoroutine(SideRoutine(body));
        }

        private IEnumerator SideRoutine(IEnumerator body)
        {
            yield return body;
            _side = null;
        }

        private void StopSideGuide()
        {
            if (_side == null) return;

            StopCoroutine(_side);
            _side = null;

            var undo = _sideUndo.ToArray();
            _sideUndo.Clear();
            foreach (var action in undo)
            {
                action();
            }

            SetGuide(_guideKey, null);
        }

        // 보조 안내가 띄운 문구를 바꾼다. 같은 키 · 같은 문구면 아무것도 하지 않고, null이면 그 키의 안내를 닫는다
        private void SetGuide(string key, string message)
        {
            if (key == _guideKey && message == _guideMessage) return;

            _guideKey = key;
            _guideMessage = message;
            if (message == null)
            {
                HideGuide(key);
            }
            else
            {
                ShowGuide(key, message);
            }
        }

        // 키로 찾은 안내 표시기를 켠다. follow를 주면 그 대상의 머리 위를 따라간다
        private void ShowGuide(string key, string message, Transform follow = null)
        {
            _guides[key].Show(message, follow);
        }

        private void HideGuide(string key)
        {
            _guides[key].Hide();
        }

        private IEnumerator Run()
        {
            while (PlayerComponent.PlayerInstance == null || CutsceneDirector.Current == null)
            {
                yield return null;
            }

            // 다른 컴포넌트의 Start(플레이어 배치 · 컷씬 감독 준비)가 끝난 뒤 시작한다
            yield return null;

            if (!Resolve())
            {
                ClearStartBlack();
                yield break;
            }

            if (GameManager.Instance.Progress.Quests.HasFlag(QuestFlags.TutorialCompleted))
            {
                Debug.Log("[TutorialFlow] 이미 튜토리얼을 마친 세이브라 건너뜁니다.");
                ClearStartBlack();
                yield break;
            }

            yield return RunFrom(Step.Escape);
        }

        private IEnumerator RunFrom(Step first)
        {
            for (var step = first; step <= Step.Chase; step++)
            {
                _step = step;
                yield return RunStep(step);
            }
        }

        // 튜토리얼 진행 순서. 단계 위의 주석 색은 레벨 디자인 문서(Docs/quests/CS_TUTORIAL.md)의 구간 색입니다.
        private IEnumerator RunStep(Step step)
        {
            switch (step)
            {
                // 빨강: 탈출 컷씬
                case Step.Escape:
                    ResetZones(_zones.Keys.ToArray());
                    HudGate.ResetAll();
                    CutscenePlayerState.SetArmed(false);
                    _shotgun.SetActive(false);
                    return PlayCutscene(_escape);

                // 주황: 탈출 컷씬이 끝나면 복도 벽의 이동 · 대시 안내가 한꺼번에 나타난다 (문구는 씬 오브젝트에 적혀 있다. 자유 이동)
                // 초록: 샷건 발견 → 줍기 → 획득 컷씬
                case Step.ShotgunSpot:
                    ResetZones("OctagonEntry");
                    ShowGuide("Move", null);
                    ShowGuide("Dash", null);
                    return WaitZoneThenCutscene("OctagonEntry", _shotgunSpot);
                case Step.ShotgunPickup:
                    return WaitPickup();
                case Step.ShotgunGet:
                    return PlayCutscene(_shotgunGet);

                // 하늘색: 적 3마리가 올라오는 컷씬 → 발사 안내 → 전투 (죽지 않음, 장전 안내)
                case Step.SkyIntro:
                    return SkyIntro();
                case Step.SkyFight:
                    return SkyFight();

                // 전투가 끝나면 플레이어에게 포커스하고 소감을 말한 뒤 길을 나선다
                case Step.SkyClear:
                    _octagonGate.SetActive(false);
                    return PlayCutscene(_skyClear);

                // 남색: 복도에서 상자가 보이면 대사 → 루팅 안내
                case Step.ChestSpot:
                    return ChestSpotStep();

                // 보라: 복도를 나서기 직전에 적 무리를 보여 주는 컷씬 → 전투 → 기절 → 회복 안내
                case Step.ArenaIntro:
                    return ArenaIntro();
                case Step.ArenaFight:
                    return ArenaFight();
                case Step.Faint:
                    return PlayCutscene(_arenaDefeated ? _faintDefeat : _faintCleared);
                case Step.HealGuide:
                    return HealGuide();

                // 회색: 추격 컷씬 (끝에서 Complete 신호)
                case Step.Chase:
                    return PlayCutscene(_chase);

                default:
                    throw new ArgumentOutOfRangeException(nameof(step), step, null);
            }
        }

        private bool Resolve()
        {
            if (_resolved) return true;
            if (PlayerComponent.PlayerInstance == null) return false;

            _markers.Clear();
            _zones.Clear();
            _guides.Clear();

            _player = PlayerComponent.PlayerInstance;
            _weapon = _player.GetComponent<PlayerWeapon>();
            _input = _player.GetComponent<PlayerInputReader>();

            foreach (var marker in FindObjectsByType<CutsceneMarker>(FindObjectsSortMode.None))
            {
                _markers[marker.Key] = marker;
            }

            foreach (var zone in FindObjectsByType<TutorialZone>(FindObjectsSortMode.None))
            {
                _zones[zone.Key] = zone;
            }

            foreach (var guide in FindObjectsByType<TutorialWallGuide>(FindObjectsSortMode.None))
            {
                _guides[guide.Key] = guide;
            }

            var problems = new List<string>();
            if (_popup == null) problems.Add("Popup");
            if (_shotgun == null) problems.Add("Shotgun");
            if (_octagonGate == null) problems.Add("Octagon Gate");
            if (_chest == null) problems.Add("Chest");
            if (_spawner == null) problems.Add("Spawner");
            foreach (var cutscene in new[] { _escape, _shotgunSpot, _shotgunGet, _skyIntro, _skyClear, _chestSpot, _arenaIntro, _faintDefeat, _faintCleared, _chase })
            {
                if (cutscene == null) problems.Add("컷씬 칸");
            }

            foreach (string key in new[] { "OctagonEntry", "ChestSight", "Loot", "ArenaEntry" })
            {
                if (!_zones.ContainsKey(key)) problems.Add($"TutorialZone '{key}'");
            }

            foreach (string key in new[] { "Move", "Dash", "Pickup", "Loot", "Player" })
            {
                if (!_guides.ContainsKey(key)) problems.Add($"TutorialWallGuide '{key}'");
            }

            foreach (string key in SkyKeys.Concat(ArenaUpperKeys).Concat(ArenaLowerKeys))
            {
                if (!_markers.ContainsKey(key)) problems.Add($"마커 '{key}'");
            }

            if (_weapon == null || _input == null) problems.Add("플레이어 무기 · 입력 컴포넌트");

            if (problems.Count > 0)
            {
                Debug.LogError($"[TutorialFlow] 빠진 항목이 있어 튜토리얼을 시작할 수 없습니다: {string.Join(", ", problems)}", this);
                return false;
            }

            // 문과 잔해의 처음 모습은 어떤 단계도 건드리기 전인 지금 기록해 둔다 (디버그 점프가 되돌릴 기준)
            _gateClosedActive = _octagonGate.activeSelf;
            _debris = FindFirstObjectByType<CutsceneShakeDebris>();
            _resolved = true;
            return true;
        }

        // 적 배치 마커 (위쪽: 일반 3 + 돌진 2, 아래쪽: 일반 2 + 돌진 1)
        private static readonly string[] SkyKeys = { "SkyEnemy1", "SkyEnemy2", "SkyEnemy3" };
        private static readonly string[] ArenaUpperKeys = { "ArenaUpper1", "ArenaUpper2", "ArenaUpper3", "ArenaUpper4", "ArenaUpper5" };
        private static readonly string[] ArenaLowerKeys = { "ArenaLower1", "ArenaLower2", "ArenaLower3" };

        // ───────────────────────────── 컷씬 · 구역 · 안내 ─────────────────────────────

        private IEnumerator PlayCutscene(CutsceneAsset cutscene)
        {
            var director = CutsceneDirector.Current;
            bool finished = false;
            bool completed = false;

            void OnFinished(CutsceneAsset asset, bool ok)
            {
                if (asset != cutscene) return;
                finished = true;
                completed = ok;
            }

            Action<CutsceneAsset, bool> handler = OnFinished;
            var unhook = Hook(() => CutsceneDirector.CutsceneFinished += handler, () => CutsceneDirector.CutsceneFinished -= handler);
            bool started = director.Play(cutscene);
            if (!started)
            {
                Unhook(unhook);
                Debug.LogError($"[TutorialFlow] '{cutscene.Id}' 컷씬을 재생하지 못했습니다. 콘솔의 CutsceneDirector 오류를 확인하세요.", this);
                yield break;
            }

            while (!finished)
            {
                yield return null;
            }

            Unhook(unhook);
            if (!completed)
            {
                Debug.LogError($"[TutorialFlow] '{cutscene.Id}' 컷씬이 끝까지 재생되지 못했습니다.", this);
            }
        }

        private void ResetZones(params string[] keys)
        {
            foreach (string key in keys)
            {
                if (_zones.TryGetValue(key, out var zone))
                {
                    zone.ResetEntered();
                }
            }
        }

        // 구역에 한 번이라도 들어왔으면 통과한다. 지금 안에 있는지만 보면 빨리 지나칠 때 놓친다
        private IEnumerator WaitZone(string key)
        {
            var zone = _zones[key];
            while (!zone.HasEntered)
            {
                yield return null;
            }
        }

        private IEnumerator WaitZoneThenCutscene(string zoneKey, CutsceneAsset cutscene)
        {
            yield return WaitZone(zoneKey);

            // 컷씬이 시작되면 진행을 막지 않던 안내(상자)는 닫는다
            StopSideGuide();
            yield return PlayCutscene(cutscene);
        }

        private IEnumerator WaitPickup()
        {
            bool picked = false;
            void OnInteracted() => picked = true;

            Action handler = OnInteracted;
            var unhook = Hook(() => _shotgun.Interacted += handler, () => _shotgun.Interacted -= handler);
            _shotgun.SetActive(true);
            ShowGuide("Pickup", _pickupGuide);
            while (!picked)
            {
                yield return null;
            }

            Unhook(unhook);
            HideGuide("Pickup");
        }

        // ───────────────────────────── 하늘색: 적 등장 · 전투 튜토리얼 ─────────────────────────────

        private IEnumerator SkyIntro()
        {
            CutscenePlayerState.SetDeathBlocked(true);

            // 적 3마리는 복도 아래쪽에 세워 두면(컷씬이 걸어 올려 보냄), 컷씬이 끝난 뒤 곧바로 플레이어를 쫓기 시작한다
            SpawnSkyEnemies();
            yield return PlayCutscene(_skyIntro);
        }

        private void SpawnSkyEnemies()
        {
            _skyEnemies = SpawnEnemies(EnemyType.Normal, hold: true, EnemyStateMode.Destruction, SkyKeys);
            _spawner.Register(SkyGroupKey, _skyEnemies);
        }

        private IEnumerator SkyFight()
        {
            _skyEnemies?.Release();

            // 컷씬 마지막 대사("이걸 누르면 되는건가?") 뒤, 첫 적이 사거리 안에 들어오면 시간을 멈추고 발사하게 한다
            yield return WaitEnemyInRange(_skyEnemies);
            yield return ShootGuide();

            // 탄창이 늘어난 적이 한 번이라도 있으면(장전을 1발이라도 했으면) 장전을 마친 것으로 본다
            bool reloaded = false;
            bool reloadShown = false;
            int lastMagazine = _weapon.CurrentMagazine;

            void OnAmmoChanged(int magazine)
            {
                if (magazine > lastMagazine)
                {
                    reloaded = true;
                }

                lastMagazine = magazine;
            }

            Action<int> handler = OnAmmoChanged;
            var unhook = Hook(() => _weapon.AmmoChanged += handler, () => _weapon.AmmoChanged -= handler);

            // 처음 버틴 순간에만 혼잣말을 한다
            bool spoke = false;
            void OnLethalBlocked()
            {
                ExtendInvulnerability();
                if (spoke) return;

                spoke = true;
                CutsceneDirector.Current.ShowSpeech(_player.transform, _lethalBlockedLine, _lethalLineSeconds);
            }

            Action lethalHandler = OnLethalBlocked;
            var unhookLethal = Hook(() => _player.Health.LethalDamageBlocked += lethalHandler, () => _player.Health.LethalDamageBlocked -= lethalHandler);

            // 3마리를 모두 처치하고 장전을 해야 끝난다
            while (!_skyEnemies.IsCleared || !reloaded)
            {
                // 탄창이 비거나 적을 다 잡았는데 아직 장전하지 않았다면 장전을 안내한다
                bool needReload = _weapon.CurrentMagazine == 0 || _skyEnemies.IsCleared;
                if (!reloaded && !reloadShown && needReload && _weapon.CurrentMagazine < _weapon.Stats.MagazineSize)
                {
                    reloadShown = true;
                    ShowGuide("Player", _reloadGuide, _player.transform);
                }

                if (reloaded && reloadShown)
                {
                    reloadShown = false;
                    HideGuide("Player");
                }

                yield return null;
            }

            Unhook(unhook);
            Unhook(unhookLethal);
            HideGuide("Player");
        }

        // 치명 피해를 막은 직후 체력 1로 연타를 맞지 않도록 무적 시간을 늘린다
        private void ExtendInvulnerability()
        {
            _player.Invulnerability.ActivateTimed(InvulnerabilityReason.Hit, _lethalInvulnerableSeconds);
        }

        // 가장 가까운 적이 샷건 사거리 안에 들어올 때까지 기다린다. 이미 안이거나 오래 걸리면 바로 넘어간다
        private IEnumerator WaitEnemyInRange(CutsceneEnemyGroup group)
        {
            float range = _weapon.Stats != null ? _weapon.Stats.MaxRange : 0f;
            if (group == null || range <= 0f) yield break;

            float giveUpAt = Time.time + _shootGuideMaxWait;
            while (group.NearestAliveDistance(_player.transform.position) > range && Time.time < giveUpAt)
            {
                yield return null;
            }
        }

        // 시간을 멈추고 안내를 띄운 뒤, 플레이어가 사격하면 시간을 다시 흐르게 한다
        private IEnumerator ShootGuide()
        {
            bool fired = false;
            void OnFire() => fired = true;

            Action handler = OnFire;
            var unhook = Hook(() => _input.FirePressed += handler, () => _input.FirePressed -= handler);
            Time.timeScale = 0f;
            ShowGuide("Player", _shootGuide, _player.transform);
            while (!fired)
            {
                yield return null;
            }

            Unhook(unhook);

            // 사격 처리가 끝난 다음 프레임에 시간을 되돌린다
            yield return null;
            Time.timeScale = 1f;
            HideGuide("Player");
        }

        // ───────────────────────────── 남색: 루팅 안내 ─────────────────────────────

        // 상자가 보이는 위치에서 컷씬을 재생한 뒤, 상자 안내는 보조 안내로 돌린다. (상자를 열기 전에는 ArenaIntro가 아레나 입구를 막는다)
        private IEnumerator ChestSpotStep()
        {
            // 보라 방의 적은 플레이어가 들어서기 전에 미리 배치해 둔다 (제자리에 세워 둠)
            if (_arenaEnemies == null)
            {
                SpawnArenaEnemies();
            }

            ResetZones("ChestSight", "Loot", "ArenaEntry");
            yield return WaitZoneThenCutscene("ChestSight", _chestSpot);
            StartSideGuide(LootGuide());
        }

        // 상자 앞 구역 안에 있을 때만 안내한다. 상자를 열고 비우면 끝난다
        private IEnumerator LootGuide()
        {
            var zone = _zones["Loot"];
            int openedFrames = 0;
            while (true)
            {
                bool opened = _chest.IsOpened;
                if (opened) openedFrames++;

                // 상자 내용이 생성된 다음 프레임부터 비었는지 확인한다
                if (opened && openedFrames > 1 && _chest.ChestInventory.IsEmpty()) break;

                SetGuide("Loot", zone.PlayerInside ? (opened ? _chestTakeGuide : _chestOpenGuide) : null);
                yield return null;
            }

            SetGuide("Loot", null);
        }

        // ───────────────────────────── 보라: 적 무리 컷씬 · 전투 · 회복 안내 ─────────────────────────────

        private void SpawnArenaEnemies()
        {
            // 위쪽 일반 3 + 돌진 2, 아래쪽 일반 2 + 돌진 1. 컷씬이 끝날 때까지는 제자리에 세워 두고, 이후에는 정찰 상태로 풀어 준다
            _arenaEnemies = SpawnEnemies(EnemyType.Normal, hold: true, EnemyStateMode.Standard, ArenaUpperKeys[0], ArenaUpperKeys[1], ArenaUpperKeys[2]);
            AddAll(_arenaEnemies, SpawnEnemies(EnemyType.Straight, hold: true, EnemyStateMode.Standard, ArenaUpperKeys[3], ArenaUpperKeys[4]));
            AddAll(_arenaEnemies, SpawnEnemies(EnemyType.Normal, hold: true, EnemyStateMode.Standard, ArenaLowerKeys[0], ArenaLowerKeys[1]));
            AddAll(_arenaEnemies, SpawnEnemies(EnemyType.Straight, hold: true, EnemyStateMode.Standard, ArenaLowerKeys[2]));
        }

        private IEnumerator ArenaIntro()
        {
            yield return WaitArenaEntry();

            // 상자 안내는 여기서 끝낸다
            StopSideGuide();
            CutscenePlayerState.SetDeathBlocked(true);
            yield return PlayCutscene(_arenaIntro);
        }

        // 상자를 열기 전에 아레나 입구에 닿으면 상자부터 열자는 컷씬을 재생해 뒤로 물러나게 한다
        private IEnumerator WaitArenaEntry()
        {
            var zone = _zones["ArenaEntry"];
            while (true)
            {
                yield return WaitZone("ArenaEntry");
                if (_chest.IsOpened) yield break;

                yield return PlayCutscene(_chestFirst);

                // 구역을 벗어난 뒤에 기록을 지워야 같은 접촉을 두 번 세지 않는다
                while (zone.PlayerInside)
                {
                    yield return null;
                }

                zone.ResetEntered();
            }
        }

        private IEnumerator ArenaFight()
        {
            _arenaEnemies?.Release();

            // 적을 모두 처치하거나, 체력이 바닥나는 치명 피해를 받으면(사망은 막혀 체력 1로 남음) 끝난다
            bool lethal = false;
            void OnLethal()
            {
                lethal = true;
                ExtendInvulnerability();
            }

            Action handler = OnLethal;
            var unhook = Hook(() => _player.Health.LethalDamageBlocked += handler, () => _player.Health.LethalDamageBlocked -= handler);
            float hintAt = Time.time + _arenaHintSeconds;
            int shownCount = -1;
            while (_arenaEnemies != null && !_arenaEnemies.IsCleared && !lethal)
            {
                // 적이 정찰만 하고 못 찾으면 끝나지 않으므로, 오래 걸리면 남은 적 수를 알려 준다
                if (Time.time >= hintAt)
                {
                    int alive = _arenaEnemies.AliveCount;
                    if (alive != shownCount)
                    {
                        shownCount = alive;
                        ShowGuide("Player", $"{_arenaHint} ({alive}마리)", _player.transform);
                    }
                }

                yield return null;
            }

            Unhook(unhook);
            if (shownCount >= 0)
            {
                HideGuide("Player");
            }

            // 패배와 모두 처치는 기절 컷씬의 대사가 다르다
            _arenaDefeated = lethal && _arenaEnemies != null && !_arenaEnemies.IsCleared;

            // 쓰러지는 순간 잠깐 멈춰 충격을 준다
            if (_arenaDefeated)
            {
                Time.timeScale = 0f;
                yield return new WaitForSecondsRealtime(_hitStopSeconds);
                Time.timeScale = 1f;
            }
        }

        private IEnumerator HealGuide()
        {
            bool healed = false;
            void OnHealth(int current, int max)
            {
                if (current > 1) healed = true;
            }

            Action<int, int> handler = OnHealth;
            var unhook = Hook(() => _player.Health.HealthChanged += handler, () => _player.Health.HealthChanged -= handler);

            // 회복 외 조작은 막는다. 좌클릭이 소모품 사용을 겸하므로 입력 전체가 아니라 이동 · 대시 · 무기만 잠근다
            var unlock = Hook(() => SetNonHealControls(false), () => SetNonHealControls(true));
            ShowGuide("Player", _healGuide, _player.transform);
            while (!healed)
            {
                yield return null;
            }

            Unhook(unhook);
            yield return new WaitForSeconds(_healGuideHoldSeconds);
            Unhook(unlock);
            HideGuide("Player");
        }

        private void SetNonHealControls(bool enabled)
        {
            _player.Movement.SetMovementEnabled(enabled);
            _player.Dash.SetDashEnabled(enabled);
            CutscenePlayerState.SetWeaponHidden(!enabled);
        }

        // ───────────────────────────── 적 소환 ─────────────────────────────

        private CutsceneEnemyGroup SpawnEnemies(EnemyType type, bool hold, EnemyStateMode mode, params string[] markerKeys)
        {
            var group = new CutsceneEnemyGroup();
            foreach (string key in markerKeys)
            {
                var enemy = _spawner.Spawn(type, _markers[key].Position, mode, hold);
                if (enemy != null)
                {
                    group.Add(enemy);
                }
            }

            return group;
        }

        private static void AddAll(CutsceneEnemyGroup target, CutsceneEnemyGroup source)
        {
            foreach (var enemy in source.Enemies)
            {
                target.Add(enemy);
            }
        }

        private void ClearAllEnemies()
        {
            _skyEnemies?.Clear();
            _skyEnemies = null;
            _spawner.Unregister(SkyGroupKey);

            _arenaEnemies?.Clear();
            _arenaEnemies = null;
            _arenaDefeated = false;
        }

        // ───────────────────────────── 컷씬 디버그 (ICutsceneDebugHost) ─────────────────────────────

        private bool TryGetStage(CutsceneAsset asset, out DebugStage stage)
        {
            stage = default;
            if (asset == null) return false;

            if (asset == _escape) stage = DebugStage.Escape;
            else if (asset == _shotgunSpot) stage = DebugStage.ShotgunSpot;
            else if (asset == _shotgunGet) stage = DebugStage.ShotgunGet;
            else if (asset == _skyIntro) stage = DebugStage.SkyIntro;
            else if (asset == _skyClear) stage = DebugStage.SkyClear;
            else if (asset == _chestSpot) stage = DebugStage.ChestSpot;
            else if (asset == _arenaIntro) stage = DebugStage.ArenaIntro;
            else if (asset == _faintDefeat) stage = DebugStage.FaintDefeat;
            else if (asset == _faintCleared) stage = DebugStage.FaintCleared;
            else if (asset == _chase) stage = DebugStage.Chase;
            else return false;

            return true;
        }

        // 컷씬이 끝난 뒤 이어서 진행할 단계. Chase는 마지막이라 없다
        private static Step? NextStep(DebugStage stage)
        {
            switch (stage)
            {
                case DebugStage.Escape: return Step.ShotgunSpot;
                case DebugStage.ShotgunSpot: return Step.ShotgunPickup;
                case DebugStage.ShotgunGet: return Step.SkyIntro;
                case DebugStage.SkyIntro: return Step.SkyFight;
                case DebugStage.SkyClear: return Step.ChestSpot;
                case DebugStage.ChestSpot: return Step.ArenaIntro;
                case DebugStage.ArenaIntro: return Step.ArenaFight;
                case DebugStage.FaintDefeat:
                case DebugStage.FaintCleared: return Step.HealGuide;
                case DebugStage.Chase: return null;
                default: throw new ArgumentOutOfRangeException(nameof(stage), stage, null);
            }
        }

        // 각 컷씬이 시작되기 직전의 상태. RunStep의 순서를 따라 적습니다.
        private static StageState StateFor(DebugStage stage)
        {
            switch (stage)
            {
                // 컷씬이 체력 · 소모품 · 위치를 직접 맞춘다
                case DebugStage.Escape:
                    return new StageState { Armed = false, Health = 3, Magazine = 0, Huds = NoHuds, PickupVisible = true };

                case DebugStage.ShotgunSpot:
                    return new StageState { Armed = false, Health = 3, Magazine = 0, Huds = FlightHuds, PickupVisible = true, Collapsed = true };

                // 줍는 순간. 컷씬이 샷건을 숨기고 무기를 든 상태로 바꾼다
                case DebugStage.ShotgunGet:
                    return new StageState { Armed = false, Health = 3, Magazine = 0, Huds = FlightHuds, PickupVisible = true, Collapsed = true };

                case DebugStage.SkyIntro:
                    return new StageState { Armed = true, Health = 3, Magazine = 3, Huds = ArmedHuds, DeathBlocked = true, Collapsed = true, Sky = EnemyState.Held };

                case DebugStage.SkyClear:
                    return new StageState { Armed = true, Health = 3, Magazine = -1, Huds = ArmedHuds, DeathBlocked = true, GateOpen = true, Collapsed = true };

                case DebugStage.ChestSpot:
                    return new StageState { Armed = true, Health = 3, Magazine = -1, Huds = ArmedHuds, DeathBlocked = true, GateOpen = true, Collapsed = true, Arena = EnemyState.Held };

                case DebugStage.ArenaIntro:
                    return new StageState { Armed = true, Health = 3, Magazine = -1, Huds = ArmedHuds, DeathBlocked = true, GateOpen = true, Collapsed = true, Arena = EnemyState.Held };

                // 체력이 바닥나 쓰러지는 경우: 적이 남아 있고 체력 1
                case DebugStage.FaintDefeat:
                    return new StageState { Armed = true, Health = 1, Magazine = -1, Huds = ArmedHuds, DeathBlocked = true, GateOpen = true, Collapsed = true, Arena = EnemyState.Alive };

                // 모두 처치한 경우: 적이 없다
                case DebugStage.FaintCleared:
                    return new StageState { Armed = true, Health = 3, Magazine = -1, Huds = ArmedHuds, DeathBlocked = true, GateOpen = true, Collapsed = true };

                // 깨어나 구급상자로 회복을 마친 뒤 (퀵슬롯 HUD가 열려 있고 소모품은 쓰고 없다)
                case DebugStage.Chase:
                    return new StageState { Armed = true, Health = 2, Magazine = -1, Huds = AllHuds, DeathBlocked = true, GateOpen = true, Collapsed = true };

                default:
                    throw new ArgumentOutOfRangeException(nameof(stage), stage, null);
            }
        }

        private void ApplyStageState(DebugStage stage)
        {
            var state = StateFor(stage);

            // 공통 초기화: 시간 · 안내 · 암전 · 구역 기록 · 이전 단계의 적
            Time.timeScale = 1f;
            ResetZones(_zones.Keys.ToArray());
            foreach (var guide in _guides.Values)
            {
                guide.Hide();
            }

            // 이동 · 대시 안내는 탈출 컷씬이 끝난 뒤부터 보인다
            if (stage != DebugStage.Escape)
            {
                ShowGuide("Move", null);
                ShowGuide("Dash", null);
            }

            _guideKey = null;
            _guideMessage = null;
            _popup.SetBlackout(false);
            ClearAllEnemies();

            // 플레이어
            CutscenePlayerState.SetArmed(state.Armed);
            CutscenePlayerState.ClearConsumables();
            CutscenePlayerState.SetDeathBlocked(state.DeathBlocked);
            CutscenePlayerState.SetHealth(state.Health);
            int magazineSize = _weapon.Stats != null ? _weapon.Stats.MagazineSize : 0;
            CutscenePlayerState.SetMagazine(state.Magazine < 0 ? magazineSize : state.Magazine);

            // HUD
            HudGate.ResetAll();
            foreach (string key in state.Huds)
            {
                HudGate.Set(key, true);
            }

            // 씬 오브젝트
            _octagonGate.SetActive(state.GateOpen ? !_gateClosedActive : _gateClosedActive);
            _shotgun.gameObject.SetActive(state.PickupVisible);
            _shotgun.SetActive(false);
            if (_debris != null)
            {
                _debris.SetCollapsed(state.Collapsed);
            }

            // 적
            if (state.Sky != EnemyState.None)
            {
                SpawnSkyEnemies();
                if (state.Sky == EnemyState.Alive) _skyEnemies.Release();
            }

            if (state.Arena != EnemyState.None)
            {
                SpawnArenaEnemies();
                if (state.Arena == EnemyState.Alive) _arenaEnemies.Release();
            }

            _arenaDefeated = stage == DebugStage.FaintDefeat;
        }

        bool ICutsceneDebugHost.PrepareForDebug(CutsceneAsset asset)
        {
            if (!TryGetStage(asset, out var stage)) return true;

            if (!Resolve()) return false;

            StopFlow();
            _debugStage = stage;
            ApplyStageState(stage);
            return true;
        }

        void ICutsceneDebugHost.OnDebugFinished(CutsceneAsset asset, bool completed, bool resume)
        {
            if (!TryGetStage(asset, out var stage)) return;

            if (!completed || !resume) return;

            var next = NextStep(stage);
            if (next.HasValue)
            {
                _debugStage = null;
                StartFlow(RunFrom(next.Value));

                // 상자 컷씬 뒤에는 상자 안내가 옆에서 돌고 있어야 한다
                if (stage == DebugStage.ChestSpot)
                {
                    StartSideGuide(LootGuide());
                }
            }
        }

        string ICutsceneDebugHost.DescribeDebugState()
        {
            if (!_resolved) return "튜토리얼: 아직 시작하지 않았습니다.";

            string flow = _flowRunning
                ? $"진행 중: {_step}"
                : _debugStage.HasValue ? $"흐름 멈춤 (디버그: {_debugStage.Value})" : "흐름 끝남";

            string player = _player != null
                ? $"체력 {_player.Health.CurrentHealth}/{_player.Health.MaxHealth} · 탄창 {_weapon.CurrentMagazine}"
                : "플레이어 없음";

            int sky = _skyEnemies != null ? _skyEnemies.AliveCount : 0;
            int arena = _arenaEnemies != null ? _arenaEnemies.AliveCount : 0;
            if (_side != null) flow += " · 보조 안내";
            return $"튜토리얼 {flow}\n{player} · 하늘색 적 {sky} · 보라 방 적 {arena} · timeScale {Time.timeScale:0.##}";
        }
    }
}
