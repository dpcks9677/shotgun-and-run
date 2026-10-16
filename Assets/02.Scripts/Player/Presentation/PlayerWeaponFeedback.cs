using System.Collections;
using ShotgunAndRun.Combat;
using ShotgunAndRun.Weapon;
using UnityEngine;

namespace ShotgunAndRun.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WeaponAudioFeedback))]
    [RequireComponent(typeof(WeaponVFXFeedback))]
    [RequireComponent(typeof(WeaponAnimationFeedback))]
    public sealed class PlayerWeaponFeedback : MonoBehaviour
    {
        [SerializeField] private PlayerWeapon _weapon;
        [SerializeField] private PlayerHealth _playerHealth;
        [SerializeField] private WeaponAudioFeedback _audio;
        [SerializeField] private WeaponVFXFeedback _vfx;
        [SerializeField] private WeaponAnimationFeedback _animation;
        [SerializeField] private PlayerAnimationFeedback _bodyAnimation;

        [Header("Critical Hit Stop")]
        [SerializeField, Min(0f)] private float _criticalHitStopDuration = 0.2f;
        [SerializeField, Range(0f, 1f)] private float _criticalHitStopTimeScale = 0.1f;

        private Coroutine _criticalHitStopRoutine;
        private float _timeScaleBeforeCriticalHitStop = 1f;

        private void Awake()
        {
            _weapon ??= GetComponentInParent<PlayerWeapon>();
            _playerHealth ??= GetComponentInParent<PlayerHealth>();
            _bodyAnimation ??= GetComponentInParent<PlayerAnimationFeedback>();
            _animation = GetComponent<WeaponAnimationFeedback>();
            _vfx = GetComponent<WeaponVFXFeedback>();
            _audio ??= GetComponent<WeaponAudioFeedback>();
        }

        private void OnEnable()
        {
            if (_weapon == null)
                return;

            _weapon.Fired += HandleFired;
            _weapon.CriticalHitConfirmed += HandleCriticalHitConfirmed;
            _weapon.PumpAction += HandlePumpAction;
            _weapon.ReloadStarted += HandleReloadStarted;
            _weapon.Reloading += HandleReloading;
            _weapon.ReloadEnded += HandleReloadEnded;
            _weapon.NoAmmo += HandleEmptyAmmo;
            _playerHealth.Damaged += HandleDamaged;
        }

        private void OnDisable()
        {
            StopCriticalHitStop();

            if (_weapon == null)
                return;

            _weapon.Fired -= HandleFired;
            _weapon.CriticalHitConfirmed -= HandleCriticalHitConfirmed;
            _weapon.PumpAction -= HandlePumpAction;
            _weapon.ReloadStarted -= HandleReloadStarted;
            _weapon.Reloading -= HandleReloading;
            _weapon.ReloadEnded -= HandleReloadEnded;
            _weapon.NoAmmo -= HandleEmptyAmmo;
            _playerHealth.Damaged -= HandleDamaged;
        }

        private void HandleCriticalHitConfirmed(Vector2 fireDirection)
        {
            StartCriticalHitStop();
        }

        private void HandleFired(bool isCritical)
        {
            _animation?.PlayFire();
            _bodyAnimation?.PlayFire();
            _vfx?.PlayMuzzleFlash(isCritical);

            if (isCritical)
            {
                _audio?.PlayCriticalFire();
            }
            else
            {
                _audio?.PlayFire();
            }
        }

        private void StartCriticalHitStop()
        {
            if (_criticalHitStopDuration <= 0f ||
                _criticalHitStopTimeScale >= 1f ||
                Time.timeScale <= 0f)
            {
                return;
            }

            if (_criticalHitStopRoutine == null)
            {
                _timeScaleBeforeCriticalHitStop = Time.timeScale;
            }
            else
            {
                StopCoroutine(_criticalHitStopRoutine);
            }

            Time.timeScale = _criticalHitStopTimeScale;
            _criticalHitStopRoutine = StartCoroutine(EndCriticalHitStop());
        }

        private IEnumerator EndCriticalHitStop()
        {
            yield return new WaitForSecondsRealtime(_criticalHitStopDuration);

            Time.timeScale = _timeScaleBeforeCriticalHitStop;
            _criticalHitStopRoutine = null;
        }

        private void StopCriticalHitStop()
        {
            if (_criticalHitStopRoutine == null)
            {
                return;
            }

            StopCoroutine(_criticalHitStopRoutine);
            _criticalHitStopRoutine = null;
            Time.timeScale = _timeScaleBeforeCriticalHitStop;
        }

        private void HandlePumpAction()
        {
            _animation?.PlayPumpAction();
        }

        private void PlayPumpAudio()
        {
            _audio?.PlayPump();
        }

        private void HandleReloadStarted()
        {
            // _audio?.PlayReloadStart();
            // _animation?.PlayReloading();
        }

        private void HandleReloading()
        {
            _animation.PlayReloading();
            _audio.PlayReload();
        }

        private void HandleReloadEnded()
        {
            // _audio?.PlayReloadComplete();
        }

        private void HandleDamaged(DamageInfo damageInfo)
        {
            _animation.OnDamaged(Player.PlayerInstance.PlayerData.HitInvincibilityDuration);
        }

        private void HandleEmptyAmmo()
        {
            _audio.PlayEmptyAmmo();
        }
    }
}
