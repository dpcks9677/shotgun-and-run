using System;
using System.Collections;
using UnityEngine;

namespace ShotgunAndRun.Core
{
    // 타이틀에서 게임을 시작할 때: 플레이어가 오른쪽 바깥으로 걸어가는 동안 화면을 서서히 어둡게 하고,
    // 어두워지는 것이 끝나면 씬 전환(시작 동작)을 이어서 실행합니다.
    public sealed class TitleExitWalk : MonoBehaviour
    {
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

        [SerializeField] private Animator _animator;
        [SerializeField] private float _walkSpeed = 4f;
        [SerializeField] private float _fadeDuration = 1f;

        private bool _isPlaying;
        private bool _isWalking;

        private void Update()
        {
            if (_isWalking)
            {
                transform.Translate(Vector3.right * (_walkSpeed * Time.deltaTime));
            }
        }

        public void Play(Action onFadedOut)
        {
            if (_isPlaying)
            {
                return;
            }

            _isPlaying = true;
            StartCoroutine(Run(onFadedOut));
        }

        private IEnumerator Run(Action onFadedOut)
        {
            _animator.SetBool(IsMovingHash, true);
            _isWalking = true;

            yield return GameManager.Instance.FadeOut(_fadeDuration);

            onFadedOut();
        }
    }
}
