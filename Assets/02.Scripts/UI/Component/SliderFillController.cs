using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ShotgunAndRun.UI
{
    public sealed class SliderFillController : MonoBehaviour
    {
        [SerializeField] private Slider _slider;

        private Coroutine _fillCoroutine;

        private void Awake()
        {
            _slider ??= GetComponent<Slider>();
        }

        public void StartFill(float duration)
        {
            StopFill();

            if (_slider == null)
            {
                return;
            }

            duration = Mathf.Max(0f, duration);
            _slider.value = 0f;

            if (duration <= 0f)
            {
                _slider.value = 1f;
                return;
            }

            _fillCoroutine = StartCoroutine(FillRoutine(duration));
        }

        // 슬라이더 코루틴을 멈추고 Slider를 지정한 값으로
        public void StopFill()
        {
            if (_fillCoroutine != null)
            {
                StopCoroutine(_fillCoroutine);
                _fillCoroutine = null;
            }

            if (_slider != null)
            {
                _slider.value = Mathf.Clamp01(0f);
            }
        }

        private IEnumerator FillRoutine(float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _slider.value = Mathf.Clamp01(elapsed / duration);

                yield return null;
            }

            _slider.value = 1f;
            _fillCoroutine = null;
        }

        private void OnDisable()
        {
            StopFill();
        }
    }
}
