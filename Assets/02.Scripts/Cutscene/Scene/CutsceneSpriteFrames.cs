using System.Collections;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 스프라이트 프레임을 한 번 재생하고 마지막 프레임에 멈춥니다. (예: 쉘터 입구 아치 붕괴)
    /// 평소(씬 로드)에는 SpriteRenderer에 넣어 둔 그림(마지막 프레임)을 그대로 보여 주고,
    /// 컷씬 도중에는 CutsceneSignal의 UnityEvent로 Play()를 불러 처음부터 재생합니다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class CutsceneSpriteFrames : MonoBehaviour
    {
        [SerializeField] private Sprite[] _frames;
        [SerializeField] private float _frameSeconds = 0.2f;

        private SpriteRenderer _renderer;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        // 재생 도중 꺼져도 중간 프레임에 멈춰 있지 않게 한다
        private void OnDisable()
        {
            _renderer.sprite = _frames[_frames.Length - 1];
        }

        public void Play()
        {
            StopAllCoroutines();
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            var wait = new WaitForSeconds(_frameSeconds);
            foreach (var frame in _frames)
            {
                _renderer.sprite = frame;
                yield return wait;
            }
        }
    }
}
