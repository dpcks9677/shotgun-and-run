using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 인물 머리 위에 대사 한 줄을 텍스트 말풍선으로 띄웁니다. 컷씬이 재생 중이 아니어도 쓸 수 있습니다.
    /// 템플릿은 Screen Space - Overlay 캔버스 아래의 비활성 UI 오브젝트(배경 Image + TMP_Text)이며, 대상마다 복제해 씁니다.
    /// 머리 높이는 EmotionBubbleDisplay와 같은 규칙으로 정합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpeechBubbleDisplay : MonoBehaviour
    {
        [Tooltip("말풍선 템플릿. Overlay 캔버스 아래의 비활성 오브젝트이며, 자손에 TMP_Text가 있어야 한다 (피벗은 하단 중앙 권장)")]
        [SerializeField] private RectTransform _template;

        [Header("위치")]
        [Tooltip("머리 위와 말풍선 하단 사이의 간격(월드 단위)")]
        [SerializeField] private float _tailSpacing = 0.3f;

        [Tooltip("머리 위치를 찾지 못했을 때 쓰는, 대상 기준점에서 머리 꼭대기까지의 높이")]
        [SerializeField] private float _defaultHeadHeight = 0.65f;

        private sealed class Bubble
        {
            public GameObject Root;
            public Coroutine Routine;
        }

        private readonly Dictionary<Transform, Bubble> _bubbles = new Dictionary<Transform, Bubble>();

        /// <summary>
        /// 대상 머리 위에 말풍선을 seconds 동안 띄웁니다. 대상에 이미 말풍선이 있으면 즉시 교체합니다.
        /// </summary>
        public void Show(Transform target, string text, float seconds)
        {
            if (target == null || string.IsNullOrEmpty(text)) return;

            // 비활성 상태에서는 코루틴을 시작할 수 없어 복제본만 남는다
            if (!isActiveAndEnabled)
            {
                Debug.LogWarning("[SpeechBubbleDisplay] 비활성 상태라 말풍선을 띄우지 않습니다.", this);
                return;
            }

            if (_template == null || _template.GetComponentInChildren<TMP_Text>(true) == null)
            {
                Debug.LogError("[SpeechBubbleDisplay] 말풍선 템플릿(TMP_Text 포함)이 없습니다.", this);
                return;
            }

            Hide(target);

            var root = Instantiate(_template.gameObject, _template.parent);
            root.name = $"SpeechBubble_{target.name}";
            root.GetComponentInChildren<TMP_Text>(true).text = text;
            root.SetActive(true);

            var bubble = new Bubble { Root = root };
            bubble.Routine = StartCoroutine(ShowRoutine(target, (RectTransform)root.transform, seconds, bubble));
            _bubbles[target] = bubble;
        }

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

        private void UpdatePosition(Transform target, RectTransform rect)
        {
            var camera = Camera.main;
            if (camera == null) return;

            var world = new Vector3(target.position.x, EmotionBubbleDisplay.GetHeadY(target, _defaultHeadHeight) + _tailSpacing, target.position.z);
            rect.position = camera.WorldToScreenPoint(world);
        }

        // EmotionBubbleDisplay와 같이 Time.deltaTime(Time.timeScale 영향)으로 시간을 잰다
        private IEnumerator ShowRoutine(Transform target, RectTransform rect, float seconds, Bubble bubble)
        {
            float elapsed = 0f;
            while (target != null && elapsed < seconds)
            {
                UpdatePosition(target, rect);
                yield return null;
                elapsed += Time.deltaTime;
            }

            Destroy(bubble.Root);
            if (_bubbles.TryGetValue(target, out var current) && current == bubble)
            {
                _bubbles.Remove(target);
            }
        }

        private void OnDisable()
        {
            HideAll();
        }
    }
}
