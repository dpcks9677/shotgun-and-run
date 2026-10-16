using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// HUD 숨김이 켜진 컷씬이 재생되는 동안 이 UI를 숨깁니다.
    /// SetActive 대신 CanvasGroup으로 숨겨서, 대상 UI의 OnEnable/OnDisable 이벤트 구독이 끊기지 않게 합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class HideDuringCutscene : MonoBehaviour
    {
        [Tooltip("컷씬이 끝나고 다시 나타날 때 페이드인 시간(초)")]
        [SerializeField] private float _fadeInDuration = 0.3f;

        private CanvasGroup _canvasGroup;
        private bool _isHidden;
        private bool _isFadingIn;
        private float _fadeProgress;
        private float _savedAlpha;
        private bool _savedInteractable;
        private bool _savedBlocksRaycasts;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        private void LateUpdate()
        {
            bool shouldHide = CutsceneDirector.IsHudHidden;
            if (shouldHide != _isHidden)
            {
                SetHidden(shouldHide);
            }

            if (_isFadingIn)
            {
                _fadeProgress = Mathf.MoveTowards(_fadeProgress, 1f, Time.unscaledDeltaTime / _fadeInDuration);
                _canvasGroup.alpha = _savedAlpha * _fadeProgress;
                _isFadingIn = _fadeProgress < 1f;
            }
        }

        private void OnDisable()
        {
            if (_isHidden)
            {
                SetHidden(false);
            }

            // 비활성화되면 페이드가 멈추므로 원래 알파로 즉시 복원
            if (_isFadingIn)
            {
                _isFadingIn = false;
                _canvasGroup.alpha = _savedAlpha;
            }
        }

        private void SetHidden(bool hidden)
        {
            _isHidden = hidden;
            if (hidden)
            {
                // 페이드인 도중 다시 숨겨지면 중간 알파가 아닌 기존 저장값을 유지한다
                if (!_isFadingIn)
                {
                    _savedAlpha = _canvasGroup.alpha;
                    _savedInteractable = _canvasGroup.interactable;
                    _savedBlocksRaycasts = _canvasGroup.blocksRaycasts;
                }

                _isFadingIn = false;
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
                return;
            }

            _canvasGroup.interactable = _savedInteractable;
            _canvasGroup.blocksRaycasts = _savedBlocksRaycasts;
            _fadeProgress = 0f;
            _isFadingIn = true;
        }
    }
}
