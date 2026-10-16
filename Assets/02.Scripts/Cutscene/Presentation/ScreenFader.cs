using UnityEngine;
using UnityEngine.UI;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 화면 전체 페이드 · 점멸용 덮개와 위아래 레터박스(검은 띠)입니다. 시작할 때 화면 UI를 직접 만듭니다.
    /// 덮개 · 레터박스 모두 대화창 아래에 그려지도록 정렬 순서를 나눕니다. 화면이 검은 채로 나오는 대사(암전 중 비명 · 혼잣말)도 읽을 수 있어야 하기 때문입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScreenFader : MonoBehaviour
    {
        [Tooltip("페이드 덮개 캔버스 정렬 순서 (대화창보다 작게: 화면이 검은 동안에도 대화창이 보임. 레터박스보다는 크게)")]
        [SerializeField]
        private int _overlaySortingOrder = 95;

        [Tooltip("레터박스 캔버스 정렬 순서 (대화창보다 작게: 대화창이 띠 위에 보임)")]
        [SerializeField]
        private int _letterboxSortingOrder = 90;

        [Tooltip("레터박스 띠 하나의 높이 (화면 높이 비율)")]
        [SerializeField, Range(0.02f, 0.3f)]
        private float _letterboxHeight = 0.12f;

        [Tooltip("켜면 씬이 뜨는 첫 프레임부터 화면을 검게 덮고 시작합니다. 컷씬이 시작해 페이드인으로 이어받습니다. (컷씬이 안 도는 경우엔 이 씬 쪽에서 직접 걷어야 합니다)")]
        [SerializeField]
        private bool _startBlack;

        private Image _overlay;
        private RectTransform _topBar;
        private RectTransform _bottomBar;

        public float OverlayAlpha => _overlay.color.a;
        public Color OverlayColor => _overlay.color;
        public float LetterboxAmount
        {
            get;
            private set;
        }

        private void Awake()
        {
            _overlay = CreateCanvas("FadeOverlay", _overlaySortingOrder).gameObject.AddComponent<Image>();
            _overlay.raycastTarget = false;
            Stretch(_overlay.rectTransform, 0f, 1f);

            Transform letterbox = CreateCanvas("Letterbox", _letterboxSortingOrder);
            _topBar = CreateBar(letterbox, "Top");
            _bottomBar = CreateBar(letterbox, "Bottom");

            ResetAll();
            if (_startBlack)
            {
                SetOverlay(Color.black, 1f);
            }
        }

        public void SetOverlay(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            _overlay.color = color;
            _overlay.enabled = color.a > 0f;
        }

        /// <summary>
        /// 0이면 띠 없음, 1이면 띠가 다 내려온 상태입니다.
        /// </summary>
        public void SetLetterbox(float amount)
        {
            LetterboxAmount = Mathf.Clamp01(amount);
            float height = _letterboxHeight * LetterboxAmount;
            Stretch(_topBar, 1f - height, 1f);
            Stretch(_bottomBar, 0f, height);
            _topBar.gameObject.SetActive(LetterboxAmount > 0f);
            _bottomBar.gameObject.SetActive(LetterboxAmount > 0f);
        }

        public void ResetAll()
        {
            SetOverlay(Color.black, 0f);
            SetLetterbox(0f);
        }

        private Transform CreateCanvas(string objectName, int sortingOrder)
        {
            var canvasObject = new GameObject(objectName, typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            return canvasObject.transform;
        }

        private static RectTransform CreateBar(Transform parent, string objectName)
        {
            var bar = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(parent, false);
            var image = bar.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;
            return image.rectTransform;
        }

        private static void Stretch(RectTransform rect, float minY, float maxY)
        {
            rect.anchorMin = new Vector2(0f, minY);
            rect.anchorMax = new Vector2(1f, maxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
