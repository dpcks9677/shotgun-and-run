using System.Collections;
using UnityEngine;

namespace ShotgunAndRun.Player
{
    // 플레이어의 몸 스프라이트입니다. 사격하면 반동 방향으로 몇 픽셀 밀렸다가 제자리로 돌아옵니다.
    // 몸 SpriteRenderer · Animator를 Rigidbody2D가 있는 루트에서 떼어 두어 몸만 흔들 수 있습니다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Animator))]
    public sealed class PlayerBody : MonoBehaviour
    {
        [SerializeField] private PlayerWeapon _weapon;
        [SerializeField] private Animator _animator;
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private float _kickDuration = 0.12f;
        [SerializeField] private int _kickPixels = 2;

        private Coroutine _kickCoroutine;
        private PlayerAudioFeedback _audio;

        public Animator Animator => _animator;
        public SpriteRenderer SpriteRenderer => _spriteRenderer;

        private void Awake()
        {
            _audio = GetComponentInParent<PlayerAudioFeedback>();
        }

        // Walk 애니메이션 이벤트는 Animator가 있는 이 오브젝트로 들어옵니다. 걷는 소리는 루트의 PlayerAudioFeedback이 냅니다.
        public void PlayWalk()
        {
            _audio.PlayWalk();
        }

        private void OnEnable()
        {
            _weapon.RecoilRequested += HandleRecoilRequested;
        }

        private void OnDisable()
        {
            _weapon.RecoilRequested -= HandleRecoilRequested;
            _kickCoroutine = null;
            transform.localPosition = Vector3.zero;
        }

        private void HandleRecoilRequested(Vector2 direction, float distance)
        {
            if (_kickCoroutine != null)
            {
                StopCoroutine(_kickCoroutine);
            }

            _kickCoroutine = StartCoroutine(KickRoutine(direction));
        }

        // 남은 시간에 따라 밀린 픽셀 수를 줄여, 도트가 칸 단위로 움직이게 합니다.
        private IEnumerator KickRoutine(Vector2 direction)
        {
            float pixelSize = 1f / _spriteRenderer.sprite.pixelsPerUnit;

            for (float elapsed = 0f; elapsed < _kickDuration; elapsed += Time.deltaTime)
            {
                float pixels = Mathf.Ceil(_kickPixels * (1f - elapsed / _kickDuration));
                Vector2 offset = direction * pixels;
                transform.localPosition = new Vector3(
                    Mathf.Round(offset.x),
                    Mathf.Round(offset.y)) * pixelSize;
                yield return null;
            }

            transform.localPosition = Vector3.zero;
            _kickCoroutine = null;
        }
    }
}
