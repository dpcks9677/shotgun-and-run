using UnityEngine;

namespace ShotgunAndRun.NPC
{
    // 스프라이트 외곽선. 대상 렌더러 뒤에 같은 스프라이트를 8방향으로 1픽셀씩 밀어 단색으로 그린다. (포탈 기둥 Outline과 같은 방식)
    // 애니메이션으로 스프라이트가 바뀌어도 따라가도록 켜져 있는 동안 매 프레임 스프라이트 · 뒤집기를 맞춘다.
    // 외곽선 렌더러는 실행 중에 만든다. 대상과 같은 SortingGroup 안에서 대상 바로 아래 순서로 그린다.
    public sealed class SpriteOutline : MonoBehaviour
    {
        private static readonly Vector2[] Directions =
        {
            Vector2.up, Vector2.down, Vector2.left, Vector2.right,
            new Vector2(1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(-1f, -1f),
        };

        [SerializeField] private SpriteRenderer _target;
        [Tooltip("알파 컷오프 단색 머티리얼 (NpcOutline). 반투명 그림자는 외곽선에서 빠진다")]
        [SerializeField] private Material _material;
        [SerializeField] private Color _color = Color.white;
        [SerializeField, Min(1)] private int _pixels = 1;

        private SpriteRenderer[] _renderers;
        private bool _visible;

        public bool Visible => _visible;

        private void Awake()
        {
            if (_target == null)
            {
                _target = GetComponent<SpriteRenderer>();
            }

            CreateRenderers();
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (_visible)
            {
                Sync();
            }
        }

        public void SetVisible(bool value)
        {
            _visible = value;
            if (_renderers == null) return;

            if (value)
            {
                Sync();
            }

            foreach (SpriteRenderer outlineRenderer in _renderers)
            {
                outlineRenderer.enabled = value;
            }
        }

        private void CreateRenderers()
        {
            if (_target == null) return;

            _renderers = new SpriteRenderer[Directions.Length];
            for (int index = 0; index < Directions.Length; index++)
            {
                var outlineObject = new GameObject($"Outline_{index}");
                outlineObject.layer = _target.gameObject.layer;
                outlineObject.transform.SetParent(_target.transform, false);

                SpriteRenderer outlineRenderer = outlineObject.AddComponent<SpriteRenderer>();
                outlineRenderer.sharedMaterial = _material;
                outlineRenderer.sortingLayerID = _target.sortingLayerID;
                outlineRenderer.sortingOrder = _target.sortingOrder - 1;
                outlineRenderer.color = _color;
                _renderers[index] = outlineRenderer;
            }
        }

        private void Sync()
        {
            if (_target == null || _renderers == null) return;

            Sprite sprite = _target.sprite;
            float offset = sprite != null ? _pixels / sprite.pixelsPerUnit : 0f;
            for (int index = 0; index < _renderers.Length; index++)
            {
                SpriteRenderer outlineRenderer = _renderers[index];
                outlineRenderer.transform.localPosition = Directions[index] * offset;
                outlineRenderer.sprite = sprite;
                outlineRenderer.flipX = _target.flipX;
                outlineRenderer.flipY = _target.flipY;
                outlineRenderer.color = _color;
            }
        }
    }
}
