using ShotgunAndRun.UI;
using UnityEngine;

namespace ShotgunAndRun.Player
{
    // 재장전 진행을 머리 위 행동 진행 막대(ActionProgressBar)로 표시합니다.
    public class PlayerReloadSliderUI : MonoBehaviour
    {
        [SerializeField] private PlayerWeapon _weapon;
        [SerializeField] private ActionProgressBar _progressBar;

        private void Awake()
        {
            _weapon ??= GetComponentInParent<PlayerWeapon>();

            if (_progressBar == null)
            {
                _progressBar = GetComponentInParent<Player>()?.GetComponentInChildren<ActionProgressBar>(true);
            }

            if (_weapon == null)
                Debug.LogError("PlayerWeapon을 찾을 수 없습니다.", this);

            if (_progressBar == null)
                Debug.LogError("ActionProgressBar를 찾을 수 없습니다.", this);
        }

        private bool _isCanceled;

        private void OnEnable()
        {
            if (_weapon == null)
            {
                Debug.LogError("_weapon이 초기화되지 않았습니다.");
                return;
            }

            _weapon.ReloadStarted += HandleReloadStarted;
            _weapon.Reloading += HandleReloading;
            _weapon.ReloadEnded += HandleReloadEnded;
            _weapon.ReloadCanceled += HandleReloadCanceled;
        }

        private void OnDisable()
        {
            if (_weapon != null)
            {
                _weapon.ReloadStarted -= HandleReloadStarted;
                _weapon.Reloading -= HandleReloading;
                _weapon.ReloadEnded -= HandleReloadEnded;
                _weapon.ReloadCanceled -= HandleReloadCanceled;
            }

            _isCanceled = false;
            _progressBar?.Hide(this);
        }

        public void HandleReloadStarted()
        {
            _isCanceled = false;
            _progressBar?.SetProgress(this, 0f);
        }

        public void HandleReloading()
        {
            _isCanceled = false;
            if (_weapon == null || _weapon.Stats == null || _progressBar == null)
            {
                return;
            }

            if (_weapon.CurrentMagazine < _weapon.Stats.MagazineSize)
            {
                _progressBar.StartFill(this, _weapon.Stats.ReloadDuration);
            }
            else
            {
                _progressBar.SetProgress(this, 0f);
            }
        }

        public void HandleReloadEnded()
        {
            if (!_isCanceled)
            {
                _progressBar?.Hide(this);
            }

            _isCanceled = false;
        }

        public void HandleReloadCanceled()
        {
            _isCanceled = true;
            _progressBar?.Cancel(this);
        }
    }
}
