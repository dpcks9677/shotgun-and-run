using UnityEngine;

namespace InventorySystem
{
    /// <summary>
    /// 플레이어가 상자에 가까이 갔을 때(상호작용 가능 범위 내) 상자 테두리에 하이라이트(외곽선)를 띄우고,
    /// 부드러운 숨쉬기(Pulse) 깜빡임 연출을 제공하는 시각 효과 컴포넌트.
    /// 미개봉 상자에는 선명한 골드 옐로우, 개봉 완료 상자에는 은은한 실버 화이트로 구분(C안)하여 표시합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ChestHighlightVisual : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("연동할 상자 컴포넌트 (비워두면 자동 탐색)")]
        [SerializeField] private LootChest _chest;

        [Tooltip("상자 스프라이트를 렌더링하는 SpriteRenderer (비워두면 상자에서 자동 탐색)")]
        [SerializeField] private SpriteRenderer _targetRenderer;

        [Tooltip("외곽선 렌더러에 적용할 단색 셰이더 머티리얼 (InteractOutline)")]
        [SerializeField] private Material _outlineMaterial;

        [Header("Outline Visual Settings (C안)")]
        [Tooltip("미개봉 상자에 접근했을 때 외곽선 기본 색상 (골드/옐로우)")]
        [SerializeField] private Color _unopenedColor = new Color(1f, 0.85f, 0.2f, 1f);

        [Tooltip("이미 열린 상자에 접근했을 때 외곽선 기본 색상 (소프트 실버/화이트)")]
        [SerializeField] private Color _openedColor = new Color(0.82f, 0.86f, 0.95f, 0.75f);

        [Tooltip("외곽선 두께 (스프라이트 픽셀 단위, 기본 1px)")]
        [SerializeField, Min(1)] private int _pixelThickness = 1;

        [Header("Pulse Animation Settings")]
        [Tooltip("외곽선 부드러운 깜빡임(Pulse) 활성화 여부")]
        [SerializeField] private bool _enablePulse = true;

        [Tooltip("깜빡임 주기 속도")]
        [SerializeField, Min(0.1f)] private float _pulseSpeed = 4.5f;

        [Tooltip("최소 알파 비율 (0.0 ~ 1.0)")]
        [SerializeField, Range(0f, 1f)] private float _minAlphaRatio = 0.4f;

        private GameObject _outlineRoot;
        private SpriteRenderer[] _outlineRenderers;
        private Sprite _lastCachedSprite;
        private int _lastSortingOrder;
        private int _lastSortingLayerID;

        // 8방향 오프셋 벡터 (상, 하, 좌, 우 및 4개 대각선)
        private static readonly Vector2[] Directions = new Vector2[]
        {
            new Vector2(0f, 1f),
            new Vector2(0f, -1f),
            new Vector2(1f, 0f),
            new Vector2(-1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(1f, -1f),
            new Vector2(-1f, 1f),
            new Vector2(-1f, -1f),
        };

        private void Awake()
        {
            ResolveReferences();
            CreateOutlineObjects();
        }

        private void ResolveReferences()
        {
            _chest ??= GetComponentInParent<LootChest>();
            if (_chest == null)
            {
                _chest = GetComponent<LootChest>();
            }

            if (_targetRenderer == null && _chest != null)
            {
                _targetRenderer = _chest.SpriteRenderer;
            }

            if (_targetRenderer == null)
            {
                _targetRenderer = GetComponent<SpriteRenderer>();
                if (_targetRenderer == null)
                {
                    _targetRenderer = GetComponentInChildren<SpriteRenderer>();
                }
            }
        }

        private void CreateOutlineObjects()
        {
            if (_outlineRoot != null || _targetRenderer == null) return;

            _outlineRoot = new GameObject("OutlineContainer");
            _outlineRoot.transform.SetParent(_targetRenderer.transform, false);
            _outlineRoot.transform.localPosition = Vector3.zero;
            _outlineRoot.transform.localRotation = Quaternion.identity;
            _outlineRoot.transform.localScale = Vector3.one;

            _outlineRenderers = new SpriteRenderer[Directions.Length];

            float ppu = GetTargetPPU();
            float unitOffset = (1f / ppu) * _pixelThickness;

            for (int i = 0; i < Directions.Length; i++)
            {
                var dir = Directions[i];
                var childGo = new GameObject($"Outline_{i}");
                childGo.transform.SetParent(_outlineRoot.transform, false);
                childGo.transform.localPosition = new Vector3(dir.x * unitOffset, dir.y * unitOffset, 0f);
                childGo.transform.localRotation = Quaternion.identity;
                childGo.transform.localScale = Vector3.one;

                var sr = childGo.AddComponent<SpriteRenderer>();
                if (_outlineMaterial != null)
                {
                    sr.sharedMaterial = _outlineMaterial;
                }

                sr.sortingLayerID = _targetRenderer.sortingLayerID;
                sr.sortingOrder = _targetRenderer.sortingOrder - 1; // 본체 스프라이트 뒤에 배치
                sr.sprite = _targetRenderer.sprite;
                sr.flipX = _targetRenderer.flipX;
                sr.flipY = _targetRenderer.flipY;
                sr.color = GetCurrentBaseColor();

                _outlineRenderers[i] = sr;
            }

            _lastCachedSprite = _targetRenderer.sprite;
            _lastSortingOrder = _targetRenderer.sortingOrder;
            _lastSortingLayerID = _targetRenderer.sortingLayerID;

            _outlineRoot.SetActive(false);
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (_chest != null)
            {
                _chest.OnPlayerRangeChanged += HandlePlayerRangeChanged;
                _chest.OnChestOpened += HandleChestOpened;

                SetOutlineActive(_chest.IsPlayerInRange);
            }
        }

        private void OnDisable()
        {
            if (_chest != null)
            {
                _chest.OnPlayerRangeChanged -= HandlePlayerRangeChanged;
                _chest.OnChestOpened -= HandleChestOpened;
            }

            SetOutlineActive(false);
        }

        private void Update()
        {
            if (_outlineRoot == null || !_outlineRoot.activeSelf) return;

            SyncVisualWithTarget();
            UpdateOutlineColor();
        }

        private void HandlePlayerRangeChanged(bool inRange)
        {
            SetOutlineActive(inRange);
        }

        private void HandleChestOpened(LootChest chest)
        {
            if (_outlineRoot != null && _outlineRoot.activeSelf)
            {
                SyncVisualWithTarget();
                UpdateOutlineColor();
            }
        }

        private void SetOutlineActive(bool active)
        {
            if (_outlineRoot == null) return;

            if (_outlineRoot.activeSelf != active)
            {
                _outlineRoot.SetActive(active);
            }

            if (active)
            {
                SyncVisualWithTarget();
                UpdateOutlineColor();
            }
        }

        /// <summary>
        /// 본체 렌더러의 스프라이트 변경(닫힘 -> 열림), 플립, 소팅 레이어 변경을 외곽선 렌더러들에 반영합니다.
        /// </summary>
        private void SyncVisualWithTarget()
        {
            if (_targetRenderer == null || _outlineRenderers == null) return;

            bool spriteChanged = _lastCachedSprite != _targetRenderer.sprite;
            bool sortingChanged = _lastSortingOrder != _targetRenderer.sortingOrder || _lastSortingLayerID != _targetRenderer.sortingLayerID;

            if (spriteChanged || sortingChanged)
            {
                _lastCachedSprite = _targetRenderer.sprite;
                _lastSortingOrder = _targetRenderer.sortingOrder;
                _lastSortingLayerID = _targetRenderer.sortingLayerID;

                float ppu = GetTargetPPU();
                float unitOffset = (1f / ppu) * _pixelThickness;

                for (int i = 0; i < _outlineRenderers.Length; i++)
                {
                    var sr = _outlineRenderers[i];
                    if (sr == null) continue;

                    sr.sprite = _targetRenderer.sprite;
                    sr.flipX = _targetRenderer.flipX;
                    sr.flipY = _targetRenderer.flipY;
                    sr.sortingLayerID = _targetRenderer.sortingLayerID;
                    sr.sortingOrder = _targetRenderer.sortingOrder - 1;

                    var dir = Directions[i];
                    sr.transform.localPosition = new Vector3(dir.x * unitOffset, dir.y * unitOffset, 0f);
                }
            }
            else
            {
                // 플립만 바뀌었을 경우 동기화
                for (int i = 0; i < _outlineRenderers.Length; i++)
                {
                    var sr = _outlineRenderers[i];
                    if (sr != null)
                    {
                        sr.flipX = _targetRenderer.flipX;
                        sr.flipY = _targetRenderer.flipY;
                    }
                }
            }
        }

        /// <summary>
        /// 현재 상자 상태(C안: 미개봉 vs 개봉) 및 숨쉬기(Pulse) 사인파에 따라 외곽선 색상과 알파를 갱신합니다.
        /// </summary>
        private void UpdateOutlineColor()
        {
            if (_outlineRenderers == null) return;

            Color baseColor = GetCurrentBaseColor();
            float alphaFactor = 1f;

            if (_enablePulse)
            {
                // 0 ~ 1 사이로 진동하는 사인 곡선
                float wave = (Mathf.Sin(Time.time * _pulseSpeed) + 1f) * 0.5f;
                alphaFactor = Mathf.Lerp(_minAlphaRatio, 1f, wave);
            }

            Color currentColor = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alphaFactor);

            for (int i = 0; i < _outlineRenderers.Length; i++)
            {
                var sr = _outlineRenderers[i];
                if (sr != null)
                {
                    sr.color = currentColor;
                }
            }
        }

        private Color GetCurrentBaseColor()
        {
            bool isOpened = _chest != null && _chest.IsOpened;
            return isOpened ? _openedColor : _unopenedColor;
        }

        private float GetTargetPPU()
        {
            if (_targetRenderer != null && _targetRenderer.sprite != null && _targetRenderer.sprite.pixelsPerUnit > 0f)
            {
                return _targetRenderer.sprite.pixelsPerUnit;
            }
            return 16f;
        }
    }
}
