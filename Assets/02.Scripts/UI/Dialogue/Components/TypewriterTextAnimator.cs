using System;
using System.Collections;
using ShotgunAndRun.Core;
using TMPro;
using UnityEngine;

namespace ShotgunAndRun.UI.Dialogue
{
    /// <summary>
    /// TextMeshPro 텍스트를 한 글자씩 타자 치듯 출력하는 타이핑 연출 컴포넌트 (SRP, ITextAnimator 구현)
    /// </summary>
    [DisallowMultipleComponent]
    public class TypewriterTextAnimator : MonoBehaviour, ITextAnimator
    {
        [Tooltip("글자 출력 간격 (초 단위, 값이 작을수록 빠름)")]
        [SerializeField, Range(0.005f, 0.1f)] private float _charInterval = 0.03f;

        [Tooltip("글자가 찍힐 때 재생할 타이핑 SFX (비어 있으면 무음)")]
        [SerializeField] private AudioClip _typeSfx;
        [Tooltip("몇 글자마다 SFX를 재생할지 (1이면 매 글자)")]
        [SerializeField, Range(1, 5)] private int _sfxEveryNChars = 2;

        private Coroutine _routine;
        private string _targetContent = string.Empty;
        private bool _isAnimating;

        public bool IsAnimating => _isAnimating;

        public void AnimateText(TextMeshProUGUI targetText, string fullContent, Action onComplete)
        {
            Stop();

            _targetContent = fullContent ?? string.Empty;

            if (targetText == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (gameObject.activeInHierarchy)
            {
                _routine = StartCoroutine(TypeRoutine(targetText, _targetContent, onComplete));
            }
            else
            {
                targetText.text = _targetContent;
                targetText.maxVisibleCharacters = _targetContent.Length;
                _isAnimating = false;
                onComplete?.Invoke();
            }
        }

        public void CompleteImmediately(TextMeshProUGUI targetText, Action onComplete)
        {
            if (!_isAnimating) return;

            Stop();

            if (targetText != null)
            {
                targetText.text = _targetContent;
                targetText.maxVisibleCharacters = _targetContent.Length;
            }

            onComplete?.Invoke();
        }

        public void Stop()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
            _isAnimating = false;
        }

        private IEnumerator TypeRoutine(TextMeshProUGUI targetText, string content, Action onComplete)
        {
            _isAnimating = true;
            targetText.text = content;
            targetText.maxVisibleCharacters = 0;

            int totalChars = content.Length;
            int visibleChars = 0;

            while (visibleChars < totalChars)
            {
                visibleChars++;
                targetText.maxVisibleCharacters = visibleChars;
                PlayTypeSfx(content[visibleChars - 1], visibleChars);

                // Time.timeScale=0 (튜토리얼/일시정지) 상황에서도 정상 작동
                yield return new WaitForSecondsRealtime(_charInterval);
            }

            targetText.maxVisibleCharacters = totalChars;
            _isAnimating = false;
            _routine = null;

            onComplete?.Invoke();
        }

        private void PlayTypeSfx(char c, int charIndex)
        {
            if (_typeSfx == null || char.IsWhiteSpace(c) || (charIndex - 1) % _sfxEveryNChars != 0)
                return;

            AudioManager.Instance.PlaySfx(_typeSfx);
        }
    }
}
