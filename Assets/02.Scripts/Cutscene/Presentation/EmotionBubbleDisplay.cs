using System.Collections;
using System.Collections.Generic;
using ShotgunAndRun.UI.Dialogue;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 인물 머리 위에 감정 말풍선을 픽셀 계단식 애니메이션으로 띄웁니다.
    /// 머리 높이는 대상의 'Head' · 'HeadPoint' · 'EmotionPoint' 자식, 신체 콜라이더 윗변, 기본 머리 높이 순서로 정합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EmotionBubbleDisplay : MonoBehaviour
    {
        [Tooltip("감정별 말풍선 스프라이트")]
        [SerializeField] private EmotionBubbleSetSO _sprites;

        [Header("렌더링")]
        [SerializeField] private string _sortingLayerName = "UI";
        [SerializeField] private int _sortingOrder = 500;

        [Header("위치")]
        [Tooltip("머리 위와 말풍선 꼬리(하단 피벗) 사이의 간격")]
        [SerializeField] private float _tailSpacing = 0.3f;

        [Tooltip("머리 위치를 찾지 못했을 때 쓰는, 대상 기준점에서 머리 꼭대기까지의 높이")]
        [SerializeField] private float _defaultHeadHeight = 0.65f;

        [SerializeField] private float _horizontalOffset;

        [Header("연출")]
        [SerializeField, Range(0.5f, 2.5f)] private float _bubbleScale = 1.35f;
        [SerializeField] private float[] _popInScaleSteps = { 0.25f, 0.50f, 0.75f, 1.15f, 1.0f };
        [SerializeField, Min(0.01f)] private float _popInStepDuration = 0.045f;
        [SerializeField] private float[] _popOutScaleSteps = { 0.70f, 0.35f };
        [SerializeField, Min(0.01f)] private float _popOutStepDuration = 0.04f;

        private sealed class Bubble
        {
            public GameObject Root;
            public Coroutine Routine;
            public bool DismissRequested;
        }

        private readonly Dictionary<Transform, Bubble> _bubbles = new Dictionary<Transform, Bubble>();

        public EmotionBubbleSetSO Sprites => _sprites;

        /// <summary>
        /// 대상 머리 위에 말풍선을 띄웁니다. duration이 무한대면 Dismiss 할 때까지 유지합니다.
        /// 대상에 이미 말풍선이 있으면 즉시 교체합니다.
        /// </summary>
        public void Show(Transform target, EmotionType emotion, float duration)
        {
            if (target == null || emotion == EmotionType.None) return;

            Sprite sprite = _sprites != null ? _sprites.Get(emotion) : null;
            if (sprite == null)
            {
                Debug.LogError($"[EmotionBubbleDisplay] {emotion} 말풍선 스프라이트가 없습니다.", this);
                return;
            }

            Hide(target);

            var bubble = new Bubble();
            bubble.Routine = StartCoroutine(PopupRoutine(target, sprite, duration, bubble));
            _bubbles[target] = bubble;
        }

        /// <summary>
        /// 퇴장 애니메이션과 함께 닫습니다.
        /// </summary>
        public void Dismiss(Transform target)
        {
            if (target != null && _bubbles.TryGetValue(target, out var bubble))
            {
                bubble.DismissRequested = true;
            }
        }

        public void DismissAll()
        {
            foreach (var bubble in _bubbles.Values)
            {
                bubble.DismissRequested = true;
            }
        }

        /// <summary>
        /// 애니메이션 없이 즉시 닫습니다.
        /// </summary>
        public void Hide(Transform target)
        {
            if (target == null || !_bubbles.TryGetValue(target, out var bubble)) return;

            DestroyBubble(bubble);
            _bubbles.Remove(target);
        }

        public void HideAll()
        {
            foreach (var bubble in _bubbles.Values)
            {
                DestroyBubble(bubble);
            }
            _bubbles.Clear();
        }

        private void DestroyBubble(Bubble bubble)
        {
            if (bubble.Routine != null) StopCoroutine(bubble.Routine);
            if (bubble.Root != null) Destroy(bubble.Root);
        }

        // 머리 위 퀘스트 마커(CutsceneNpcDialogue)도 대화 말풍선과 같은 자리에 두려고 이 위치를 쓴다
        internal Vector3 GetBubblePosition(Transform target)
        {
            return new Vector3(target.position.x + _horizontalOffset, GetHeadY(target) + _tailSpacing, target.position.z);
        }

        private float GetHeadY(Transform target) => GetHeadY(target, _defaultHeadHeight);

        // SpeechBubbleDisplay도 같은 머리 위치 규칙을 쓴다
        internal static float GetHeadY(Transform target, float defaultHeadHeight)
        {
            var anchor = target.Find("Head") ?? target.Find("HeadPoint") ?? target.Find("EmotionPoint");
            if (anchor != null) return anchor.position.y;

            // 원점보다 위에 있는 신체 콜라이더의 윗변을 머리로 본다 (발밑 판정용 콜라이더는 제외)
            float top = float.MinValue;
            foreach (var bodyCollider in target.GetComponentsInChildren<Collider2D>())
            {
                if (!bodyCollider.enabled) continue;
                float colliderTop = bodyCollider.bounds.max.y;
                if (colliderTop > target.position.y + 0.35f && colliderTop < target.position.y + 1.8f)
                {
                    top = Mathf.Max(top, colliderTop);
                }
            }

            return top > float.MinValue ? top : target.position.y + defaultHeadHeight;
        }

        private IEnumerator PopupRoutine(Transform target, Sprite sprite, float duration, Bubble bubble)
        {
            var root = new GameObject($"EmotionBubble_{sprite.name}_{target.name}");
            bubble.Root = root;
            root.transform.position = GetBubblePosition(target);
            root.transform.localScale = Vector3.zero;

            var spriteRenderer = root.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingLayerName = _sortingLayerName;
            spriteRenderer.sortingOrder = _sortingOrder;

            bool persistent = float.IsPositiveInfinity(duration);
            float animationTime = _popInScaleSteps.Length * _popInStepDuration + _popOutScaleSteps.Length * _popOutStepDuration;
            float holdDuration = persistent ? float.PositiveInfinity : Mathf.Max(0.2f, duration - animationTime);

            foreach (float step in _popInScaleSteps)
            {
                root.transform.localScale = Vector3.one * (_bubbleScale * step);
                yield return Follow(target, root, _popInStepDuration, null);
            }

            root.transform.localScale = Vector3.one * _bubbleScale;
            yield return Follow(target, root, holdDuration, bubble);

            foreach (float step in _popOutScaleSteps)
            {
                root.transform.localScale = Vector3.one * (_bubbleScale * step);
                yield return Follow(target, root, _popOutStepDuration, null);
            }

            Destroy(root);
            if (_bubbles.TryGetValue(target, out var current) && current == bubble)
            {
                _bubbles.Remove(target);
            }
        }

        // seconds 동안(또는 dismissible이 닫힐 때까지) 말풍선이 대상 머리 위를 따라가게 한다
        private IEnumerator Follow(Transform target, GameObject root, float seconds, Bubble dismissible)
        {
            float elapsed = 0f;
            while (elapsed < seconds && (dismissible == null || !dismissible.DismissRequested))
            {
                if (target != null) root.transform.position = GetBubblePosition(target);
                yield return null;
                elapsed += Time.deltaTime;
            }
        }

        private void OnDisable()
        {
            HideAll();
        }
    }
}
