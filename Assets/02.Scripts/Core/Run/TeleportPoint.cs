using ShotgunAndRun.Player;
using ShotgunAndRun.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using PlayerComponent = ShotgunAndRun.Player.Player;

namespace ShotgunAndRun.Core
{
    // 순간이동 지점 (game-spec 4.2, level-design 7장)
    // 짝 지점과 양방향으로 이어집니다. 범위 안에서 E를 2초 누르고 있으면 짝 지점으로 바로 이동합니다.
    // 한 번 사용하면 양쪽 지점이 재사용 대기 시간(기본 60초) 동안 꺼지고 불 꺼진 기둥(또는 비활성 포탈)으로 바뀝니다.
    // 홀드 중에는 이동 · 사격 · 대시 · 재장전 입력을 막아 무방비 상태가 됩니다. 조준은 막지 않아 커서 방향을 계속 바라봅니다.
    // E를 떼거나 범위를 벗어나면 진행이 초기화됩니다.
    // 도착한 뒤에는 E를 한 번 떼야 다시 사용할 수 있습니다. (누른 채로 되돌아가는 것 방지)
    // ExtractionZone과 같은 이유로 범위는 콜라이더 대신 플레이어 위치로 판정합니다.
    // 홀드 진행은 기둥 위의 행동 진행 막대(ActionProgressBar)로 표시합니다.
    // 오브젝트 위치는 기둥 밑동이며, 도착 위치는 짝 지점의 밑동 + 도착 오프셋입니다. (기둥 콜라이더와 겹치지 않도록)
    public sealed class TeleportPoint : MonoBehaviour
    {
        // 모든 지점이 공유합니다. 순간이동 직후 E를 떼기 전까지 true
        private static bool _waitForRelease;

        [SerializeField] private TeleportPoint _destination;
        [SerializeField, Min(0.1f)] private float _holdTime = 2f;
        [Tooltip("사용 가능 반경 (월드 단위, 기둥 밑동 기준)")]
        [SerializeField, Min(0.5f)] private float _radius = 2f;
        [Tooltip("다른 지점에서 이 지점으로 올 때 도착 위치 (밑동 기준 로컬 오프셋)")]
        [SerializeField] private Vector2 _arrivalOffset = new Vector2(0f, -1.5f);
        [Tooltip("사용한 뒤 양쪽 지점이 다시 켜질 때까지 걸리는 시간 (초)")]
        [SerializeField, Min(0f)] private float _cooldown = 60f;

        [Header("Visual")]
        [Tooltip("사용 가능 범위 안에 있을 때 켜는 외곽선")]
        [SerializeField] private GameObject _highlight;
        [Tooltip("기둥 스프라이트를 그리는 렌더러")]
        [SerializeField] private SpriteRenderer _pillarRenderer;
        [Tooltip("재사용 대기 중에 보여 줄 스프라이트 (불 꺼진 구슬 기둥)")]
        [SerializeField] private Sprite _cooldownSprite;
        [Tooltip("홀드 진행을 표시할 기둥 위 막대")]
        [SerializeField] private ActionProgressBar _progressBar;
        [Tooltip("기둥 대신 포탈을 쓸 때의 애니메이터. Disabled(bool): 재사용 대기 중 true → 비활성(검은) 포탈, 끝나면 false → 다시 빛이 들어옴")]
        [SerializeField] private Animator _portalAnimator;

        private static readonly int DisabledHash = Animator.StringToHash("Disabled");

        private PlayerInputReader _lockedInput;
        private Sprite _readySprite;
        private float _holdElapsed;

        public TeleportPoint Destination => _destination;

        public Vector2 ArrivalPosition => transform.TransformPoint(_arrivalOffset);

        public float HoldProgress => Mathf.Clamp01(_holdElapsed / _holdTime);

        public bool IsHolding => _lockedInput != null;

        // 재사용 대기 시간이 남아 있으면 0보다 큽니다.
        public float CooldownRemaining
        {
            get;
            private set;
        }

        public bool IsCoolingDown => CooldownRemaining > 0f;

        private void Awake()
        {
            _readySprite = _pillarRenderer != null ? _pillarRenderer.sprite : null;
            SetHighlight(false);
            SetPortalDisabled(false);
        }

        private void OnDisable()
        {
            StopHolding();
            SetHighlight(false);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            bool isKeyHeld = keyboard != null && keyboard.eKey.isPressed;

            if (!isKeyHeld)
            {
                _waitForRelease = false;
            }

            TickCooldown();

            PlayerComponent player = PlayerComponent.PlayerInstance;
            bool isUsable = !IsCoolingDown
                && _destination != null
                && !_destination.IsCoolingDown
                && player != null
                && player.Health != null
                && !player.Health.IsDead
                && Contains(player.transform.position);

            SetHighlight(isUsable);

            if (!isUsable || !isKeyHeld || _waitForRelease)
            {
                StopHolding(isCancelled: true);
                return;
            }

            if (!IsHolding)
            {
                StartHolding(player);
            }

            _holdElapsed += Time.deltaTime;
            _progressBar?.SetProgress(this, HoldProgress);

            if (_holdElapsed >= _holdTime)
            {
                StopHolding(isCancelled: false);
                _waitForRelease = true;
                PlayerPlacement.MoveTo(player, _destination.ArrivalPosition);
                StartCooldown();
                _destination.StartCooldown();
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.85f, 0.9f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, _radius);
            Gizmos.DrawWireCube(ArrivalPosition, new Vector3(0.5f, 0.5f, 0f));

            if (_destination != null)
            {
                Gizmos.DrawLine(transform.position, _destination.transform.position);
            }
        }

        public bool Contains(Vector2 worldPosition)
        {
            return Vector2.Distance(transform.position, worldPosition) <= _radius;
        }

        private void StartHolding(PlayerComponent player)
        {
            _holdElapsed = 0f;
            _lockedInput = player.GetComponent<PlayerInputReader>();
            _progressBar?.SetProgress(this, 0f);

            if (_lockedInput != null)
            {
                _lockedInput.SetActionsEnabled(false);
            }
        }

        private void StopHolding(bool isCancelled = false)
        {
            if (_lockedInput != null)
            {
                // 사망 처리는 SetGameplayEnabled로 입력 전체를 막으므로, 여기서 행동 입력을 켜도 되살아나지 않습니다.
                _lockedInput.SetActionsEnabled(true);
                _lockedInput = null;
            }

            if (isCancelled && _holdElapsed > 0f && _holdElapsed < _holdTime)
            {
                _progressBar?.Cancel(this);
            }
            else
            {
                _progressBar?.Hide(this);
            }

            _holdElapsed = 0f;
        }

        private void StartCooldown()
        {
            if (_cooldown <= 0f)
            {
                return;
            }

            CooldownRemaining = _cooldown;
            StopHolding();
            SetHighlight(false);
            SetPillarSprite(_cooldownSprite);
            SetPortalDisabled(true);
        }

        private void TickCooldown()
        {
            if (!IsCoolingDown)
            {
                return;
            }

            CooldownRemaining = Mathf.Max(0f, CooldownRemaining - Time.deltaTime);
            if (!IsCoolingDown)
            {
                SetPillarSprite(_readySprite);
                SetPortalDisabled(false);
            }
        }

        private void SetPortalDisabled(bool value)
        {
            if (_portalAnimator != null)
            {
                _portalAnimator.SetBool(DisabledHash, value);
            }
        }

        private void SetPillarSprite(Sprite sprite)
        {
            if (_pillarRenderer != null && sprite != null)
            {
                _pillarRenderer.sprite = sprite;
            }
        }

        private void SetHighlight(bool value)
        {
            if (_highlight != null && _highlight.activeSelf != value)
            {
                _highlight.SetActive(value);
            }
        }
    }
}
