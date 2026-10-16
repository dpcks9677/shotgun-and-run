using System;
using UnityEngine;

namespace ShotgunAndRun.Weapon
{
    public class WeaponAudioFeedback : MonoBehaviour
    {
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _shotAudio;
        [SerializeField] private AudioClip _reloadAudio;
        [SerializeField] private AudioClip _pumpAudio;
        [SerializeField] private AudioClip _criticalShotAudio;
        [SerializeField] private AudioClip _emptyAmmoClip;
        [SerializeField, Range(0f, 1f)] private float _reloadVolume = 0.45f;
        [SerializeField, Range(0f, 1f)] private float _pumpVolume = 0.45f;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();

            if (_audioSource == null)
            {
                Debug.LogError("_audioSource가 초기화 되지 않았습니다.");
            }
        }

        private void PlayOneShot(AudioClip clip, float volumeScale = 1f)
        {
            if (_audioSource == null || clip == null)
                return;

            _audioSource.PlayOneShot(clip, volumeScale);
        }

        public void PlayFire()
        {
            float randomPitch = UnityEngine.Random.Range(0.8f, 1.2f);

            _audioSource.pitch = randomPitch;
            PlayOneShot(_shotAudio);
        }

        public void PlayCriticalFire()
        {
            // 일반 사격의 랜덤 피치가 남아 있으면 크리티컬음이 늘어지거나 높아집니다.
            _audioSource.pitch = 1f;
            PlayOneShot(_criticalShotAudio);
        }

        public void PlayReload()
        {
            _audioSource.pitch = 1f;
            PlayOneShot(_reloadAudio, _reloadVolume);
        }

        public void PlayPump()
        {
            _audioSource.pitch = 1f;
            PlayOneShot(_pumpAudio, _pumpVolume);
        }

        public void PlayEmptyAmmo()
        {
            _audioSource.pitch = 1f;
            PlayOneShot(_emptyAmmoClip);
        }
    }
}
