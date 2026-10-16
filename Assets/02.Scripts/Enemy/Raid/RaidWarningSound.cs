using ShotgunAndRun.Core;
using UnityEngine;

namespace ShotgunAndRun.Enemy
{
    [RequireComponent(typeof(AudioSource))]
    public class RaidWarningSound : MonoBehaviour
    {
        // Audio Clip 컴포넌트 캐싱
        [SerializeField] private AudioClip _warningClip;

        private AudioSource _audioSource;
        private RaidTimer _raidTimer;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
        }

        private void Start()
        {
            RunSession runSession = GameManager.Instance.CurrentRun;

            if (runSession == null)
            {
                Debug.Log("runSession is null");
                return;
            }

            _raidTimer = runSession.RaidTimer;
            _raidTimer.WarningEvent += StartWarningSound;
        }

        // 습격 경고마다 1번만 재생 (반복 재생 없음)
        private void StartWarningSound()
        {
            _audioSource.PlayOneShot(_warningClip);
        }

        private void OnDestroy()
        {
            if (_raidTimer != null)
            {
                _raidTimer.WarningEvent -= StartWarningSound;
            }
        }
    }
}
