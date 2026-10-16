using System;
using System.Collections;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    // 사운드 명령: 효과음 · 배경음. 공용 오디오 매니저가 생기기 전까지 Director의 AudioSource로 재생합니다.
    // 컷씬이 끝나면 컷씬 배경음은 멈춥니다.
    public sealed partial class CutsceneContext
    {
        private const string BgmOwner = "bgm";

        public void PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            _director.SfxSource.PlayOneShot(clip, volume);
        }

        /// <summary>
        /// 효과음을 startDelay초 뒤부터 interval초 간격으로 count번 연달아 재생합니다. (예: 멀리서 들리는 총성) 핸들은 마지막 재생 때 끝납니다.
        /// </summary>
        public CutsceneHandle PlaySfxBurst(AudioClip clip, int count, float interval, float volume = 1f, float startDelay = 0f)
        {
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            return Run(SfxBurstRoutine(clip, count, interval, volume, startDelay));
        }

        /// <summary>
        /// 배경음을 fadeSeconds초 동안 키우며 재생합니다.
        /// </summary>
        public CutsceneHandle PlayBgm(AudioClip clip, float fadeSeconds = 1f)
        {
            if (clip == null) throw new ArgumentNullException(nameof(clip));

            var source = _director.BgmSource;
            source.clip = clip;
            source.loop = true;
            source.volume = 0f;
            source.Play();
            return Run(VolumeRoutine(0f, _bgmVolume, fadeSeconds, stopAtEnd: false), BgmOwner);
        }

        /// <summary>
        /// 배경음을 fadeSeconds초 동안 줄이고 멈춥니다.
        /// </summary>
        public CutsceneHandle StopBgm(float fadeSeconds = 1f)
        {
            return Run(VolumeRoutine(_director.BgmSource.volume, 0f, fadeSeconds, stopAtEnd: true), BgmOwner);
        }

        private IEnumerator SfxBurstRoutine(AudioClip clip, int count, float interval, float volume, float startDelay)
        {
            if (startDelay > 0f)
            {
                yield return WaitRoutine(startDelay);
            }

            for (int i = 0; i < count; i++)
            {
                PlaySfx(clip, volume);
                if (i < count - 1)
                {
                    yield return WaitRoutine(interval);
                }
            }
        }

        private IEnumerator VolumeRoutine(float from, float to, float seconds, bool stopAtEnd)
        {
            var source = _director.BgmSource;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                source.volume = Mathf.Lerp(from, to, elapsed / seconds);
                yield return null;
                elapsed += Time.deltaTime;
            }

            source.volume = to;
            if (stopAtEnd)
            {
                source.Stop();
                source.volume = _bgmVolume;
            }
        }
    }
}
