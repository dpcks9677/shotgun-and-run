using System;
using System.Collections;
using ShotgunAndRun.Core;
using UnityEngine;

namespace ShotgunAndRun.UI
{
    // 강화 버튼에 맞춰 그룬이 망치로 모루를 정해진 횟수만큼 내려치는 연출.
    // Animator(IsReinforcing)가 Reinforce 클립을 반복 재생하고, 클립의 타격 프레임이 애니메이션 이벤트 OnHammerHit를 부른다.
    // 타격마다 불똥과 효과음을 재생하고, 마지막 타격 순간에 onFinished를 불러 강화를 확정하게 한다.
    // 이 컴포넌트는 Animator와 같은 오브젝트에 둔다 (애니메이션 이벤트는 Animator가 있는 오브젝트로 전달됨).
    public sealed class GrunForgeSequence : MonoBehaviour
    {
        private static readonly int IsReinforcing = Animator.StringToHash("IsReinforcing");

        [SerializeField] private Animator _animator;

        [Tooltip("강화 한 번에 내려치는 횟수")]
        [SerializeField] private int _hitCount = 3;

        [Tooltip("불똥을 그릴 렌더러. 망치가 모루에 닿는 자리에 두고, 스프라이트 피벗이 타격 지점이다")]
        [SerializeField] private SpriteRenderer _spark;
        [SerializeField] private Sprite[] _sparkFrames;
        [SerializeField] private float _sparkFramesPerSecond = 20f;

        [Tooltip("타격 순서대로 재생할 효과음 (타격 횟수보다 적으면 처음부터 반복)")]
        [SerializeField] private AudioClip[] _hitClips;

        private int _hitsLeft;
        private int _hitIndex;
        private Action _onFinished;
        private Coroutine _sparkRoutine;

        public bool IsPlaying => _hitsLeft > 0;

        // 강화 컷씬(GrunReinforceCutscene)이 연출이 끝나기를 기다리는 데 쓴다. 대장간 연출은 씬에 하나뿐이다.
        public static bool IsAnyPlaying { get; private set; }

        private void OnDisable()
        {
            _hitsLeft = 0;
            IsAnyPlaying = false;
        }

        public void Play(Action onFinished)
        {
            _hitsLeft = _hitCount;
            _hitIndex = 0;
            _onFinished = onFinished;
            IsAnyPlaying = true;

            // 모루는 그룬의 오른쪽에 있다. 컷씬 뒤에 왼쪽을 보고 있었어도 모루 쪽으로 돌아서서 친다.
            _animator.GetComponent<SpriteRenderer>().flipX = false;
            _animator.SetBool(IsReinforcing, true);
        }

        // 강화 창이 닫히는 등으로 연출을 중간에 끊는다. 강화는 확정되지 않는다.
        public void Cancel()
        {
            _hitsLeft = 0;
            IsAnyPlaying = false;
            _onFinished = null;
            _animator.SetBool(IsReinforcing, false);
        }

        // 애니메이션 이벤트: Grun_Reinforce의 망치가 모루에 닿는 프레임
        public void OnHammerHit()
        {
            if (_hitsLeft <= 0)
            {
                return;
            }

            if (_sparkRoutine != null)
            {
                StopCoroutine(_sparkRoutine);
            }

            _sparkRoutine = StartCoroutine(PlaySpark());
            AudioManager.Instance.PlaySfx(_hitClips[_hitIndex++ % _hitClips.Length]);

            if (--_hitsLeft > 0)
            {
                return;
            }

            _animator.SetBool(IsReinforcing, false);
            IsAnyPlaying = false;
            Action finished = _onFinished;
            _onFinished = null;
            finished?.Invoke();
        }

        private IEnumerator PlaySpark()
        {
            float interval = 1f / _sparkFramesPerSecond;
            foreach (Sprite frame in _sparkFrames)
            {
                _spark.sprite = frame;
                yield return new WaitForSeconds(interval);
            }

            _spark.sprite = null;
        }
    }
}
