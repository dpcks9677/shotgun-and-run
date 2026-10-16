using System;
using System.Collections;
using UnityEngine;

namespace ShotgunAndRun.Player
{
    public sealed class PlayerAnimationFeedback : MonoBehaviour
    {
        private static readonly int HitTrigger = Animator.StringToHash("Hit");
        private static readonly int DeadTrigger = Animator.StringToHash("Dead");
        private static readonly int DashTrigger = Animator.StringToHash("Dash");
        private static readonly int DashEndTrigger = Animator.StringToHash("DashEnd");
        private static readonly int FireTrigger = Animator.StringToHash("Fire");

        private static readonly int MoveBool = Animator.StringToHash("IsMoving");

        [SerializeField] private Animator _animator;
        [SerializeField] private SpriteRenderer _spriteRenderer;

        [SerializeField] private RuntimeAnimatorController _shelterController;
        [SerializeField] private RuntimeAnimatorController _armedController;

        private Coroutine _blinkCoroutine;

        private void Awake()
        {
            PlayerBody body = GetComponentInChildren<PlayerBody>(true);
            _animator = body.Animator;
            _spriteRenderer = body.SpriteRenderer;
        }

        public void PlayDamage() => SetTrigger(HitTrigger);
        public void PlayFire() => SetTrigger(FireTrigger);
        public void PlayDead() => SetTrigger(DeadTrigger);
        public void PlayDash(Vector2 direction) => SetTrigger(DashTrigger);
        public void PlayDashEnd() => SetTrigger(DashEndTrigger);

        private void SetTrigger(int trigger)
        {
            if (_animator != null)
            {
                _animator.SetTrigger(trigger);
            }
        }

        private void SetBool(int param, bool boolean)
        {
            if (_animator != null)
            {
                _animator.SetBool(param, boolean);
            }
        }

        public void UpdateWalkAnimation(Vector2 moveInput)
        {
            // 좌우 입력이 있을 때만 방향 변경
            // if (Mathf.Abs(moveInput.x) > 0.01f)
            // {
            //     _spriteRenderer.flipX = moveInput.x < 0f;
            // }

            SetBool(MoveBool, Player.PlayerInstance.Movement.IsMoving);
        }

        public void OnDamaged()
        {
            if (_blinkCoroutine != null)
                StopCoroutine(_blinkCoroutine);

            _blinkCoroutine = StartCoroutine(BlinkRoutine());
        }

        private IEnumerator BlinkRoutine()
        {
            float invulnerabilityTime = Player.PlayerInstance.PlayerData.HitInvincibilityDuration;
            float blinkInterval = 0.1f;

            float elapsedTime = 0f;

            while (elapsedTime < invulnerabilityTime)
            {
                // 반투명
                _spriteRenderer.color = new Color(1f, 1f, 1f, 0.4f);

                float waitTime = Mathf.Min(blinkInterval, invulnerabilityTime - elapsedTime);
                yield return new WaitForSeconds(waitTime);
                elapsedTime += waitTime;

                if (elapsedTime >= invulnerabilityTime)
                    break;

                // 원래 상태
                _spriteRenderer.color = new Color(1f, 1f, 1f, 1f);

                waitTime = Mathf.Min(blinkInterval, invulnerabilityTime - elapsedTime);
                yield return new WaitForSeconds(waitTime);
                elapsedTime += waitTime;
            }

            // 무적 시간이 끝나면 반드시 원래 상태로 복구
            _spriteRenderer.color = new Color(1f, 1f, 1f, 1f);
        }

        public void SetShelterMode(bool isShelter)
        {
            _animator ??= GetComponentInChildren<PlayerBody>(true).Animator;
            if (_animator != null)
            {
                var controller = isShelter ? _shelterController : _armedController;
                if (controller != null && _animator.runtimeAnimatorController != controller)
                {
                    _animator.runtimeAnimatorController = controller;
                }
            }

            if (Player.PlayerInstance != null && Player.PlayerInstance.Movement != null)
            {
                SetBool(MoveBool, Player.PlayerInstance.Movement.IsMoving);
            }
        }
    }
}
