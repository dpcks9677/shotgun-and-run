using System.Collections;
using System.Collections.Generic;
using ShotgunAndRun.Core;
using ShotgunAndRun.NPC;
using ShotgunAndRun.Quest;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Events;
using UnityEngine.Playables;
using PlayerComponent = ShotgunAndRun.Player.Player;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// H2 홀의 Q2 "마법사 구출" 씬 진행. 결박된 엘레나 1차 상호작용(E) → 매복 웨이브 → 소탕 후 다시 E로 결박 해제 → 엘레나 퇴장.
    /// 퀘스트 미수주 · 이미 구출 완료 상태면 엘레나 오브젝트째 꺼 둡니다. 컷씬을 볼 수 없는 재시도 회차는 말풍선 한 줄로 대체합니다.
    /// 컷씬 안의 CutsceneSignal에서 OnAmbushReveal / OnElenaCastFx / OnElenaGone을 호출하도록 연결합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ElenaRescueEvent : MonoBehaviour
    {
        private enum State
        {
            Inactive,
            Bound,
            AmbushCutscene,
            Fighting,
            FreedReady,
            Freeing,
            Done,
        }

        [Header("컷씬")]
        [Tooltip("1차 상호작용 때 재생할 매복 컷씬 (CS_ELENA_AMBUSH)")]
        [SerializeField] private CutsceneAsset _ambushCutscene;

        [Tooltip("결박 해제 후 재생할 전이 컷씬 (CS_ELENA_TELEPORT)")]
        [SerializeField] private CutsceneAsset _teleportCutscene;

        [Header("씬 참조")]
        [Tooltip("매복 웨이브 스포너. 시작 전에는 꺼 둔 오브젝트로 두세요 (이벤트가 켜서 시작합니다)")]
        [SerializeField] private EnemyDestructionSpawner _spawner;

        [Tooltip("엘레나 오브젝트 (말풍선 대상 · 퇴장 시 비활성화). 이 컴포넌트가 엘레나 자신에 있으면 비워도 됩니다")]
        [SerializeField] private Transform _elena;

        [Tooltip("엘레나의 NpcInteractable (1차 상호작용 계기). 비우면 엘레나에서 찾습니다")]
        [SerializeField] private NpcInteractable _interactable;

        [SerializeField] private SpriteRenderer _elenaRenderer;

        [Tooltip("엘레나 Animator. 결박 중에는 결박 클립을 덮어 재생하고, 해제하면 원래 애니메이션으로 돌아갑니다. 없으면 무시")]
        [SerializeField] private Animator _elenaAnimator;

        [Tooltip("결박 중 반복 재생할 쓰러져 숨 쉬는 클립 (Elena_Kidnapped). 비우면 바꾸지 않습니다")]
        [SerializeField] private AnimationClip _boundClip;

        [Tooltip("시전 상태 스프라이트 (Elena_Cast). 비우면 바꾸지 않습니다")]
        [SerializeField] private Sprite _castSprite;

        [Tooltip("마법진 · 퇴장 FX 오브젝트 (평소 꺼 둠, 시전 시점에 켭니다)")]
        [SerializeField] private GameObject _castFx;

        [Header("조건")]
        [SerializeField] private string _questId = "SQ_MAIN_002";
        [SerializeField] private string _acceptedFlag = QuestFlags.Q2Accepted;
        [SerializeField] private string _rescuedFlag = QuestFlags.ElenaRescued;
        [SerializeField] private string _runFlag = QuestFlags.H2ElenaFreed;

        [Header("대사 · 시간")]
        [SerializeField] private string _ambushSpeech = "함정이에요! 조심해요!";
        [SerializeField, Min(0.1f)] private float _ambushSpeechSeconds = 2.5f;
        [SerializeField] private string _teleportSpeech = "고마워요! 어서 탈출하세요!";
        [SerializeField, Min(0.1f)] private float _teleportSpeechSeconds = 2.5f;

        [Header("목표")]
        [SerializeField] private string _escapeObjective = "탈출 구역으로 이동";

        [Header("이벤트")]
        [Tooltip("퀘스트 목표 문구가 바뀔 때 (목표 HUD가 아직 없어 문구만 알려 줍니다)")]
        [SerializeField] private UnityEvent<string> _onObjectiveChanged = new UnityEvent<string>();

        private State _state;
        private bool _spawnerRequested;
        private bool _ambushStarting;
        private bool _waveEnded;
        private bool _elenaGone;
        private PlayableGraph _boundGraph;
        private readonly List<global::Enemy> _spawnedEnemies = new List<global::Enemy>();

        public UnityEvent<string> OnObjectiveChanged => _onObjectiveChanged;

        private Transform Elena => _elena != null ? _elena : transform;

        private void Awake()
        {
            if (_interactable == null)
            {
                _interactable = Elena.GetComponent<NpcInteractable>();
            }
        }

        private void Start()
        {
            if (!ShouldBeActive())
            {
                _state = State.Inactive;
                Elena.gameObject.SetActive(false);
                if (_spawner != null) _spawner.gameObject.SetActive(false);
                return;
            }

            _state = State.Bound;
            ApplyBoundLook();
            if (_castFx != null) _castFx.SetActive(false);

            if (_interactable != null)
            {
                _interactable.OnInteract.AddListener(HandleInteract);
            }
        }

        private void OnDestroy()
        {
            if (_interactable != null) _interactable.OnInteract.RemoveListener(HandleInteract);
            if (_spawner != null)
            {
                _spawner.CompletedEvent -= HandleWaveCompleted;
                _spawner.EnemySpawned -= HandleEnemySpawned;
            }

            CutsceneDirector.CutsceneFinished -= HandleCutsceneFinished;
            StopBoundClip();
        }

        // 수주 중(영구 플래그 + 수주 목록)이고 아직 구출 전일 때만 켠다. 진행 데이터가 없는 테스트 씬은 켠다.
        private bool ShouldBeActive()
        {
            var quests = GameManager.Instance != null ? GameManager.Instance.Progress?.Quests : null;
            if (quests == null)
            {
                Debug.LogWarning("[ElenaRescueEvent] 진행 데이터(GameManager)가 없어 퀘스트 조건을 검사하지 않습니다.", this);
                return true;
            }

            return quests.HasFlag(_acceptedFlag) && quests.IsAccepted(_questId) && !quests.HasFlag(_rescuedFlag);
        }

        // ───────────────────────────── 1. 매복 ─────────────────────────────

        private void HandleInteract()
        {
            if (CutsceneDirector.IsPlaying) return;

            // 웨이브를 정리한 뒤 다시 말을 걸면 바로 결박을 푼다
            if (_state == State.FreedReady)
            {
                CompleteFree();
                return;
            }

            if (_state != State.Bound || _ambushStarting) return;

            _ambushStarting = true;
            SetInteractable(false);
            StartCoroutine(AmbushRoutine());
        }

        private IEnumerator AmbushRoutine()
        {
            // 컷씬의 적 정지가 Start 전의 스포너를 꺼 버리지 않도록, 컷씬 전에 스포너를 켜서 Start를 끝내 둔다
            if (_spawner != null)
            {
                _spawner.gameObject.SetActive(true);
                yield return null;
            }

            _ambushStarting = false;

            var director = CutsceneDirector.Current;
            if (director != null && _ambushCutscene != null && director.CanPlay(_ambushCutscene))
            {
                // Play 안에서 Signal("AmbushReveal")이 동기로 나올 수 있어 상태 · 구독을 먼저 정한다
                _state = State.AmbushCutscene;
                CutsceneDirector.CutsceneFinished += HandleCutsceneFinished;
                if (director.Play(_ambushCutscene)) yield break;

                CutsceneDirector.CutsceneFinished -= HandleCutsceneFinished;
                _state = State.Bound;
            }

            // 이미 본 컷씬(재시도 런) 등: 말풍선 한 줄로 대체하고 조작은 잠그지 않는다
            if (director != null)
            {
                director.ShowSpeech(Elena, _ambushSpeech, _ambushSpeechSeconds);
            }

            BeginFight();
        }

        /// <summary>
        /// 매복 컷씬의 Signal("AmbushReveal")에서 호출. 스포너를 켜서 1웨이브 등장을 시작합니다.
        /// 스포너에 "미리 소환만 하고 대기" 기능이 없어, 등장하는 적은 컷씬의 적 정지로 가만히 서 있게 됩니다.
        /// </summary>
        public void OnAmbushReveal()
        {
            if (_state != State.AmbushCutscene) return;
            StartSpawner();
        }

        private void HandleCutsceneFinished(CutsceneAsset asset, bool completed)
        {
            if (asset == _ambushCutscene && _state == State.AmbushCutscene)
            {
                CutsceneDirector.CutsceneFinished -= HandleCutsceneFinished;

                if (completed)
                {
                    BeginFight();
                }
                else
                {
                    // 중단된 컷씬: 다시 1차 상호작용부터 (이미 켠 스포너는 그대로 진행)
                    if (_spawnerRequested)
                    {
                        BeginFight();
                    }
                    else { _state = State.Bound; SetInteractable(true); }
                }
            }
            else if (asset == _teleportCutscene && _state == State.Freeing)
            {
                CutsceneDirector.CutsceneFinished -= HandleCutsceneFinished;
                // 컷씬에 Signal이 빠졌어도 엘레나는 반드시 사라지게 한다
                OnElenaGone();
                FinishRescue();
            }
        }

        private void BeginFight()
        {
            _state = State.Fighting;
            SetInteractable(false);
            StartSpawner();
            TryEnterFreedReady();
        }

        // ───────────────────────────── 2. 웨이브 ─────────────────────────────

        // EnemyDestructionSpawner는 플레이어가 트리거에 들어와야만 시작한다(공개 시작 메서드 없음).
        // 스포너 오브젝트를 켜고, 한 프레임 뒤 플레이어 콜라이더로 OnTriggerEnter2D를 직접 보낸다.
        // 이미 시작된 스포너는 _isWaveStarted로 중복 시작을 막는다.
        private void StartSpawner()
        {
            if (_spawnerRequested) return;
            _spawnerRequested = true;

            if (_spawner == null)
            {
                Debug.LogError("[ElenaRescueEvent] 스포너가 비어 있어 매복 웨이브를 시작할 수 없습니다. 전투를 건너뜁니다.", this);
                _waveEnded = true;
                return;
            }

            _spawner.CompletedEvent += HandleWaveCompleted;
            _spawner.EnemySpawned += HandleEnemySpawned;
            _spawner.gameObject.SetActive(true);
            StartCoroutine(KickSpawner());
        }

        private IEnumerator KickSpawner()
        {
            // 스포너의 Start(플레이어 참조 확보)가 끝난 뒤에 보낸다
            yield return null;

            var player = PlayerComponent.PlayerInstance;
            var collider = player != null ? player.GetComponentInChildren<Collider2D>() : null;
            if (collider == null || _spawner == null)
            {
                // 웨이브를 못 시작하면 전투를 건너뛰어 퀘스트가 멈추지 않게 한다
                Debug.LogError("[ElenaRescueEvent] 플레이어/콜라이더/스포너가 없어 매복 웨이브를 시작할 수 없습니다. 전투를 건너뜁니다.", this);
                if (_spawner != null) _spawner.CompletedEvent -= HandleWaveCompleted;
                _waveEnded = true;
                TryEnterFreedReady();
                yield break;
            }

            _spawner.SendMessage("OnTriggerEnter2D", collider, SendMessageOptions.DontRequireReceiver);
        }

        private void HandleWaveCompleted()
        {
            _spawner.CompletedEvent -= HandleWaveCompleted;
            _waveEnded = true;
            TryEnterFreedReady();
        }

        private void HandleEnemySpawned(global::Enemy enemy)
        {
            _spawnedEnemies.Add(enemy);
            enemy.EnemyHealth.DeathEvent += () => HandleSpawnedEnemyDeath(enemy);
        }

        private void HandleSpawnedEnemyDeath(global::Enemy enemy)
        {
            _spawnedEnemies.Remove(enemy);
            TryEnterFreedReady();
        }

        // 결박 해제 대기: 웨이브가 끝나고 이벤트가 만든 적이 모두 죽었을 때만 들어간다. 다시 E로 말을 걸면 푼다
        private void TryEnterFreedReady()
        {
            if (_state != State.Fighting || !_waveEnded) return;

            if (_spawnedEnemies.Count > 0) return;

            _state = State.FreedReady;
            SetInteractable(true);
        }

        // ───────────────────────────── 3. 결박 해제 (E) ─────────────────────────────

        private void CompleteFree()
        {
            _state = State.Freeing;
            SetInteractable(false);

            // 컷씬 재생 여부와 무관하게 구출은 즉시 기록한다 (탈출해야 퀘스트 완료로 반영)
            GameManager.Instance?.Quests?.RaiseRunFlag(_runFlag);

            var director = CutsceneDirector.Current;
            if (director != null && _teleportCutscene != null && director.CanPlay(_teleportCutscene) && director.Play(_teleportCutscene))
            {
                CutsceneDirector.CutsceneFinished += HandleCutsceneFinished;
                StartCoroutine(StopBoundClipAfterTakeover());
                return;
            }

            StopBoundClip();
            StartCoroutine(TeleportFallback(director));
        }

        // 컷씬은 다음 틱에 일어서기 클립을 시작한다. 그 전에 결박 클립을 걷으면 한두 프레임 서 있는 모습(Idle)이 비치므로,
        // 일어서기가 이어받을 때까지 둔다. (일어서기의 첫 프레임이 쓰러진 모습이라 겹쳐도 티가 나지 않는다)
        private IEnumerator StopBoundClipAfterTakeover()
        {
            yield return null;
            yield return null;
            StopBoundClip();
        }

        private IEnumerator TeleportFallback(CutsceneDirector director)
        {
            if (director != null)
            {
                director.ShowSpeech(Elena, _teleportSpeech, _teleportSpeechSeconds);
            }

            // 컷씬이 없으니 시전 자세는 여기서 직접 만든다
            if (_elenaAnimator != null) _elenaAnimator.enabled = false;
            if (_elenaRenderer != null && _castSprite != null) _elenaRenderer.sprite = _castSprite;
            OnElenaCastFx();
            yield return new WaitForSeconds(_teleportSpeechSeconds);

            OnElenaGone();
            FinishRescue();
        }

        /// <summary>
        /// 전이 컷씬의 Signal("ElenaCastFx")에서 호출. 마법진 FX만 켭니다. (시전 모습은 컷씬의 시전 클립이 담당)
        /// </summary>
        public void OnElenaCastFx()
        {
            if (_castFx != null) _castFx.SetActive(true);
        }

        /// <summary>
        /// 전이 컷씬의 Signal("ElenaGone")에서 호출. 엘레나를 숨깁니다. (FX 오브젝트가 엘레나 하위가 아니면 남아 있습니다)
        /// </summary>
        public void OnElenaGone()
        {
            if (_elenaGone) return;
            _elenaGone = true;
            Elena.gameObject.SetActive(false);
        }

        private void FinishRescue()
        {
            if (_state == State.Done) return;
            _state = State.Done;
            _onObjectiveChanged.Invoke(_escapeObjective);
        }

        // ───────────────────────────── 보조 ─────────────────────────────

        private void ApplyBoundLook()
        {
            // 컨트롤러의 Idle이 쓰러진 모습을 덮어쓰지 않도록 결박 클립을 덮어 재생한다
            if (_elenaAnimator != null && _boundClip != null) AnimationPlayableUtilities.PlayClip(_elenaAnimator, _boundClip, out _boundGraph);
        }

        private void StopBoundClip()
        {
            if (_boundGraph.IsValid()) _boundGraph.Destroy();
        }

        private void SetInteractable(bool value)
        {
            if (_interactable != null) _interactable.enabled = value;
        }
    }
}
