using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShotgunAndRun.Core
{
    // 씬 전환 연출: 페이드 아웃 → 로딩 화면 → 새 씬 활성화 → 페이드 인
    // GameManager 프리팹의 자식이라 씬이 바뀌어도 유지되며, 결과창처럼 timeScale이 0이어도 멈추지 않습니다.
    [DisallowMultipleComponent]
    public sealed class SceneTransition : MonoBehaviour
    {
        [Tooltip("화면 전체를 덮는 검은 덮개")]
        [SerializeField] private CanvasGroup _fade;

        [Tooltip("화면이 검은 동안 보여 줄 로딩 표시 (우하단 Loading...)")]
        [SerializeField] private GameObject _loading;

        [Tooltip("Loading 뒤의 점들. 앞에서부터 차례로 튀어 오릅니다")]
        [SerializeField] private RectTransform[] _dots;

        [SerializeField] private float _fadeDuration = 0.4f;

        [Tooltip("로딩이 빨리 끝나도 로딩 화면을 보여 줄 최소 시간")]
        [SerializeField] private float _minLoadingTime = 0.5f;

        [SerializeField] private float _dotBounceHeight = 8f;
        [SerializeField] private float _dotBouncePeriod = 0.9f;

        private float[] _dotBaseY;

        private void Awake()
        {
            _dotBaseY = new float[_dots.Length];
            for (int i = 0; i < _dots.Length; i++)
            {
                _dotBaseY[i] = _dots[i].anchoredPosition.y;
            }

            _fade.alpha = 0f;
            _fade.blocksRaycasts = false;
            _loading.SetActive(false);
        }

        private void Update()
        {
            if (!_loading.activeSelf)
            {
                return;
            }

            // 점마다 주기의 앞쪽 절반 안에서 시작을 늦춰 물결처럼 차례로 튀고, 나머지 절반은 모두 쉽니다.
            float cycle = Time.unscaledTime / _dotBouncePeriod;
            for (int i = 0; i < _dots.Length; i++)
            {
                float phase = cycle - i * 0.5f / _dots.Length;
                float bounce = Mathf.Max(0f, Mathf.Sin(phase * 2f * Mathf.PI));
                Vector2 position = _dots[i].anchoredPosition;
                position.y = _dotBaseY[i] + bounce * _dotBounceHeight;
                _dots[i].anchoredPosition = position;
            }
        }

        // 씬 전환 전에 화면만 먼저 어둡게 합니다. 이어서 Play를 실행하면 이미 어두우므로 페이드 아웃을 건너뜁니다.
        public IEnumerator FadeOut(float duration)
        {
            _fade.blocksRaycasts = true;
            yield return Fade(0f, 1f, duration);
        }

        // GameManager가 코루틴으로 실행합니다. 새 씬이 활성화되는 순간 sceneLoaded가 호출됩니다.
        public IEnumerator Play(string sceneName)
        {
            _fade.blocksRaycasts = true;
            if (_fade.alpha < 1f)
            {
                yield return Fade(0f, 1f, _fadeDuration);
            }

            _loading.SetActive(true);

            float shownAt = Time.unscaledTime;
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
            operation.allowSceneActivation = false;

            // allowSceneActivation이 false면 진행률은 0.9에서 멈춥니다.
            while (operation.progress < 0.9f || Time.unscaledTime - shownAt < _minLoadingTime)
            {
                yield return null;
            }

            operation.allowSceneActivation = true;
            yield return operation;

            _loading.SetActive(false);
            yield return Fade(1f, 0f, _fadeDuration);
            _fade.blocksRaycasts = false;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
            {
                _fade.alpha = Mathf.Lerp(from, to, time / duration);
                yield return null;
            }

            _fade.alpha = to;
        }
    }
}
