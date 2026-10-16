using System.Collections;
using System.Collections.Generic;
using ShotgunAndRun.Core;
using ShotgunAndRun.Enemy;
using ShotgunAndRun.NPC;
using ShotgunAndRun.Player;
using ShotgunAndRun.Quest;
using ShotgunAndRun.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using PlayerComponent = ShotgunAndRun.Player.Player;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 구역 정화(퀘스트 디펜스) 활성 오브젝트 (game-spec 4.3, quest-db 1.4 · 1.5). Z4 · Z7에 하나씩 둡니다.
    /// 퀘스트를 수주한 런에만 보이고, 범위 안에서 E를 2초 누르면 디펜스 웨이브를 시작합니다.
    /// 작동하면 존 출입구(DefenseGate)를 모두 닫고, 웨이브를 모두 처치하면 문을 열고 런 플래그를 올립니다(탈출해야 완료).
    /// 출입구가 닫혀 있어도 순간이동 등으로 존 범위를 벗어나면 초기화해 다시 작동할 수 있게 합니다 (안전장치).
    /// 진행 중에는 주기 습격 타이머를 멈춥니다 (game-spec 4.6).
    /// 웨이브는 꺼 둔 EnemyDestructionSpawner(템플릿)에 설정하고, 작동할 때마다 복제해서 돌립니다. (초기화 시 복제본만 지움)
    /// 초기화해도 이미 나온 적은 남아서 추적합니다. 스폰 중이던 포탈은 닫습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuestDefenseEvent : MonoBehaviour
    {
        private enum State
        {
            Inactive,
            Ready,
            Fighting,
            Cleared,
        }

        [Header("퀘스트")]
        [SerializeField] private string _questId = "SQ_DEF_Z4";
        [SerializeField] private string _runFlag = QuestFlags.Z4DefenseCleared;

        [Header("웨이브")]
        [Tooltip("디펜스 웨이브를 설정한 스포너. 꺼 둔 오브젝트로 두세요 (작동할 때마다 복제해서 켭니다)")]
        [SerializeField] private EnemyDestructionSpawner _spawnerTemplate;

        [Header("출입구")]
        [Tooltip("디펜스 중 닫을 존 출입구 문")]
        [SerializeField] private DefenseGate[] _gates = new DefenseGate[0];
        [Tooltip("출입구가 닫힐 때 한 번 재생 (문이 여러 개여도 겹치지 않게 여기서 재생)")]
        [SerializeField] private AudioClip _gateCloseSfx;

        [Header("상호작용")]
        [SerializeField, Min(0.1f)] private float _holdTime = 2f;
        [Tooltip("작동 가능 반경 (월드 단위, 오브젝트 위치 기준)")]
        [SerializeField, Min(0.5f)] private float _radius = 2f;
        [Tooltip("작동 가능 범위 안에 있을 때 켜는 외곽선")]
        [SerializeField] private SpriteOutline _highlight;
        [Tooltip("홀드 진행을 표시할 막대")]
        [SerializeField] private ActionProgressBar _progressBar;
        [Tooltip("작동 대기 중에 띄우는 머리 위 마커 (전구 말풍선). 홀드 · 웨이브 중에는 숨깁니다")]
        [SerializeField] private GameObject _marker;

        [Header("존 범위")]
        [Tooltip("진행 중 벗어나면 초기화되는 범위의 중심. 비우면 이 오브젝트 위치")]
        [SerializeField] private Transform _zoneCenter;
        [SerializeField] private Vector2 _zoneSize = new Vector2(30f, 30f);

        private State _state;
        private EnemyDestructionSpawner _spawner;
        private readonly HashSet<RaidPortal> _portalsBeforeDefense = new HashSet<RaidPortal>();
        private PlayerInputReader _lockedInput;
        private float _holdElapsed;
        private bool _waitForRelease;

        private Vector2 ZoneCenter => _zoneCenter != null ? (Vector2)_zoneCenter.position : (Vector2)transform.position;

        private void Start()
        {
            if (_spawnerTemplate != null) _spawnerTemplate.gameObject.SetActive(false);
            SetHighlight(false);
            SetMarker(false);

            if (!ShouldBeActive())
            {
                _state = State.Inactive;
                gameObject.SetActive(false);
                return;
            }

            _state = State.Ready;
            SetMarker(true);
        }

        // 진행 중 꺼지면(씬 종료 포함) 존 이탈 검사가 멈추므로 초기화해 습격 타이머를 되살린다
        private void OnDisable()
        {
            StopHolding(isCancelled: true);
            SetHighlight(false);
            ResetDefense();
        }

        // 수주 중(완료 가능 · 완료 전)일 때만 켠다. 진행 데이터가 없는 테스트 씬은 켠다.
        private bool ShouldBeActive()
        {
            QuestService quests = GameManager.Instance != null ? GameManager.Instance.Quests : null;
            if (quests == null)
            {
                Debug.LogWarning("[QuestDefenseEvent] 퀘스트 서비스(GameManager)가 없어 수주 여부를 검사하지 않습니다.", this);
                return true;
            }

            return quests.TryGetQuest(_questId, out QuestDefinitionSO quest) && quests.GetState(quest) == QuestState.Accepted;
        }

        private void Update()
        {
            PlayerComponent player = PlayerComponent.PlayerInstance;
            bool isPlayerAlive = player != null && player.Health != null && !player.Health.IsDead;

            if (_state == State.Fighting)
            {
                if (isPlayerAlive && !IsInZone(player.transform.position)) ResetDefense();
                return;
            }

            if (_state != State.Ready) return;

            UpdateHold(player, isPlayerAlive);
        }

        // ───────────────────────────── 1. 작동 (E 홀드) ─────────────────────────────

        private void UpdateHold(PlayerComponent player, bool isPlayerAlive)
        {
            Keyboard keyboard = Keyboard.current;
            bool isKeyHeld = keyboard != null && keyboard.eKey.isPressed;
            if (!isKeyHeld) _waitForRelease = false;

            bool isUsable = isPlayerAlive && Vector2.Distance(transform.position, player.transform.position) <= _radius;
            SetHighlight(isUsable);

            if (!isUsable || !isKeyHeld || _waitForRelease)
            {
                StopHolding(isCancelled: true);
                return;
            }

            if (_lockedInput == null) StartHolding(player);

            _holdElapsed += Time.deltaTime;
            _progressBar?.SetProgress(this, Mathf.Clamp01(_holdElapsed / _holdTime));

            if (_holdElapsed < _holdTime) return;

            StopHolding(isCancelled: false);
            _waitForRelease = true;
            BeginDefense();
        }

        private void StartHolding(PlayerComponent player)
        {
            _holdElapsed = 0f;
            _lockedInput = player.GetComponent<PlayerInputReader>();
            _progressBar?.SetProgress(this, 0f);
            if (_lockedInput != null) _lockedInput.SetActionsEnabled(false);
            SetMarker(false);
        }

        private void StopHolding(bool isCancelled)
        {
            if (_lockedInput != null) _lockedInput.SetActionsEnabled(true);
            _lockedInput = null;

            if (isCancelled && _holdElapsed > 0f && _holdElapsed < _holdTime) _progressBar?.Cancel(this);
            else _progressBar?.Hide(this);

            _holdElapsed = 0f;
            SetMarker(_state == State.Ready && isCancelled);
        }

        // ───────────────────────────── 2. 웨이브 ─────────────────────────────

        private void BeginDefense()
        {
            if (_spawnerTemplate == null)
            {
                Debug.LogError("[QuestDefenseEvent] 스포너 템플릿이 비어 있어 디펜스를 시작할 수 없습니다.", this);
                return;
            }

            _state = State.Fighting;
            SetHighlight(false);
            SetMarker(false);
            PauseRaidTimer();
            RememberExistingPortals();
            SetGatesClosed(true);
            AudioManager.Instance.PlaySfx(_gateCloseSfx);

            _spawner =Instantiate(_spawnerTemplate, _spawnerTemplate.transform.parent);
            _spawner.transform.SetParent(global::SpawnerLayer.Root, true);
            _spawner.CompletedEvent += HandleWaveCompleted;
            _spawner.gameObject.SetActive(true);
            StartCoroutine(KickSpawner(_spawner));
        }

        // EnemyDestructionSpawner는 플레이어가 트리거에 들어와야 시작하므로(공개 시작 메서드 없음),
        // Start(플레이어 참조 확보)가 끝난 다음 프레임에 플레이어 콜라이더로 OnTriggerEnter2D를 보낸다. (ElenaRescueEvent와 같은 방식)
        private IEnumerator KickSpawner(EnemyDestructionSpawner spawner)
        {
            yield return null;
            if (spawner == null || spawner != _spawner) yield break;

            PlayerComponent player = PlayerComponent.PlayerInstance;
            Collider2D playerCollider = player != null ? player.GetComponentInChildren<Collider2D>() : null;
            if (playerCollider == null)
            {
                Debug.LogError("[QuestDefenseEvent] 플레이어 콜라이더가 없어 디펜스 웨이브를 시작할 수 없습니다.", this);
                ResetDefense();
                yield break;
            }

            spawner.SendMessage("OnTriggerEnter2D", playerCollider, SendMessageOptions.DontRequireReceiver);
        }

        private void HandleWaveCompleted()
        {
            if (_state != State.Fighting) return;

            _state = State.Cleared;
            DiscardSpawner();
            ResumeRaidTimer();
            SetGatesClosed(false);
            GameManager.Instance?.Quests?.RaiseRunFlag(_runFlag);
        }

        // 존 이탈 등: 스포너만 지우고 다시 작동할 수 있게 한다. 이미 나온 적은 남는다.
        private void ResetDefense()
        {
            if (_state != State.Fighting) return;

            _state = State.Ready;
            DiscardSpawner();
            ResumeRaidTimer();
            SetGatesClosed(false);
            SetMarker(true);
        }

        private void SetGatesClosed(bool closed)
        {
            foreach (DefenseGate gate in _gates)
            {
                if (gate == null) continue;
                if (closed) gate.Close();
                else gate.Open();
            }
        }

        private void DiscardSpawner()
        {
            if (_spawner == null) return;

            _spawner.CompletedEvent -= HandleWaveCompleted;
            Destroy(_spawner.gameObject);
            _spawner = null;
            CloseDefensePortals();
        }

        // 스포너를 지우면 스폰 코루틴이 끊겨 열린 포탈이 닫히지 않고 길을 막는다.
        // 시작 전에 있던 포탈(주기 습격 등)을 기억해 두고, 그 뒤에 생긴 포탈만 닫는다. (진행 중에는 습격 타이머가 멈춰 있음)
        private void RememberExistingPortals()
        {
            _portalsBeforeDefense.Clear();
            foreach (RaidPortal portal in FindObjectsByType<RaidPortal>(FindObjectsSortMode.None))
            {
                _portalsBeforeDefense.Add(portal);
            }
        }

        private void CloseDefensePortals()
        {
            foreach (RaidPortal portal in FindObjectsByType<RaidPortal>(FindObjectsSortMode.None))
            {
                // 씬 종료 중 비활성 포탈은 코루틴을 시작할 수 없으므로 건너뛴다
                if (!_portalsBeforeDefense.Contains(portal) && portal.isActiveAndEnabled) portal.Close();
            }

            _portalsBeforeDefense.Clear();
        }

        // ───────────────────────────── 보조 ─────────────────────────────

        private bool IsInZone(Vector2 worldPosition)
        {
            Vector2 offset = worldPosition - ZoneCenter;
            return Mathf.Abs(offset.x) <= _zoneSize.x * 0.5f && Mathf.Abs(offset.y) <= _zoneSize.y * 0.5f;
        }

        private static void PauseRaidTimer()
        {
            GameManager.Instance?.CurrentRun?.RaidTimer?.PauseTimer();
        }

        private static void ResumeRaidTimer()
        {
            GameManager.Instance?.CurrentRun?.RaidTimer?.ResumeTimer();
        }

        private void SetMarker(bool value)
        {
            if (_marker != null && _marker.activeSelf != value) _marker.SetActive(value);
        }

        private void SetHighlight(bool value)
        {
            if (_highlight != null && _highlight.Visible != value) _highlight.SetVisible(value);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.95f, 0.55f, 0.15f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, _radius);
            Gizmos.DrawWireCube(ZoneCenter, new Vector3(_zoneSize.x, _zoneSize.y, 0f));
        }
    }
}
