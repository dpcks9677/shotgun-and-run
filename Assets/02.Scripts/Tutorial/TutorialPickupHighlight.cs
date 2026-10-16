using UnityEngine;

namespace ShotgunAndRun.Tutorial
{
    /// <summary>
    /// (프로토타입) 튜토리얼 줍는 물건(샷건)에 가까이 갔을 때 테두리에 선이 들어오게 합니다.
    /// 상자(ChestHighlightVisual)와 같은 방식입니다: 대상 스프라이트를 8방향으로 1픽셀씩 밀어 InteractOutline 단색 머티리얼로 뒤에 그리고,
    /// 알파를 사인파로 숨쉬듯 깜빡입니다. 줍기를 받을 수 있는 동안(TutorialPickup.IsActive)에 플레이어가 범위 안에 있을 때만 켜집니다.
    /// 컷씬 도중이나 줍기 전에는 눌러도 반응하지 않으므로 선을 띄우지 않습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TutorialPickupHighlight : MonoBehaviour
    {
        [Tooltip("연동할 줍기 컴포넌트 (비우면 같은 오브젝트에서 찾습니다)")]
        [SerializeField] private TutorialPickup _pickup;

        [Tooltip("테두리를 그릴 스프라이트 (비우면 자식 중 그리기 순서가 가장 높은 것 = 샷건 본체)")]
        [SerializeField] private SpriteRenderer _target;

        [Tooltip("외곽선 렌더러에 적용할 단색 머티리얼 (InteractOutline)")]
        [SerializeField] private Material _outlineMaterial;

        [Tooltip("외곽선 색상 (상자의 미개봉 색과 같은 골드)")]
        [SerializeField] private Color _color = new Color(1f, 0.85f, 0.2f, 1f);

        [SerializeField, Min(1)] private int _pixelThickness = 1;
        [SerializeField] private bool _enablePulse = true;
        [SerializeField, Min(0.1f)] private float _pulseSpeed = 4.5f;
        [SerializeField, Range(0f, 1f)] private float _minAlphaRatio = 0.4f;

        private static readonly Vector2[] Directions =
        {
            new Vector2(0f, 1f), new Vector2(0f, -1f), new Vector2(1f, 0f), new Vector2(-1f, 0f),
            new Vector2(1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(-1f, -1f),
        };

        private GameObject _root;
        private SpriteRenderer[] _outlines;

        private void Awake()
        {
            if (_pickup == null) _pickup = GetComponent<TutorialPickup>();
            if (_target == null) _target = FindBodyRenderer();
            CreateOutlines();
        }

        private void OnDisable()
        {
            if (_root != null) _root.SetActive(false);
        }

        private void Update()
        {
            if (_root == null || _pickup == null) return;

            bool show = _pickup.IsActive && _pickup.PlayerInRange && _target != null && _target.enabled && _target.gameObject.activeInHierarchy;
            if (_root.activeSelf != show)
            {
                _root.SetActive(show);
            }

            if (show)
            {
                Sync();
            }
        }

        private SpriteRenderer FindBodyRenderer()
        {
            SpriteRenderer best = null;
            foreach (var renderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (best == null || renderer.sortingOrder > best.sortingOrder)
                {
                    best = renderer;
                }
            }

            return best;
        }

        private void CreateOutlines()
        {
            if (_target == null) return;

            _root = new GameObject("OutlineContainer");
            _root.transform.SetParent(_target.transform, false);

            _outlines = new SpriteRenderer[Directions.Length];
            float offset = _pixelThickness / PixelsPerUnit();
            for (int i = 0; i < Directions.Length; i++)
            {
                var child = new GameObject("Outline_" + i);
                child.layer = _target.gameObject.layer;
                child.transform.SetParent(_root.transform, false);
                child.transform.localPosition = new Vector3(Directions[i].x * offset, Directions[i].y * offset, 0f);

                var sr = child.AddComponent<SpriteRenderer>();
                if (_outlineMaterial != null) sr.sharedMaterial = _outlineMaterial;
                _outlines[i] = sr;
            }

            Sync();
            _root.SetActive(false);
        }

        // 본체의 스프라이트 · 뒤집기 · 그리기 순서를 따라가고, 알파를 숨쉬듯 바꾼다
        private void Sync()
        {
            float alpha = 1f;
            if (_enablePulse)
            {
                float wave = (Mathf.Sin(Time.time * _pulseSpeed) + 1f) * 0.5f;
                alpha = Mathf.Lerp(_minAlphaRatio, 1f, wave);
            }

            var color = new Color(_color.r, _color.g, _color.b, _color.a * alpha);
            foreach (var sr in _outlines)
            {
                if (sr == null) continue;

                sr.sprite = _target.sprite;
                sr.flipX = _target.flipX;
                sr.flipY = _target.flipY;
                sr.sortingLayerID = _target.sortingLayerID;
                sr.sortingOrder = _target.sortingOrder - 1;
                sr.color = color;
            }
        }

        private float PixelsPerUnit()
        {
            return _target != null && _target.sprite != null && _target.sprite.pixelsPerUnit > 0f ? _target.sprite.pixelsPerUnit : 16f;
        }
    }
}
