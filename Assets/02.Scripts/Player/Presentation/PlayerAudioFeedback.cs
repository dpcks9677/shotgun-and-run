using UnityEngine;

namespace ShotgunAndRun.Player
{
    public class PlayerAudioFeedback : MonoBehaviour
    {
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioSource _walkAudioSource;

        [Header("Clips")]
        [SerializeField] private AudioClip _walkClip;
        [SerializeField] private AudioClip _damageClip;
        [SerializeField] private AudioClip _deathClip;
        [SerializeField] private AudioClip _dashClip;
        [SerializeField] private AudioClip _fireClip;
        [SerializeField] private AudioClip _reloadStartClip;
        [SerializeField] private AudioClip _reloadCompleteClip;
        [SerializeField] private AudioClip _armorBrokenClip;

        [Header("Volume")]
        [SerializeField, Range(0f, 1f)] private float _dashVolume = 0.5f;
        [SerializeField] private Vector2 _walkPitchRange = new Vector2(0.92f, 1.08f);
        [SerializeField] private Vector2 _walkVolumeRange = new Vector2(0.85f, 1f);

        private float _walkBaseVolume = 1f;

        private void Awake()
        {
            if (_walkAudioSource != null)
            {
                _walkBaseVolume = _walkAudioSource.volume;
            }
        }

        public void PlayDamage() => Play(_damageClip);
        public void PlayDead() => Play(_deathClip);
        public void PlayDash() => Play(_dashClip, _dashVolume);
        public void PlayFire() => Play(_fireClip);
        public void PlayReloadStart() => Play(_reloadStartClip);
        public void PlayReloadComplete() => Play(_reloadCompleteClip);
        public void PlayArmorBroken() => Play(_armorBrokenClip);

        private void Play(AudioClip clip, float volumeScale = 1f)
        {
            if (_audioSource == null || clip == null) return;
            _audioSource.PlayOneShot(clip, volumeScale);
        }
        
        public void PlayWalk()
        {
            if (_walkAudioSource == null || _walkClip == null)
            {
                return;
            }

            _walkAudioSource.Stop();
            _walkAudioSource.clip = _walkClip;
            _walkAudioSource.loop = false;
            _walkAudioSource.pitch = Random.Range(_walkPitchRange.x, _walkPitchRange.y);
            _walkAudioSource.volume = _walkBaseVolume * Random.Range(_walkVolumeRange.x, _walkVolumeRange.y);
            _walkAudioSource.Play();
        }
        
        public void StopWalk()
        {
            if (_walkAudioSource != null && _walkAudioSource.isPlaying)
            {
                _walkAudioSource.Stop();
            }
        }

        private void OnDisable()
        {
            StopWalk();
        }
    }
}
