using TMPro;
using UnityEngine;

namespace ShotgunAndRun.Tutorial
{
    /// <summary>
    /// (프로토타입) 튜토리얼 조작 안내 표시기입니다. 월드 공간 텍스트 하나와 그 뒤에 깔리는 배경 판을 켜고 끕니다.
    /// 씬에 놓인 자리(벽)에 고정되어 보이거나, Show에 대상을 주면 그 대상의 머리 위를 따라다닙니다. (플레이어 안내)
    /// 루트는 켜 둔 채 글자(와 판)만 끄므로 TutorialFlow가 키로 찾을 수 있습니다.
    /// </summary>
    public sealed class TutorialWallGuide : MonoBehaviour
    {
        // 글자 둘레 여백(월드 단위). 도트 격자(1/16)에 맞춰 가로 12px · 세로 6px
        private static readonly Vector2 PanelPadding = new Vector2(0.75f, 0.375f);
        private const float PixelsPerUnit = 16f;

        [SerializeField] private string _key;
        [SerializeField] private TMP_Text _text;
        [SerializeField] private SpriteRenderer _panel;
        [SerializeField] private float _followHeight = 2.5f;

        private Transform _follow;

        public string Key => _key;

        private void Awake()
        {
            Hide();
        }

        // 시간이 멈춰도(timeScale 0) 따라가야 하므로 Time.deltaTime 없이 위치만 맞춘다
        private void LateUpdate()
        {
            if (_follow != null)
            {
                transform.position = _follow.position + Vector3.up * _followHeight;
            }
        }

        /// <summary>
        /// 안내를 켭니다. message가 null이면 씬 오브젝트에 적어 둔 문구를 그대로 씁니다.
        /// </summary>
        public void Show(string message, Transform follow = null)
        {
            if (message != null)
            {
                _text.text = message;
            }

            _follow = follow;
            _text.gameObject.SetActive(true);
            _text.ForceMeshUpdate();
            FitPanel();
        }

        public void Hide()
        {
            _follow = null;
            _text.gameObject.SetActive(false);
        }

        // 글자 크기에 판을 맞추고, 글자 · 키캡 하위 메시가 같은 레이어에서 판 위에 그려지게 한다
        // (키캡 스프라이트 하위 메시는 만들어질 때의 레이어를 그대로 갖고 있어 따로 맞춰야 한다)
        private void FitPanel()
        {
            Bounds bounds = _text.textBounds;
            Vector2 size = bounds.size + (Vector3)(PanelPadding * 2f);
            _panel.size = new Vector2(Mathf.Round(size.x * PixelsPerUnit), Mathf.Round(size.y * PixelsPerUnit)) / PixelsPerUnit;
            _panel.transform.localPosition = new Vector3(bounds.center.x, bounds.center.y, 0f);

            var main = _text.GetComponent<Renderer>();
            _panel.sortingLayerID = main.sortingLayerID;
            _panel.sortingOrder = main.sortingOrder - 1;
            foreach (var sub in _text.GetComponentsInChildren<Renderer>(true))
            {
                if (sub != main && sub != _panel)
                {
                    sub.sortingLayerID = main.sortingLayerID;
                    sub.sortingOrder = main.sortingOrder;
                }
            }
        }
    }
}
