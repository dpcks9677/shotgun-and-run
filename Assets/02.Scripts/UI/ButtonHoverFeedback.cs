using ShotgunAndRun.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShotgunAndRun.UI
{
    [DisallowMultipleComponent]
    public class ButtonHoverFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField, Range(0f, 1f)]
        private float _hoverVolume = 0.15f;

        [SerializeField, Range(0f, 1f)]
        private float _clickVolume = 0.25f;

        [SerializeField] private TMP_Text _targetText;
        [SerializeField] private AudioClip _hoverSfx;
        [SerializeField] private AudioClip _clickSfx;

        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _hoverColor = new Color(1f, 0.9f, 0.25f, 1f);
        [SerializeField] private Color _disabledColor = new Color(0.196f, 0.196f, 0.196f, 1f);

        // 호버 시 글자 양옆에 띄우는 문자. 글자 위치는 그대로 두고 따로 띄웁니다.
        // Mulmaru 폰트에 없는 글리프(▶ ◀)는 쓸 수 없습니다.
        [SerializeField] private string _leftArrow = ">";
        [SerializeField] private string _rightArrow = "<";
        [SerializeField] private float _arrowGap = 3f;

        // 켜면 저장 파일이 있을 때만 버튼을 활성화합니다. (타이틀의 Load game)
        [SerializeField] private bool _requiresSave;

        private Selectable _selectable;
        private Button _button;
        private TMP_Text _leftArrowText;
        private TMP_Text _rightArrowText;

        private void Awake()
        {
            _selectable = GetComponent<Selectable>();
            _button = GetComponent<Button>();
            if (_targetText == null)
            {
                _targetText = GetComponentInChildren<TMP_Text>();
            }

            _leftArrowText = CreateArrow(_leftArrow);
            _rightArrowText = CreateArrow(_rightArrow);

            if (_button != null)
            {
                _button.onClick.AddListener(PlayClickSfx);
            }
        }

        private void Start()
        {
            if (_requiresSave && _selectable != null)
            {
                GameManager gameManager = GameManager.Instance;
                _selectable.interactable = gameManager != null && gameManager.HasSave;
            }

            ResetColor();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_selectable != null && !_selectable.interactable)
            {
                return;
            }

            PlaySfx(_hoverSfx, _hoverVolume);

            if (_targetText != null)
            {
                _targetText.color = _hoverColor;
                ShowArrows();
            }
        }

        public void SetAudio(
            AudioClip hoverSfx,
            AudioClip clickSfx,
            float hoverVolume,
            float clickVolume)
        {
            _hoverSfx = hoverSfx;
            _clickSfx = clickSfx;
            _hoverVolume = hoverVolume;
            _clickVolume = clickVolume;
        }

        private void PlayClickSfx()
        {
            if (CanInteract())
            {
                PlaySfx(_clickSfx, _clickVolume);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ResetColor();
        }

        private bool CanInteract()
        {
            return _selectable == null || _selectable.interactable;
        }

        private static void PlaySfx(AudioClip clip, float volume)
        {
            if (clip != null)
            {
                AudioManager.Instance?.PlaySfx(clip, volume);
            }
        }


        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(PlayClickSfx);
            }
        }

        private void OnDisable()
        {
            ResetColor();
        }

        private void ResetColor()
        {
            if (_targetText != null)
            {
                bool isEnabled = _selectable == null || _selectable.interactable;
                _targetText.color = isEnabled ? _normalColor : _disabledColor;

                // 씬이 닫히며 OnDisable이 불릴 때는 화살표가 이미 파괴됐을 수 있습니다.
                if (_leftArrowText != null)
                {
                    _leftArrowText.gameObject.SetActive(false);
                    _rightArrowText.gameObject.SetActive(false);
                }
            }
        }

        // 글자와 같은 설정으로 복제한 화살표 텍스트입니다.
        // 글자는 버튼 전체를 채우므로 글자의 형제로 두면 글자와 같은 중심 기준으로 배치할 수 있습니다.
        private TMP_Text CreateArrow(string symbol)
        {
            if (_targetText == null)
            {
                return null;
            }

            TMP_Text arrow = Instantiate(_targetText, _targetText.transform.parent);
            arrow.name = "Arrow";
            arrow.text = symbol;
            arrow.raycastTarget = false;
            arrow.alignment = TextAlignmentOptions.Center;
            arrow.textWrappingMode = TextWrappingModes.NoWrap;
            arrow.gameObject.SetActive(false);

            RectTransform rect = arrow.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            return arrow;
        }

        private void ShowArrows()
        {
            _targetText.ForceMeshUpdate();
            Bounds bounds = _targetText.textBounds;
            PlaceArrow(_leftArrowText, bounds.min.x - _arrowGap, -1f);
            PlaceArrow(_rightArrowText, bounds.max.x + _arrowGap, 1f);
        }

        // edge에서 direction 방향으로 화살표 폭만큼 떨어진 곳에 둡니다.
        private void PlaceArrow(TMP_Text arrow, float edge, float direction)
        {
            RectTransform rect = arrow.rectTransform;
            rect.sizeDelta = new Vector2(arrow.preferredWidth, _targetText.rectTransform.rect.height);
            rect.anchoredPosition = new Vector2(edge + direction * rect.sizeDelta.x * 0.5f, 0f);
            arrow.color = _hoverColor;
            arrow.gameObject.SetActive(true);
        }

#if UNITY_EDITOR
        private void Reset()
        {
            _targetText = GetComponentInChildren<TMP_Text>();
            if (_targetText != null)
            {
                _normalColor = _targetText.color;
            }
        }
#endif
    }
}
