using UnityEngine;
using PlayerComponent = ShotgunAndRun.Player.Player;

namespace ShotgunAndRun.Core
{
    // 탈출 구역 (game-spec 4.4)
    // 런 시작 3분 후 활성화되며, 플레이어가 구역 안에서 10초 머무르면 탈출 성공입니다.
    // 구역을 벗어나면 대기 시간이 초기화됩니다.
    // 대기 시간은 RunSession이 관리하며, 이 컴포넌트는 플레이어가 구역 안에 있는지만 알립니다.
    // 콜라이더를 쓰면 히트스캔 사격이 구역 경계에 막히므로, 플레이어 위치로 판정합니다.
    // 구역 표시: 1픽셀 테두리 선(포탈 파티클과 같은 파랑)과 선에서 올라오는 파티클, 그리고 포탈 애니메이터(비활성 검정 → 활성 파랑 점등).
    public sealed class ExtractionZone : MonoBehaviour
    {
        private static Sprite _squareSprite;

        [Header("Area")]
        [Tooltip("구역 크기 (로컬 단위). 오브젝트 위치가 중심입니다.")]
        [SerializeField] private Vector2 _size = new Vector2(8f, 8f);

        [Header("Visual")]
        [Tooltip("구역 표시용 테두리 선. 스프라이트(9분할, 테두리 1픽셀)가 비어 있으면 구역 크기의 사각형을 자동으로 사용합니다.")]
        [SerializeField] private SpriteRenderer _visual;
        [SerializeField] private Color _lockedColor = new Color(0.55f, 0.54f, 0.64f, 0.55f);
        [SerializeField] private Color _activeColor = new Color(0.31f, 0.69f, 1f, 1f);
        [SerializeField] private Color _holdingColor = new Color(0.71f, 0.9f, 1f, 1f);

        [Header("Particles")]
        [Tooltip("테두리 선에서 올라오는 파티클. 모양(Box Edge)은 구역 크기에 맞춰 자동으로 조절합니다.")]
        [SerializeField] private ParticleSystem _particles;
        [Tooltip("초당 방출 수 (구역 둘레 1유닛 기준)")]
        [SerializeField, Min(0f)] private float _activeRate = 1.5f;
        [SerializeField, Min(0f)] private float _holdingRate = 3.5f;

        [Header("Portal")]
        [Tooltip("구역 안 포탈 애니메이터. Active(bool): 탈출 구역이 열리면 true → 검정 포탈에 파랑 불이 들어옴")]
        [SerializeField] private Animator _portalAnimator;

        private static readonly int ActiveHash = Animator.StringToHash("Active");
        private int _lastState = -1;

        private RunSession _holdingRun;

        public bool IsActive
        {
            get;
            private set;
        }

        public bool IsPlayerInside
        {
            get;
            private set;
        }

        // 이 구역에서 탈출 대기 중인지 여부
        public bool IsHolding => _holdingRun != null;

        private void Awake()
        {
            SetupVisual();
            UpdateVisual();
        }

        private void OnDisable()
        {
            IsPlayerInside = false;
            StopHolding();
        }

        private void Update()
        {
            GameManager gameManager = GameManager.Instance;
            RunSession run = gameManager != null ? gameManager.CurrentRun : null;
            IsActive = run != null && !run.IsEnded && run.IsExtractionUnlocked;
            IsPlayerInside = ContainsPlayer();

            if (IsActive && IsPlayerInside)
            {
                _holdingRun = run;
                run.AdvanceExtractionHold(Time.deltaTime);

                if (run.IsExtractionHoldComplete)
                {
                    _holdingRun = null;
                    gameManager.EndRun(RunOutcome.Extracted);
                }
            }
            else
            {
                StopHolding();
            }

            UpdateVisual();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.9f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, _size);
        }

        private void OnValidate()
        {
            _size = Vector2.Max(_size, Vector2.zero);
        }

        public bool Contains(Vector2 worldPosition)
        {
            Vector2 local = transform.InverseTransformPoint(worldPosition);
            Vector2 half = _size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y;
        }

        private bool ContainsPlayer()
        {
            PlayerComponent player = PlayerComponent.PlayerInstance;
            return player != null && Contains(player.transform.position);
        }

        // 이 구역에서 대기하던 경우에만 초기화합니다. (다른 구역의 대기를 지우지 않도록)
        private void StopHolding()
        {
            if (_holdingRun == null)
            {
                return;
            }

            _holdingRun.CancelExtractionHold();
            _holdingRun = null;
        }

        private void SetupVisual()
        {
            if (_visual == null)
            {
                return;
            }

            if (_visual.sprite == null)
            {
                _visual.sprite = GetSquareSprite();
            }

            _visual.drawMode = SpriteDrawMode.Sliced;
            _visual.size = _size;

            if (_particles != null)
            {
                ParticleSystem.ShapeModule shape = _particles.shape;
                shape.scale = new Vector3(_size.x, _size.y, 0f);
            }
        }

        private void UpdateVisual()
        {
            // 0 = 잠김, 1 = 열림, 2 = 대기 중. 바뀔 때만 파티클 · 애니메이터를 건드린다.
            int state = !IsActive ? 0 : (IsHolding ? 2 : 1);
            if (state == _lastState)
            {
                return;
            }

            _lastState = state;

            if (_visual != null)
            {
                _visual.color = state == 0 ? _lockedColor : (state == 2 ? _holdingColor : _activeColor);
            }

            if (_particles != null)
            {
                ParticleSystem.EmissionModule emission = _particles.emission;
                float perimeter = (_size.x + _size.y) * 2f;
                emission.rateOverTime = perimeter * (state == 0 ? 0f : (state == 2 ? _holdingRate : _activeRate));
            }

            if (_portalAnimator != null)
            {
                _portalAnimator.SetBool(ActiveHash, state != 0);
            }
        }

        // 1유닛 크기의 흰색 사각형 스프라이트
        private static Sprite GetSquareSprite()
        {
            if (_squareSprite != null)
            {
                return _squareSprite;
            }

            Texture2D texture = Texture2D.whiteTexture;
            _squareSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                texture.width,
                0,
                SpriteMeshType.FullRect);
            return _squareSprite;
        }
    }
}
