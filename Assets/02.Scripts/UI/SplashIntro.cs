using System.Collections;
using ShotgunAndRun.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShotgunAndRun.UI
{
    /// <summary>
    /// 게임 시작 시 팀 로고 연출: 검은 화면 → 로고와 효과음이 함께 등장 → 잠시 유지 → 페이드 아웃 → 타이틀 씬.
    /// </summary>
    public sealed class SplashIntro : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _logo;
        [SerializeField] private AudioClip _sfx;
        [SerializeField] private string _nextScene = "TitleScene";
        [SerializeField] private float _blackDelay = 0.5f;
        [SerializeField] private float _holdDuration = 2f;
        [SerializeField] private float _fadeOutDuration = 0.25f;

        private IEnumerator Start()
        {
            _logo.alpha = 0f;
            yield return new WaitForSecondsRealtime(_blackDelay);

            _logo.alpha = 1f;
            AudioManager.Instance.PlaySfx(_sfx);
            yield return new WaitForSecondsRealtime(_holdDuration);

            for (float time = 0f; time < _fadeOutDuration; time += Time.unscaledDeltaTime)
            {
                _logo.alpha = 1f - time / _fadeOutDuration;
                yield return null;
            }

            _logo.alpha = 0f;
            SceneManager.LoadScene(_nextScene);
        }
    }
}
