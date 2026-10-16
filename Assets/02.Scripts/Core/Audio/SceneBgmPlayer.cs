using System.Collections;
using UnityEngine;

namespace ShotgunAndRun.Core
{
    [DisallowMultipleComponent]
    public sealed class SceneBgmPlayer : MonoBehaviour
    {
        [SerializeField] private AudioClip _bgmClip;
        [SerializeField] private float _fadeDuration = 0.5f;
        [SerializeField] private bool _loop = true;
        [SerializeField] private bool _stopOnDestroy = true;

        // 씬 전환 연출(검은 화면 · 로딩)이 끝나 플레이어가 보인 뒤에 재생해야 페이드 인이 들린다.
        private IEnumerator Start()
        {
            GameManager manager = GameManager.Instance;
            while (manager != null && manager.IsSceneTransitioning)
            {
                yield return null;
            }

            // 컷씬 등이 컴포넌트를 꺼 두면 재생하지 않고, 끝난 뒤 Play()로 직접 시작한다
            if (enabled)
            {
                Play();
            }
        }

        public void Play()
        {
            if (_bgmClip != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBgm(_bgmClip, _loop, _fadeDuration);
            }
        }

        private void OnDestroy()
        {
            if (_stopOnDestroy && AudioManager.Instance != null && AudioManager.Instance.CurrentBgmClip == _bgmClip)
            {
                AudioManager.Instance.StopBgm(_fadeDuration);
            }
        }
    }
}
