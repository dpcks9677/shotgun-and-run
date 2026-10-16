using System;
using UnityEngine;

namespace Consumable
{
    public class HealEffect : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        float animationLength = 0f;
        float elapsed = 0f;
        
        private void Awake()
        {
            _animator = GetComponent<Animator>();

            if (_animator == null)
            {
                Debug.LogError("_animator가 할당되지 않았습니다.");
            }
            
            transform.position += Vector3.up * 0.8f;
            animationLength = GetAnimationLength();
        }

        private void Update()
        {
            elapsed += Time.deltaTime;

            if (elapsed >= animationLength)
            {
                Destroy(gameObject);
            }
        }


        private float GetAnimationLength()
        {
            AnimatorClipInfo[] clips = _animator.GetCurrentAnimatorClipInfo(0);

            if (clips.Length == 0)
            {
                return 0f;
            }

            return clips[0].clip.length;
        }
    }
}