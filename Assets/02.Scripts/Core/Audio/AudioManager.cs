using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace ShotgunAndRun.Core
{
    /// <summary>
    /// 게임 전역 사운드(Master, BGM, SFX)와 볼륨 영속화를 관리하는 중앙 싱글톤 오디오 매니저입니다.
    /// AudioMixer(MainAudioMixer)를 통해 인게임 사운드와 분리된 라우팅을 제공하며,
    /// PlayerPrefs를 통해 볼륨 설정을 씬 전환 및 게임 재시작 후에도 유지합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioManager : MonoBehaviour
    {
        private const string MixerResourcePath = "Audio/MainAudioMixer";
        private const string MasterParam = "MasterVolume";
        private const string BgmParam = "BgmVolume";
        private const string SfxParam = "SfxVolume";

        private const string PrefsMasterKey = "Audio_MasterVolume";
        private const string PrefsBgmKey = "Audio_BgmVolume";
        private const string PrefsSfxKey = "Audio_SfxVolume";

        private const float DefaultVolume = 1f;
        private const float MinLinearVolume = 0.0001f;
        private const float MinDb = -80f;

        private static AudioManager _instance;
        private static bool _isQuitting;

        [Header("Mixer Configuration")]
        [SerializeField]
        private AudioMixer _mixer;

        private AudioMixerGroup _masterGroup;
        private AudioMixerGroup _bgmGroup;
        private AudioMixerGroup _sfxGroup;
        private AudioMixerGroup _playerGroup;
        private AudioMixerGroup _weaponGroup;
        private AudioMixerGroup _enemyGroup;
        private AudioMixerGroup _ambienceGroup;
        private AudioMixerGroup _uiGroup;

        private AudioSource _bgmSource;
        private AudioSource _bgmOutgoingSource;
        private AudioSource _sfxSource;
        private Coroutine _bgmFadeRoutine;
        private bool _bgmFadingOut;

        public static AudioManager Instance
        {
            get
            {
                if (_instance == null && !_isQuitting)
                {
                    EnsureInstance();
                }

                return _instance;
            }
        }

        public float MasterVolume
        {
            get;
            private set;
        } = DefaultVolume;

        public float BgmVolume
        {
            get;
            private set;
        } = DefaultVolume;

        public float SfxVolume
        {
            get;
            private set;
        } = DefaultVolume;

        public AudioMixerGroup MasterGroup => _masterGroup;
        public AudioMixerGroup BgmGroup => _bgmGroup;
        public AudioMixerGroup SfxGroup => _sfxGroup;
        public AudioMixerGroup PlayerGroup => _playerGroup;
        public AudioMixerGroup WeaponGroup => _weaponGroup;
        public AudioMixerGroup EnemyGroup => _enemyGroup;
        public AudioMixerGroup AmbienceGroup => _ambienceGroup;
        public AudioMixerGroup UiGroup => _uiGroup;

        public bool IsBgmPlaying => _bgmSource != null && _bgmSource.isPlaying;
        public float BgmTime => _bgmSource != null ? _bgmSource.time : 0f;
        public AudioClip CurrentBgmClip => _bgmSource != null ? _bgmSource.clip : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeOnStartup()
        {
            _isQuitting = false;
            EnsureInstance();
        }

        private static void EnsureInstance()
        {
            if (_instance != null)
            {
                return;
            }

            _instance = FindAnyObjectByType<AudioManager>();
            if (_instance != null)
            {
                DontDestroyOnLoad(_instance.gameObject);
                return;
            }

            GameObject audioGo = new GameObject(nameof(AudioManager));
            _instance = audioGo.AddComponent<AudioManager>();
            DontDestroyOnLoad(audioGo);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeAudioSources();
            LoadAndApplyVolumes();
        }

        private void OnApplicationQuit()
        {
            _isQuitting = true;
        }

        private void Start()
        {
            ApplyAllVolumesToMixer();
            StartCoroutine(ApplyVolumesDelayedRoutine());
        }

        private IEnumerator ApplyVolumesDelayedRoutine()
        {
            yield return null;
            ApplyAllVolumesToMixer();
        }

        private void InitializeAudioSources()
        {
            if (_mixer == null)
            {
                _mixer = Resources.Load<AudioMixer>(MixerResourcePath);
                if (_mixer == null)
                {
                    Debug.LogWarning($"[AudioManager] AudioMixer not found at Resources/{MixerResourcePath}");
                }
            }

            if (_mixer != null)
            {
                AudioMixerGroup[] masterGroups = _mixer.FindMatchingGroups("Master");
                if (masterGroups != null && masterGroups.Length > 0)
                {
                    _masterGroup = masterGroups[0];
                }

                AudioMixerGroup[] bgmGroups = _mixer.FindMatchingGroups("BGM");
                if (bgmGroups != null && bgmGroups.Length > 0)
                {
                    _bgmGroup = bgmGroups[0];
                }

                AudioMixerGroup[] sfxGroups = _mixer.FindMatchingGroups("SFX");
                if (sfxGroups != null && sfxGroups.Length > 0)
                {
                    _sfxGroup = sfxGroups[0];
                }

                _playerGroup = FindGroupByName("Player", _sfxGroup);
                _weaponGroup = FindGroupByName("Weapon", _sfxGroup);
                _enemyGroup = FindGroupByName("Enemy", _sfxGroup);
                _ambienceGroup = FindGroupByName("Ambience", _sfxGroup);
                _uiGroup = FindGroupByName("UI", _sfxGroup);
            }

            if (_bgmSource == null)
            {
                _bgmSource = gameObject.AddComponent<AudioSource>();
                _bgmSource.loop = true;
                _bgmSource.playOnAwake = false;
                _bgmSource.outputAudioMixerGroup = _bgmGroup;
            }

            if (_bgmOutgoingSource == null)
            {
                _bgmOutgoingSource = gameObject.AddComponent<AudioSource>();
                _bgmOutgoingSource.playOnAwake = false;
                _bgmOutgoingSource.outputAudioMixerGroup = _bgmGroup;
            }

            if (_sfxSource == null)
            {
                _sfxSource = gameObject.AddComponent<AudioSource>();
                _sfxSource.loop = false;
                _sfxSource.playOnAwake = false;
                _sfxSource.outputAudioMixerGroup = _uiGroup;
            }
        }

        private AudioMixerGroup FindGroupByName(string groupName, AudioMixerGroup fallback)
        {
            // FindMatchingGroups는 하위 경로 매칭이라 이름을 정확히 비교한다.
            foreach (AudioMixerGroup group in _mixer.FindMatchingGroups(string.Empty))
            {
                if (group.name == groupName)
                {
                    return group;
                }
            }

            return fallback;
        }

        private void LoadAndApplyVolumes()
        {
            MasterVolume = PlayerPrefs.GetFloat(PrefsMasterKey, DefaultVolume);
            BgmVolume = PlayerPrefs.GetFloat(PrefsBgmKey, DefaultVolume);
            SfxVolume = PlayerPrefs.GetFloat(PrefsSfxKey, DefaultVolume);

            ApplyAllVolumesToMixer();
        }

        private void ApplyAllVolumesToMixer()
        {
            ApplyMixerVolume(MasterParam, MasterVolume);
            ApplyMixerVolume(BgmParam, BgmVolume);
            ApplyMixerVolume(SfxParam, SfxVolume);
        }

        #region Volume Control

        public void SetMasterVolume(float linearVolume)
        {
            MasterVolume = Mathf.Clamp01(linearVolume);
            PlayerPrefs.SetFloat(PrefsMasterKey, MasterVolume);
            ApplyMixerVolume(MasterParam, MasterVolume);
        }

        public void SetBgmVolume(float linearVolume)
        {
            BgmVolume = Mathf.Clamp01(linearVolume);
            PlayerPrefs.SetFloat(PrefsBgmKey, BgmVolume);
            ApplyMixerVolume(BgmParam, BgmVolume);
        }

        public void SetSfxVolume(float linearVolume)
        {
            SfxVolume = Mathf.Clamp01(linearVolume);
            PlayerPrefs.SetFloat(PrefsSfxKey, SfxVolume);
            ApplyMixerVolume(SfxParam, SfxVolume);
        }

        public void ResetVolumesToDefault()
        {
            SetMasterVolume(DefaultVolume);
            SetBgmVolume(DefaultVolume);
            SetSfxVolume(DefaultVolume);
            PlayerPrefs.Save();
        }

        public void SavePreferences()
        {
            PlayerPrefs.Save();
        }

        private void ApplyMixerVolume(string parameterName, float linearVolume)
        {
            if (_mixer == null)
            {
                return;
            }

            float dB = LinearToDecibels(linearVolume);
            _mixer.SetFloat(parameterName, dB);
        }

        private static float LinearToDecibels(float linear)
        {
            if (linear <= MinLinearVolume)
            {
                return MinDb;
            }

            return Mathf.Log10(Mathf.Clamp01(linear)) * 20f;
        }

        #endregion

        #region BGM Playback

        public void PlayBgm(AudioClip clip, bool loop = true, float fadeDuration = 0.5f)
        {
            if (clip == null)
            {
                StopBgm(fadeDuration);
                return;
            }

            if (_bgmSource.isPlaying && _bgmSource.clip == clip && !_bgmFadingOut)
            {
                return;
            }

            if (_bgmFadeRoutine != null)
            {
                StopCoroutine(_bgmFadeRoutine);
            }

            _bgmFadingOut = false;
            _bgmFadeRoutine = StartCoroutine(FadeToNewBgm(clip, loop, fadeDuration));
        }

        public void StopBgm(float fadeDuration = 0.5f)
        {
            if (!_bgmSource.isPlaying)
            {
                return;
            }

            if (_bgmFadeRoutine != null)
            {
                StopCoroutine(_bgmFadeRoutine);
            }

            _bgmOutgoingSource.Stop();
            _bgmFadingOut = true;
            _bgmFadeRoutine = StartCoroutine(FadeOutBgmRoutine(fadeDuration));
        }

        public void PauseBgm()
        {
            if (_bgmSource != null && _bgmSource.isPlaying)
            {
                _bgmSource.Pause();
            }
        }

        public void ResumeBgm()
        {
            if (_bgmSource != null && !_bgmSource.isPlaying && _bgmSource.clip != null)
            {
                _bgmSource.UnPause();
            }
        }

        // 이전 곡은 별도 소스로 넘겨 새 곡과 겹쳐 페이드한다 (크로스페이드)
        private IEnumerator FadeToNewBgm(AudioClip newClip, bool loop, float duration)
        {
            float outgoingStartVolume = 0f;
            _bgmOutgoingSource.Stop();
            if (duration > 0f && _bgmSource.isPlaying && _bgmSource.clip != null)
            {
                outgoingStartVolume = _bgmSource.volume;
                _bgmOutgoingSource.clip = _bgmSource.clip;
                _bgmOutgoingSource.loop = _bgmSource.loop;
                _bgmOutgoingSource.volume = outgoingStartVolume;
                _bgmOutgoingSource.time = _bgmSource.time;
                _bgmOutgoingSource.Play();
            }

            _bgmSource.clip = newClip;
            _bgmSource.loop = loop;
            _bgmSource.volume = 0f;
            _bgmSource.Play();

            if (duration > 0f)
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = elapsed / duration;
                    _bgmSource.volume = Mathf.Lerp(0f, 1f, t);
                    _bgmOutgoingSource.volume = Mathf.Lerp(outgoingStartVolume, 0f, t);
                    yield return null;
                }
            }

            _bgmOutgoingSource.Stop();
            _bgmOutgoingSource.clip = null;
            _bgmSource.volume = 1f;
            _bgmFadeRoutine = null;
        }

        private IEnumerator FadeOutBgmRoutine(float duration)
        {
            if (duration > 0f)
            {
                float startVol = _bgmSource.volume;
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    _bgmSource.volume = Mathf.Lerp(startVol, 0f, elapsed / duration);
                    yield return null;
                }
            }

            _bgmSource.Stop();
            _bgmSource.clip = null;
            _bgmSource.volume = 1f;
            _bgmFadingOut = false;
            _bgmFadeRoutine = null;
        }

        #endregion

        #region SFX Playback

        public void PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (clip == null || _sfxSource == null)
            {
                return;
            }

            _sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        #endregion
    }
}
