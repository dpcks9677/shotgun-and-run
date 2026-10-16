using System.Collections;
using UnityEngine;

namespace ShotgunAndRun.Weapon
{
    public sealed class WeaponAnimationFeedback : MonoBehaviour
    {
        private static readonly int FireTrigger =
            Animator.StringToHash("Fire");

        private static readonly int PumpActionTrigger =
            Animator.StringToHash("PumpAction");

        private static readonly int ReloadTrigger =
            Animator.StringToHash("Reload");

        private static readonly int ReloadingTrigger =
            Animator.StringToHash("Reloading");

        private static readonly int ReloadEndTrigger =
            Animator.StringToHash("ReloadEnd");

        [SerializeField] private Animator _animator;
        [SerializeField] private SpriteRenderer _spriteRenderer;

        private Coroutine _blinkCoroutine;

        private void Awake()
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void PlayFire()
        {
            SetTrigger(FireTrigger);
        }

        public void PlayPumpAction()
        {
            SetTrigger(PumpActionTrigger);
        }

        public void PlayReload()
        {
            SetTrigger(ReloadTrigger);
        }

        public void PlayReloading()
        {
            SetTrigger(ReloadingTrigger);
        }

        public void PlayReloadComplete()
        {
            SetTrigger(ReloadEndTrigger);
        }

        private void SetTrigger(int trigger)
        {
            if (_animator == null)
            {
                Debug.LogWarning(
                    $"{nameof(WeaponAnimationFeedback)}에 Animator가 할당되지 않았습니다.",
                    this);

                return;
            }

            _animator.SetTrigger(trigger);
        }

        public void OnDamaged(float duration)
        {
            if (_blinkCoroutine != null)
                StopCoroutine(_blinkCoroutine);

            _blinkCoroutine = StartCoroutine(BlinkRoutine(duration));
        }

        private IEnumerator BlinkRoutine(float duration)
        {
            float elapsedTime = 0f;
            float blinkInterval = 0.1f;

            while (elapsedTime < duration)
            {
                // 반투명
                _spriteRenderer.color = new Color(1f, 1f, 1f, 0.4f);

                float waitTime = Mathf.Min(blinkInterval, duration - elapsedTime);
                yield return new WaitForSeconds(waitTime);
                elapsedTime += waitTime;

                if (elapsedTime >= duration)
                    break;

                // 원래 상태
                _spriteRenderer.color = new Color(1f, 1f, 1f, 1f);

                waitTime = Mathf.Min(blinkInterval, duration - elapsedTime);
                yield return new WaitForSeconds(waitTime);
                elapsedTime += waitTime;
            }

            // 무적 시간이 끝나면 반드시 원래 상태로 복구
            _spriteRenderer.color = new Color(1f, 1f, 1f, 1f);
        }
    }
}
