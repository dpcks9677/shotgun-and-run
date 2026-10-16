using System;
using ShotgunAndRun.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShotgunAndRun.UI
{
    /// <summary>
    /// 마스터, BGM, SFX 음량을 조절하고 PlayerPrefs 및 MainAudioMixer와 동기화하는 설정창 팝업 UI입니다.
    /// 슬라이더 조작 시 실시간으로 볼륨이 반영되며, SFX 슬라이더 조절 시 프리뷰 효과음을 재생합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class SettingsPopupUI : MonoBehaviour
    {
        [Header("Popup Root")]
        [Tooltip("설정창 팝업의 최상위 패널 오브젝트 (활성/비활성화 대상)")]
        [SerializeField]
        private GameObject _popupPanel;

        [Header("Volume Sliders")]
        [Tooltip("마스터 전체 음량 슬라이더")]
        [SerializeField]
        private Slider _masterSlider;

        [Tooltip("BGM 배경음악 음량 슬라이더")]
        [SerializeField]
        private Slider _bgmSlider;

        [Tooltip("SFX 효과음 음량 슬라이더")]
        [SerializeField]
        private Slider _sfxSlider;

        [Header("Value Display Texts (Optional)")]
        [SerializeField]
        private TextMeshProUGUI _masterValueText;

        [SerializeField] private TextMeshProUGUI _bgmValueText;
        [SerializeField] private TextMeshProUGUI _sfxValueText;

        [Header("Buttons")][SerializeField] private Button _closeButton;
        [SerializeField] private Button _resetButton;

        [Header("Audio Feedback")]
        [Tooltip("SFX 슬라이더 조절 시 재생할 테스트 효과음 클립")]
        [SerializeField]
        private AudioClip _testSfxClip;

        [SerializeField] private float _previewThrottleInterval = 0.15f;

        [Header("Options")][SerializeField] private bool _closeOnEscape = true;

        private float _lastPreviewSoundTime;
        private bool _isSuppressingEvents;

        public bool IsOpen => _popupPanel != null ? _popupPanel.activeSelf : gameObject.activeSelf;

        public event Action Closed;

        private void Awake()
        {
            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(Close);
            }

            if (_resetButton != null)
            {
                _resetButton.onClick.AddListener(ResetToDefault);
            }

            if (_masterSlider != null)
            {
                _masterSlider.onValueChanged.AddListener(OnMasterSliderChanged);
            }

            if (_bgmSlider != null)
            {
                _bgmSlider.onValueChanged.AddListener(OnBgmSliderChanged);
            }

            if (_sfxSlider != null)
            {
                _sfxSlider.onValueChanged.AddListener(OnSfxSliderChanged);
            }
        }

        private void Start()
        {
            SyncSlidersFromAudioManager();
        }

        private void Update()
        {
            if (_closeOnEscape && IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }

        public void Open()
        {
            if (_popupPanel != null)
            {
                _popupPanel.SetActive(true);
            }
            else
            {
                gameObject.SetActive(true);
            }

            SyncSlidersFromAudioManager();
        }

        public void SetButtonAudio(
            AudioClip hoverSfx,
            AudioClip clickSfx,
            float hoverVolume,
            float clickVolume)
        {
            ConfigureButtonAudio(_closeButton, hoverSfx, clickSfx, hoverVolume, clickVolume);
            ConfigureButtonAudio(_resetButton, hoverSfx, clickSfx, hoverVolume, clickVolume);
        }

        public void Close()
        {
            if (_popupPanel != null)
            {
                _popupPanel.SetActive(false);
            }
            else
            {
                gameObject.SetActive(false);
            }

            AudioManager.Instance.SavePreferences();
            Closed?.Invoke();
        }

        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        public void ResetToDefault()
        {
            AudioManager.Instance.ResetVolumesToDefault();
            SyncSlidersFromAudioManager();
            PlayPreviewSound();
        }

        private void SyncSlidersFromAudioManager()
        {
            _isSuppressingEvents = true;

            AudioManager audioMgr = AudioManager.Instance;
            if (_masterSlider != null)
            {
                _masterSlider.value = audioMgr.MasterVolume;
                UpdateValueText(_masterValueText, audioMgr.MasterVolume);
            }

            if (_bgmSlider != null)
            {
                _bgmSlider.value = audioMgr.BgmVolume;
                UpdateValueText(_bgmValueText, audioMgr.BgmVolume);
            }

            if (_sfxSlider != null)
            {
                _sfxSlider.value = audioMgr.SfxVolume;
                UpdateValueText(_sfxValueText, audioMgr.SfxVolume);
            }

            _isSuppressingEvents = false;
        }

        private void OnMasterSliderChanged(float value)
        {
            if (_isSuppressingEvents)
            {
                return;
            }

            AudioManager.Instance.SetMasterVolume(value);
            UpdateValueText(_masterValueText, value);
        }

        private void OnBgmSliderChanged(float value)
        {
            if (_isSuppressingEvents)
            {
                return;
            }

            AudioManager.Instance.SetBgmVolume(value);
            UpdateValueText(_bgmValueText, value);
        }

        private void OnSfxSliderChanged(float value)
        {
            if (_isSuppressingEvents)
            {
                return;
            }

            AudioManager.Instance.SetSfxVolume(value);
            UpdateValueText(_sfxValueText, value);

            if (Time.unscaledTime - _lastPreviewSoundTime >= _previewThrottleInterval)
            {
                PlayPreviewSound();
                _lastPreviewSoundTime = Time.unscaledTime;
            }
        }

        private void PlayPreviewSound()
        {
            if (_testSfxClip != null)
            {
                AudioManager.Instance.PlaySfx(_testSfxClip);
            }
        }

        private static void ConfigureButtonAudio(
            Button button,
            AudioClip hoverSfx,
            AudioClip clickSfx,
            float hoverVolume,
            float clickVolume)
        {
            if (button == null)
            {
                return;
            }

            ButtonHoverFeedback feedback = button.GetComponent<ButtonHoverFeedback>();
            if (feedback == null)
            {
                feedback = button.gameObject.AddComponent<ButtonHoverFeedback>();
            }

            feedback.SetAudio(hoverSfx, clickSfx, hoverVolume, clickVolume);
        }

        private static void UpdateValueText(TextMeshProUGUI textComponent, float linearValue)
        {
            if (textComponent != null)
            {
                int percentage = Mathf.RoundToInt(linearValue * 100f);
                textComponent.text = $"{percentage}%";
            }
        }
    }
}
