using UnityEngine;

namespace ShotgunAndRun.Enemy
{
    public class EnemySound : MonoBehaviour
    {
        private const float AudibleHalfExtent = 15f;

        [SerializeField] private AudioClip[] _idleClips;
        [SerializeField] private Vector2 _idleCooldownRange = new Vector2(2f, 4f);
        [SerializeField] private AudioClip _moveClip;
        [SerializeField] private AudioClip[] _moveClips;
        [SerializeField] private bool _randomMoveClips;
        [SerializeField] private AudioClip _detectClip;
        [SerializeField] private float _detectVolume = 1f;
        [SerializeField] private bool _playDetectionSounds;
        [SerializeField] private AudioClip[] _attackClips;
        [SerializeField] private AudioClip[] _drawClips;
        [SerializeField] private AudioClip[] _shotClips;
        [SerializeField] private AudioClip _throwClip;
        [SerializeField] private AudioClip _teleportOutClip;
        [SerializeField] private AudioClip _teleportInClip;
        [SerializeField] private Vector2 _throwPitchRange = new Vector2(0.9f, 1.1f);
        [SerializeField] private AudioClip _missClip;
        [SerializeField] private AudioClip[] _hitClips;
        [SerializeField] private AudioClip[] _hitDefenceClips;
        [SerializeField] private AudioClip _defenceClip;
        [SerializeField] private float _defenceVolume = 2f;
        [SerializeField] private float _hitDefencePitch = 1.5f;
        [SerializeField] private float _hitDefenceVolume = 2f;
        [SerializeField] private AudioClip _deathClip;
        [SerializeField] private AudioClip[] _deathClips;
        [SerializeField] private float _volume = 1f;
        [SerializeField] private float _moveVolume = 1f;
        [SerializeField] private float _hitVolume = 1f;
        [SerializeField] private float _deathVolume = 1f;
        [SerializeField] private float _idleVolume = 1f;
        [SerializeField] private float _pitch = 1f;

        private AudioSource _effectSource;
        private AudioSource _idleSource;
        private AudioSource _moveSource;
        private AudioSource _throwSource;
        private AudioSource _teleportSource;
        private AudioSource _impactSource;
        private AudioSource _defenceSource;
        private EnemySensor _sensor;
        private EnemyStateMachine _stateMachine;
        private bool? _lineOfSightInRange;
        private bool _suppressDetectAfterHit;
        private bool _started;
        private float _nextIdleTime;
        private bool _isMoving;
        private int _moveClipIndex;
        private bool _manualMoveSound;
        private float _moveVolumeMultiplier = 1f;

        private void Awake()
        {
            _sensor = GetComponent<EnemySensor>();
            _stateMachine = GetComponent<EnemyStateMachine>();
            _effectSource = gameObject.AddComponent<AudioSource>();
            _idleSource = gameObject.AddComponent<AudioSource>();
            _moveSource = gameObject.AddComponent<AudioSource>();
            _effectSource.playOnAwake = false;
            _idleSource.playOnAwake = false;
            _moveSource.playOnAwake = false;
            _moveSource.loop = _moveClips == null || _moveClips.Length == 0;
            _moveSource.clip = _moveClip;
            if (_teleportOutClip != null || _teleportInClip != null)
            {
                _teleportSource = gameObject.AddComponent<AudioSource>();
                _teleportSource.playOnAwake = false;
            }
            if (_throwClip != null)
            {
                _throwSource = gameObject.AddComponent<AudioSource>();
                _throwSource.playOnAwake = false;
            }

            _effectSource.pitch = _pitch;
            _idleSource.pitch = _pitch;
            _moveSource.pitch = _pitch;

            var enemyGroup = ShotgunAndRun.Core.AudioManager.Instance.EnemyGroup;
            _effectSource.outputAudioMixerGroup = enemyGroup;
            _idleSource.outputAudioMixerGroup = enemyGroup;
            _moveSource.outputAudioMixerGroup = enemyGroup;
            if (_throwSource != null) _throwSource.outputAudioMixerGroup = enemyGroup;
            if (_teleportSource != null) _teleportSource.outputAudioMixerGroup = enemyGroup;
        }

        private void Update()
        {
            UpdateVolume();

            if (_isMoving && !_manualMoveSound && !_moveSource.isPlaying) PlayNextMoveClip();

            if (_stateMachine == null || !_stateMachine.HasBegun ||
                _stateMachine.CurrentState == _stateMachine.DeathState) return;

            if (!_started)
            {
                _started = true;
                ScheduleIdle();
                if (_playDetectionSounds && _sensor != null && _sensor.IsTargetInDetectionRange())
                    _lineOfSightInRange = _sensor.HasLineOfSightToTarget();
                return;
            }

            UpdateIdleSound();

            if (!_playDetectionSounds) return;

            if (_stateMachine.CurrentState == _stateMachine.ReturnState)
            {
                _lineOfSightInRange = null;
                return;
            }

            if (_sensor == null || (_detectClip == null && _missClip == null)) return;

            if (!_sensor.IsTargetInDetectionRange())
            {
                _lineOfSightInRange = null;
                _suppressDetectAfterHit = false;
                return;
            }

            bool hasLineOfSight = _sensor.HasLineOfSightToTarget();
            if (_lineOfSightInRange != hasLineOfSight)
            {
                if (hasLineOfSight)
                {
                    if (!_suppressDetectAfterHit) PlayDetect();
                }
                else Play(_missClip);

                _lineOfSightInRange = hasLineOfSight;
            }

            if (!hasLineOfSight) _suppressDetectAfterHit = false;
        }

        public void SetMoving(bool isMoving)
        {
            _isMoving = isMoving;
            if (_moveClip == null && (_moveClips == null || _moveClips.Length == 0)) return;

            if (_isMoving)
            {
                if (!_manualMoveSound && !_moveSource.isPlaying) PlayNextMoveClip();
            }
            else
            {
                _moveSource.Stop();
                _moveClipIndex = 0;
            }
        }

        public void BeginManualMoveSound(float pitch, float volumeMultiplier)
        {
            _manualMoveSound = true;
            _moveSource.Stop();
            _moveSource.pitch = pitch;
            _moveVolumeMultiplier = volumeMultiplier;
        }

        public void PlayManualMoveStep()
        {
            if (!_manualMoveSound || !_isMoving) return;

            _moveSource.Stop();
            SelectNextMoveClip();
            if (_moveSource.clip != null) _moveSource.PlayOneShot(_moveSource.clip, _moveVolumeMultiplier);
        }

        public void EndManualMoveSound()
        {
            _manualMoveSound = false;
            _moveSource.Stop();
            _moveSource.pitch = _pitch;
            _moveVolumeMultiplier = 1f;
        }

        public void PlayAttack()
        {
            PauseIdle();
            PlayRandom(_attackClips);
        }

        public void PlayDetect() => Play(_detectClip, _detectVolume);

        public void PlayDraw() => PlayRandom(_drawClips);
        public void PlayShot() => PlayRandom(_shotClips);

        public void PlayTeleportOut() => PlayTeleport(_teleportOutClip);
        public void PlayTeleportIn() => PlayTeleport(_teleportInClip);

        public void StopTeleport()
        {
            if (_teleportSource != null) _teleportSource.Stop();
        }

        private void PlayTeleport(AudioClip clip)
        {
            if (_teleportSource == null || clip == null) return;

            UpdateVolume();
            _teleportSource.clip = clip;
            _teleportSource.Play();
        }

        public void PlayThrow()
        {
            if (_throwSource == null || UpdateVolume() <= 0f) return;

            _throwSource.pitch = Random.Range(_throwPitchRange.x, _throwPitchRange.y);
            _throwSource.PlayOneShot(_throwClip);
        }

        public void PlayHit()
        {
            PauseIdle();
            _suppressDetectAfterHit = true;
            PlayRandom(_hitClips, _hitVolume);
        }

        public void PlayDefence()
        {
            if (_defenceClip == null) return;
            StopDefenceSound();
            if (_defenceSource == null)
            {
                _defenceSource = gameObject.AddComponent<AudioSource>();
                _defenceSource.playOnAwake = false;
                _defenceSource.outputAudioMixerGroup = _effectSource.outputAudioMixerGroup;
            }

            UpdateVolume();
            _defenceSource.clip = _defenceClip;
            _defenceSource.pitch = 1f;
            _defenceSource.Play();
        }

        private void StopDefenceSound()
        {
            if (_defenceSource != null) _defenceSource.Stop();
        }

        public void PlayHitDefence()
        {
            PauseIdle();
            _suppressDetectAfterHit = true;
            PlayImpact(PickRandom(_hitDefenceClips), _hitDefencePitch, _hitDefenceVolume);
        }

        public void PlayHit(float pitch)
        {
            PauseIdle();
            _suppressDetectAfterHit = true;
            PlayImpact(PickRandom(_hitClips), pitch, _hitVolume);
        }

        private void PlayImpact(AudioClip clip, float pitch, float volume)
        {
            if (_impactSource == null)
            {
                _impactSource = gameObject.AddComponent<AudioSource>();
                _impactSource.playOnAwake = false;
                _impactSource.outputAudioMixerGroup = _effectSource.outputAudioMixerGroup;
            }

            _impactSource.pitch = pitch;
            if (clip != null && UpdateVolume() > 0f)
                _impactSource.PlayOneShot(clip, volume);
        }

        public void PlayDeath()
        {
            StopDefenceSound();
            if (_impactSource != null) _impactSource.Stop();
            StopTeleport();
            PauseIdle();
            _isMoving = false;
            _moveSource.Stop();
            _idleSource.Stop();
            _effectSource.Stop();
            Play(_deathClips != null && _deathClips.Length > 0
                ? PickRandom(_deathClips)
                : _deathClip, _deathVolume);
        }

        private void PlayNextMoveClip()
        {
            SelectNextMoveClip();
            if (_moveSource.clip != null) _moveSource.Play();
        }

        private void SelectNextMoveClip()
        {
            if (_moveClips != null && _moveClips.Length > 0)
            {
                if (_randomMoveClips) _moveSource.clip = PickRandom(_moveClips);
                else
                {
                    _moveSource.clip = _moveClips[_moveClipIndex];
                    _moveClipIndex = (_moveClipIndex + 1) % _moveClips.Length;
                }
            }

        }

        private void PlayIdle()
        {
            AudioClip clip = PickRandom(_idleClips);
            if (clip == null || UpdateVolume() <= 0f) return;

            _idleSource.clip = clip;
            _idleSource.Play();
        }

        private void UpdateIdleSound()
        {
            if (_idleClips == null || _idleClips.Length == 0) return;

            if (_stateMachine.CurrentState == _stateMachine.AttackState ||
                _stateMachine.CurrentState == _stateMachine.HitState ||
                _stateMachine.IsStunned)
            {
                PauseIdle();
                return;
            }

            if (_idleSource.isPlaying || Time.time < _nextIdleTime) return;

            PlayIdle();
            ScheduleIdle();
        }

        private void PauseIdle()
        {
            _idleSource.Stop();
        }

        private void ScheduleIdle()
        {
            _nextIdleTime = Time.time +
                            Random.Range(_idleCooldownRange.x, _idleCooldownRange.y);
        }

        private void PlayRandom(AudioClip[] clips, float volume = 1f) => Play(PickRandom(clips), volume);

        private static AudioClip PickRandom(AudioClip[] clips)
        {
            return clips == null || clips.Length == 0
                ? null
                : clips[Random.Range(0, clips.Length)];
        }

        private void Play(AudioClip clip, float volume = 1f)
        {
            if (clip != null && UpdateVolume() > 0f) _effectSource.PlayOneShot(clip, volume);
        }

        private float UpdateVolume()
        {
            Transform target = _sensor.TargetTransform;
            float volume = 0f;

            if (target != null)
            {
                Vector2 delta = target.position - transform.position;
                float distance = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
                if (distance < AudibleHalfExtent)
                    volume = 1f - distance / AudibleHalfExtent;
            }

            _effectSource.volume = volume * _volume;
            _idleSource.volume = volume * _volume * _idleVolume;
            _moveSource.volume = volume * _moveVolume;
            if (_throwSource != null) _throwSource.volume = volume * _volume;
            if (_teleportSource != null) _teleportSource.volume = volume * _volume;
            if (_impactSource != null) _impactSource.volume = volume * _volume;
            if (_defenceSource != null) _defenceSource.volume = volume * _volume * _defenceVolume;
            return _effectSource.volume;
        }

        private void OnDisable()
        {
            StopDefenceSound();
            if (_impactSource != null) _impactSource.Stop();
            StopTeleport();
            _isMoving = false;
            EndManualMoveSound();
            if (_moveSource != null) _moveSource.Stop();
            if (_idleSource != null) _idleSource.Stop();
            if (_throwSource != null) _throwSource.Stop();
        }
    }
}
