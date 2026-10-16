using System.Collections.Generic;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// NPC용 컷씬 인물. Transform으로 이동하고, 걷기는 Animator의 IsMoving, 방향은 SpriteRenderer.flipX로 처리합니다.
    /// Animator는 NPC 공용 컨트롤러(NPC_Base)나 그 Override Controller를 써야 합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NpcCutsceneActor : CutsceneActor
    {
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

        [Tooltip("걷기 · 대기 애니메이션을 재생하는 Animator")]
        [SerializeField] private Animator _animator;

        [Tooltip("방향을 바꿀 SpriteRenderer들")]
        [SerializeField] private SpriteRenderer[] _renderers;

        [Tooltip("스프라이트 원본이 오른쪽을 보고 있으면 체크 (flipX가 켜지면 왼쪽을 봄)")]
        [SerializeField] private bool _spriteFacesRight = true;

        protected override Animator Animator => _animator;

        private void Reset()
        {
            _animator = GetComponentInChildren<Animator>();
            _renderers = GetComponentsInChildren<SpriteRenderer>();
        }

        internal override void Validate(List<string> errors)
        {
            base.Validate(errors);

            if (_animator == null)
            {
                errors.Add($"'{name}'의 NpcCutsceneActor에 Animator가 비어 있습니다.");
            }
            else if (Application.isPlaying && !HasMovingParameter())
            {
                errors.Add($"'{name}'의 Animator에 Bool 파라미터 'IsMoving'이 없습니다. NPC_Base 컨트롤러(또는 그 Override)를 쓰세요.");
            }

            if (_renderers == null || _renderers.Length == 0)
            {
                errors.Add($"'{name}'의 NpcCutsceneActor에 SpriteRenderer가 비어 있습니다.");
            }
        }

        internal override void BeginCutscene(CutsceneContext context)
        {
        }

        internal override void EndCutscene()
        {
            SetMoving(false, Vector2.zero);
            StopClip();
        }

        internal override void Teleport(Vector3 position)
        {
            position.z = transform.position.z;
            transform.position = position;
        }

        internal override void StepTowards(Vector3 target, float maxDistance)
        {
            target.z = transform.position.z;
            transform.position = Vector3.MoveTowards(transform.position, target, maxDistance);
        }

        internal override void SetMoving(bool moving, Vector2 direction)
        {
            if (_animator != null && _animator.isActiveAndEnabled)
            {
                _animator.SetBool(IsMovingHash, moving);
            }
        }

        // Face와 같은 규칙: flipX == (left == _spriteFacesRight)
        internal override bool IsFacingLeft =>
            _renderers != null && _renderers.Length > 0 && _renderers[0] != null && _renderers[0].flipX == _spriteFacesRight;

        internal override void Face(bool left)
        {
            bool flip = left == _spriteFacesRight;
            foreach (var spriteRenderer in _renderers)
            {
                if (spriteRenderer != null)
                {
                    spriteRenderer.flipX = flip;
                }
            }
        }

        private bool HasMovingParameter()
        {
            foreach (var parameter in _animator.parameters)
            {
                if (parameter.nameHash == IsMovingHash && parameter.type == AnimatorControllerParameterType.Bool)
                    return true;
            }

            return false;
        }
    }
}
