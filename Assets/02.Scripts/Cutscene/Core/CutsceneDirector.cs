using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ShotgunAndRun.Core;
using ShotgunAndRun.Player;
using ShotgunAndRun.UI.Dialogue;
using UnityEngine;
using PlayerComponent = ShotgunAndRun.Player.Player;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 씬의 컷씬 관리자. 씬마다 하나씩 둡니다 (CutsceneDirector 프리팹).
    /// 등록된 컷씬의 재생 조건 검사 · 자동 재생 · 대기열 · 사전 검사 · 재생 · 정리 · 완료 플래그 저장을 맡습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CutsceneDirector : MonoBehaviour
    {
        [Header("이 씬에서 재생할 컷씬")]
        [SerializeField] private List<CutsceneAsset> _cutscenes = new List<CutsceneAsset>();

        [Header("공용 설정")]
        [Tooltip("플레이어를 나타내는 인물 프로필 (대사 화자 · 말풍선 대상으로 사용)")]
        [SerializeField]
        private CharacterProfileSO _playerProfile;

        [Tooltip("컷씬이 끝날 때 카메라가 플레이어에게 돌아오는 시간(초)")]
        [SerializeField, Min(0f)]
        private float _cameraReturnDuration = 0.6f;

        [Header("연출 부품")]
        [SerializeField] private EmotionBubbleDisplay _bubbles;
        [SerializeField] private SpeechBubbleDisplay _speech;
        [SerializeField] private ScreenFader _screen;
        [SerializeField] private AudioSource _sfxSource;
        [SerializeField] private AudioSource _bgmSource;

        private readonly Queue<CutsceneAsset> _queue = new Queue<CutsceneAsset>();
        private CutsceneContext _context;
        private CutsceneHandle _returnHandle;
        private DebugRun _debug;
        private List<ICutsceneDebugHost> _debugHosts;

        // 런 중에 얻은 플래그. 탈출하면 영구 플래그로 저장하고, 사망하면 버린다.
        private readonly HashSet<string> _pendingFlags = new HashSet<string>();
        private GameManager _subscribedManager;

        private sealed class DebugRun
        {
            public CutsceneAsset Asset;
            public bool Resume;
            public bool SaveFlags;
            public List<ICutsceneDebugHost> Hosts;
        }

        /// <summary>
        /// 현재 씬의 Director
        /// </summary>
        public static CutsceneDirector Current
        {
            get;
            private set;
        }

        public static bool IsPlaying => Current != null && Current._context != null;
        public static bool IsHudHidden => IsPlaying && Current._context.Asset.HideHud;

        /// <summary>
        /// 지금 재생 중인 컷씬이 디버그 재생인지. 디버그 재생은 세이브 · 씬 이동에 영향을 주면 안 됩니다.
        /// </summary>
        public bool IsDebugRun => _context != null && _debug != null;

        public static event Action<CutsceneAsset> CutsceneStarted;

        /// <summary>
        /// 컷씬이 끝났을 때. completed는 끝까지 재생했는지 여부(예외 · 중단이면 false)입니다.
        /// </summary>
        public static event Action<CutsceneAsset, bool> CutsceneFinished;

        public IReadOnlyList<CutsceneAsset> Cutscenes => _cutscenes;
        public CutsceneAsset Playing => _context?.Asset;
        public CharacterProfileSO PlayerProfile => _playerProfile;

        internal EmotionBubbleDisplay Bubbles => _bubbles;
        public ScreenFader Screen => _screen;
        internal AudioSource SfxSource => _sfxSource;
        internal AudioSource BgmSource => _bgmSource;

        /// <summary>
        /// 컷씬 재생 여부와 상관없이 대상 머리 위에 대사 말풍선을 띄웁니다 (재시도 회차의 대체 대사 등).
        /// </summary>
        public void ShowSpeech(Transform target, string text, float seconds)
        {
            if (_speech == null)
            {
                Debug.LogError("[CutsceneDirector] Speech(SpeechBubbleDisplay)가 비어 있어 대사 말풍선을 띄울 수 없습니다.", this);
                return;
            }

            _speech.Show(target, text, seconds);
        }

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogError($"[CutsceneDirector] 씬에 CutsceneDirector가 둘 이상 있습니다. '{name}'는 사용하지 않습니다.", this);
                enabled = false;
                return;
            }

            Current = this;
        }

        private void OnEnable()
        {
            SubscribeRunEnded();
        }

        private void Start()
        {
            // OnEnable 때 GameManager가 아직 없었을 수 있다
            SubscribeRunEnded();
            StartCoroutine(AutoPlayWhenReady());
        }

        private void OnDisable()
        {
            if (_subscribedManager != null)
            {
                _subscribedManager.RunEnded -= OnRunEnded;
                _subscribedManager = null;
            }

            if (_context != null)
            {
                Finish(completed: false);
            }

            _queue.Clear();
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        // ───────────────────────────── 재생 요청 ─────────────────────────────

        /// <summary>
        /// 영구 플래그와 이번 런의 보류 플래그 기준으로 재생할 수 있는지 확인합니다.
        /// 진행 데이터(GameManager)가 없는 테스트 씬에서는 경고 후 재생 가능으로 봅니다.
        /// </summary>
        public bool CanPlay(CutsceneAsset asset)
        {
            var quests = GameManager.Instance != null ? GameManager.Instance.Progress?.Quests : null;
            if (quests == null)
            {
                Debug.LogWarning($"[CutsceneDirector] 진행 데이터(GameManager)가 없어 '{asset.Id}'의 재생 조건(플래그)을 검사하지 않습니다.",
                    this);
                return true;
            }

            bool HasFlag(string flag) => quests.HasFlag(flag) || _pendingFlags.Contains(flag);
            return asset.RequiredFlags.All(HasFlag) && !asset.BlockedFlags.Any(HasFlag);
        }

        /// <summary>
        /// 컷씬을 재생합니다. 다른 컷씬이 재생 중이면 대기열에 넣습니다.
        /// 재생 조건(플래그)은 검사하지 않으므로 필요하면 CanPlay를 먼저 확인하세요.
        /// </summary>
        public bool Play(CutsceneAsset asset)
        {
            if (asset == null)
            {
                Debug.LogError("[CutsceneDirector] 재생할 컷씬이 null입니다.", this);
                return false;
            }

            if (!_cutscenes.Contains(asset))
            {
                Debug.LogError($"[CutsceneDirector] '{asset.Id}'는 이 씬의 Director에 등록되지 않은 컷씬입니다.", this);
                return false;
            }

            if (_context != null)
            {
                if (_context.Asset != asset && !_queue.Contains(asset))
                {
                    _queue.Enqueue(asset);
                }

                return true;
            }

            return StartCutscene(asset);
        }

        public bool Play(string id)
        {
            var asset = _cutscenes.FirstOrDefault(c => c != null && c.Id == id);
            if (asset == null)
            {
                Debug.LogError($"[CutsceneDirector] 이 씬에 '{id}' 컷씬이 등록되어 있지 않습니다.", this);
                return false;
            }

            return Play(asset);
        }

        // ───────────────────────────── 진행 ─────────────────────────────

        private IEnumerator AutoPlayWhenReady()
        {
            while (PlayerComponent.PlayerInstance == null)
            {
                yield return null;
            }

            var candidate = _cutscenes
                .Where(c => c != null && c.AutoPlay)
                .OrderByDescending(c => c.Priority)
                .FirstOrDefault(CanPlay);

            if (candidate == null) yield break;

            // 플레이어가 나타나는 연출이면 시작을 기다리는 동안 그려지지 않게 지금(첫 화면 전) 숨긴다
            PlayerCutsceneActor hiddenPlayer = null;
            if (candidate.HidesPlayerBeforeStart)
            {
                hiddenPlayer = PlayerCutsceneActor.GetOrAdd(PlayerComponent.PlayerInstance);
                hiddenPlayer.SetVisible(false);
            }

            // 다른 컴포넌트의 Start(플레이어 배치 등)가 끝난 뒤 시작한다
            yield return null;

            // 재생하지 못하면 숨긴 플레이어를 되돌린다
            if (!Play(candidate) && hiddenPlayer != null)
            {
                hiddenPlayer.SetVisible(true);
            }
        }

        private bool StartCutscene(CutsceneAsset asset)
        {
            var context = CutsceneContext.TryCreate(this, asset, out var errors);
            if (context == null)
            {
                Debug.LogError($"[CutsceneDirector] '{asset.Id}' 컷씬을 재생할 수 없습니다.\n- {string.Join("\n- ", errors)}",
                    asset);
                return false;
            }

            _context = context;
            GameManager.Instance?.HoldSave(true);
            CutsceneStarted?.Invoke(asset);

            try
            {
                _context.Begin();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, asset);
                Finish(completed: false);
                return false;
            }

            return true;
        }

        private void Update()
        {
            if (_context == null) return;

            // 씬 전환 중에는 시작만 해 두고(HUD 숨김 · 입력 잠금 · 적 정지) 페이드 인이 끝난 뒤 진행한다
            if (GameManager.Instance != null && GameManager.Instance.IsSceneTransitioning) return;

            try
            {
                _context.Tick();

                if (_context.IsMainFinished && _returnHandle == null)
                {
                    _returnHandle = _context.ReturnCameraToPlayer(_cameraReturnDuration);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, _context.Asset);
                Finish(completed: false);
            }
        }

        private void LateUpdate()
        {
            if (_context == null) return;

            try
            {
                _context.LateTick();
                if (_returnHandle != null && _returnHandle.IsDone)
                {
                    Finish(completed: true);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, _context.Asset);
                Finish(completed: false);
            }
        }

        /// <summary>
        /// 재생 중인 컷씬을 중단합니다. 완료 플래그는 추가하지 않습니다.
        /// </summary>
        public void Abort()
        {
            if (_context != null)
            {
                Finish(completed: false, aborted: true);
            }
        }

        private void Finish(bool completed, bool aborted = false)
        {
            var context = _context;
            var debug = _debug;
            _context = null;
            _returnHandle = null;
            _debug = null;
            GameManager.Instance?.HoldSave(false);

            try
            {
                context.Cleanup();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, context.Asset);
            }

            if (completed)
            {
                // 디버그 재생은 옵션을 켠 경우에만 세이브에 남긴다
                if (debug == null || debug.SaveFlags)
                {
                    SaveCompletionFlags(context.Asset);
                }
            }
            else if (!aborted)
            {
                Debug.LogError($"[CutsceneDirector] '{context.Asset.Id}' 컷씬이 중간에 멈췄습니다. 완료 플래그를 추가하지 않습니다.",
                    context.Asset);
            }

            CutsceneFinished?.Invoke(context.Asset, completed);

            if (debug != null)
            {
                foreach (var host in debug.Hosts)
                {
                    if (!IsAlive(host)) continue;

                    try
                    {
                        host.OnDebugFinished(context.Asset, completed, debug.Resume);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception, this);
                    }
                }
            }

            while (_context == null && isActiveAndEnabled && _queue.Count > 0)
            {
                var next = _queue.Dequeue();
                if (CanPlay(next))
                {
                    StartCutscene(next);
                }
            }
        }

        private void SaveCompletionFlags(CutsceneAsset asset)
        {
            if (asset.CompletionFlags.Count == 0) return;

            var gameManager = GameManager.Instance;
            var quests = gameManager != null ? gameManager.Progress?.Quests : null;
            if (quests == null)
            {
                Debug.LogWarning($"[CutsceneDirector] 진행 데이터(GameManager)가 없어 '{asset.Id}'의 완료 플래그를 저장하지 못했습니다.", this);
                return;
            }

            // 런 중에는 보류하고, 즉시 저장 옵션을 켠 컷씬만 바로 저장한다
            bool immediate = asset.SaveCompletionFlagsImmediately || !IsRunInProgress(gameManager);
            foreach (string flag in asset.CompletionFlags)
            {
                if (immediate)
                {
                    quests.AddFlag(flag);
                }
                else
                {
                    _pendingFlags.Add(flag);
                }
            }

            if (immediate)
            {
                gameManager.SaveProgress();
            }
        }

        private static bool IsRunInProgress(GameManager gameManager)
        {
            return gameManager != null && gameManager.CurrentRun != null && !gameManager.CurrentRun.IsEnded;
        }

        internal bool HasPendingFlag(string flag)
        {
            return _pendingFlags.Contains(flag);
        }

        /// <summary>
        /// 컷씬 도중 추가하는 플래그. 런 중이면 보류하고, 아니면 영구 플래그에 추가합니다. (저장은 하지 않음)
        /// </summary>
        internal void AddFlag(string flag)
        {
            var gameManager = GameManager.Instance;
            var quests = gameManager != null ? gameManager.Progress?.Quests : null;
            if (quests == null) return;

            if (IsRunInProgress(gameManager))
            {
                _pendingFlags.Add(flag);
            }
            else
            {
                quests.AddFlag(flag);
            }
        }

        private void SubscribeRunEnded()
        {
            if (_subscribedManager != null || GameManager.Instance == null) return;

            _subscribedManager = GameManager.Instance;
            _subscribedManager.RunEnded += OnRunEnded;
        }

        private void OnRunEnded(RunResult result)
        {
            var gameManager = GameManager.Instance;
            var quests = gameManager != null ? gameManager.Progress?.Quests : null;

            if (result.Outcome == RunOutcome.Extracted && quests != null && _pendingFlags.Count > 0)
            {
                foreach (string flag in _pendingFlags)
                {
                    quests.AddFlag(flag);
                }

                gameManager.SaveProgress();
            }

            _pendingFlags.Clear();
        }

        /// <summary>
        /// Director 자신의 연출 부품이 모두 연결되어 있는지 확인합니다.
        /// </summary>
        internal void ValidateParts(List<string> errors)
        {
            if (_playerProfile == null) errors.Add("CutsceneDirector의 Player Profile이 비어 있습니다.");
            if (_screen == null) errors.Add("CutsceneDirector의 Screen(ScreenFader)이 비어 있습니다.");
            if (_sfxSource == null) errors.Add("CutsceneDirector의 Sfx Source가 비어 있습니다.");
            if (_bgmSource == null) errors.Add("CutsceneDirector의 Bgm Source가 비어 있습니다.");

            if (_bubbles == null)
            {
                errors.Add("CutsceneDirector의 Bubbles(EmotionBubbleDisplay)가 비어 있습니다.");
            }
            else if (_bubbles.Sprites == null)
            {
                errors.Add("EmotionBubbleDisplay의 Sprites(말풍선 세트)가 비어 있습니다.");
            }
            else
            {
                _bubbles.Sprites.CollectMissing(errors);
            }
        }

        // ───────────────────────────── 디버그 지원 ─────────────────────────────

        /// <summary>
        /// (디버그용) 컷씬 조건(플래그)을 무시하고 재생합니다. 재생 중인 컷씬과 대기열은 버리고,
        /// 씬의 진행 흐름(ICutsceneDebugHost)이 컷씬의 시작 직전 상태를 만든 뒤 재생합니다.
        /// 옵션에 따라 플레이어 텔레포트 · 완료 플래그 저장 · 끝난 뒤 흐름 이어가기를 합니다.
        /// </summary>
        public bool PlayDebug(CutsceneAsset asset, CutsceneDebugOptions options)
        {
            if (asset == null || !_cutscenes.Contains(asset))
            {
                Debug.LogError("[CutsceneDirector] 이 씬의 Director에 등록되지 않은 컷씬은 디버그 재생할 수 없습니다.", this);
                return false;
            }

            // 대기열을 먼저 비운다 (Abort가 끝나면서 대기 중인 컷씬을 시작하지 않게)
            _queue.Clear();
            Abort();

            var hosts = FindDebugHosts(refresh: true);
            if (!PrepareDebug(asset, options.Teleport, hosts))
            {
                return false;
            }

            _debug = new DebugRun
            { Asset = asset, Resume = options.ResumeAfter, SaveFlags = options.SaveCompletionFlags, Hosts = hosts };
            if (!StartCutscene(asset))
            {
                _debug = null;
                return false;
            }

            return true;
        }

        /// <summary>
        /// (디버그용) 재생은 하지 않고, 컷씬의 시작 직전 상태(진행 흐름 상태 · 위치)만 맞춥니다.
        /// </summary>
        public bool PrepareDebug(CutsceneAsset asset, bool teleport)
        {
            if (asset == null) return false;

            _queue.Clear();
            Abort();
            return PrepareDebug(asset, teleport, FindDebugHosts(refresh: true));
        }

        /// <summary>
        /// (디버그용) 씬의 진행 흐름이 알려 주는 현재 상태 요약
        /// </summary>
        public IEnumerable<string> DescribeDebugStates()
        {
            foreach (var host in FindDebugHosts(refresh: false))
            {
                if (!IsAlive(host)) continue;

                string text = host.DescribeDebugState();
                if (!string.IsNullOrEmpty(text))
                {
                    yield return text;
                }
            }
        }

        private bool PrepareDebug(CutsceneAsset asset, bool teleport, List<ICutsceneDebugHost> hosts)
        {
            // 이전 컷씬의 암전이 남아 있을 경우를 대비해 화면 덮개를 걷는다
            if (_screen != null)
            {
                _screen.SetOverlay(Color.black, 0f);
            }

            foreach (var host in hosts)
            {
                if (!IsAlive(host)) continue;

                if (!host.PrepareForDebug(asset))
                {
                    Debug.LogError($"[CutsceneDirector] '{asset.Id}'의 시작 직전 상태를 만들지 못했습니다. 콘솔의 이전 오류를 확인하세요.", this);
                    return false;
                }
            }

            if (teleport)
            {
                TeleportPlayerToCutscene(asset);
            }

            return true;
        }

        private List<ICutsceneDebugHost> FindDebugHosts(bool refresh)
        {
            if (refresh || _debugHosts == null || _debugHosts.Exists(h => !IsAlive(h)))
            {
                _debugHosts = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<ICutsceneDebugHost>()
                    .ToList();
            }

            return _debugHosts;
        }

        // 인터페이스 변수에 든 Unity 객체는 파괴돼도 == null이 아니므로 UnityEngine.Object로 확인한다
        private static bool IsAlive(ICutsceneDebugHost host)
        {
            return host is UnityEngine.Object unityObject && unityObject != null;
        }

        /// <summary>
        /// (디버그용) 컷씬 재생에 적합한 위치로 플레이어를 텔레포트하고 카메라를 동기화합니다.
        /// </summary>
        public bool TeleportPlayerToCutscene(CutsceneAsset asset)
        {
            if (asset == null || PlayerComponent.PlayerInstance == null) return false;

            var player = PlayerComponent.PlayerInstance;
            Vector3? targetPosition = FindSpawnPositionForCutscene(asset);

            if (!targetPosition.HasValue) return false;

            targetPosition += (Vector3)asset.DebugSpawnOffset;

            // 1. 플레이어 위치 이동 및 물리 속도 정지
            player.transform.position = targetPosition.Value;
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            // 2. 화면 암전 걷어내기 (이전 컷씬의 암전이 남아있을 경우 대비)
            if (_screen != null)
            {
                _screen.SetOverlay(Color.black, 0f);
            }

            // 3. 카메라를 플레이어 위치로 즉시 스냅
            var camFollow = CutsceneCamera.FindMainFollow();
            if (camFollow != null)
            {
                camFollow.SetTarget(player.transform, snapImmediately: true);
            }
            else if (Camera.main != null)
            {
                var cp = Camera.main.transform.position;
                Camera.main.transform.position = new Vector3(targetPosition.Value.x, targetPosition.Value.y, cp.z);
            }

            return true;
        }

        private Vector3? FindSpawnPositionForCutscene(CutsceneAsset asset)
        {
            string markerKey = asset.DebugSpawnMarker;

            // 1. 디버그 스폰 마커 키로 찾기 (CutsceneMarker, ICutsceneSpawnPoint, GameObject 순)
            if (!string.IsNullOrEmpty(markerKey))
            {
                var marker = FindObjectsByType<CutsceneMarker>(FindObjectsSortMode.None)
                    .FirstOrDefault(m => m != null && m.Key == markerKey);
                if (marker != null) return marker.transform.position;

                var spawnPoint = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                    .OfType<ICutsceneSpawnPoint>()
                    .FirstOrDefault(p => p.Key == markerKey);
                if (spawnPoint != null) return spawnPoint.SpawnPosition;

                var go = GameObject.Find(markerKey);
                if (go != null) return go.transform.position;
            }

            // 2. 씬의 CutsceneTrigger 중 이 에셋을 물고 있는 트리거 찾기
            var trigger = FindObjectsByType<CutsceneTrigger>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t != null && t.Cutscene == asset);
            if (trigger != null) return trigger.transform.position;

            // 3. 컷씬에 등록된 요구 마커 목록 중 첫 번째 마커 위치
            var req = asset.CollectRequirements();
            if (req.Markers.Count > 0)
            {
                foreach (string key in req.Markers)
                {
                    var marker = FindObjectsByType<CutsceneMarker>(FindObjectsSortMode.None)
                        .FirstOrDefault(m => m != null && m.Key == key);
                    if (marker != null) return marker.transform.position;
                }
            }

            return null;
        }
    }
}
