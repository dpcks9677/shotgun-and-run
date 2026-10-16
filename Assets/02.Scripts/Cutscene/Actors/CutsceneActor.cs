using System.Collections.Generic;
using ShotgunAndRun.UI.Dialogue;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 컷씬에 등장하는 인물입니다. 인물 프로필(CharacterProfileSO)로 식별하며, 이름 문자열로 찾지 않습니다.
    /// NPC와 플레이어는 움직이는 방식이 달라 NpcCutsceneActor · PlayerCutsceneActor로 나뉩니다.
    /// </summary>
    public abstract class CutsceneActor : MonoBehaviour
    {
        [Tooltip("이 인물의 프로필. 컷씬과 대사가 이 프로필로 인물을 찾습니다.")]
        [SerializeField]
        private CharacterProfileSO _profile;

        [Tooltip("이동 명령에 속도를 지정하지 않았을 때의 이동 속도 (유닛/초)")]
        [SerializeField, Min(0.1f)]
        private float _defaultMoveSpeed = 2f;

        private PlayableGraph _clipGraph;

        public CharacterProfileSO Profile => _profile;
        public float DefaultMoveSpeed => _defaultMoveSpeed;
        public virtual Vector3 Position => transform.position;

        /// <summary>
        /// 감정 말풍선을 띄울 기준 Transform
        /// </summary>
        public virtual Transform BubbleTarget => transform;

        protected abstract Animator Animator
        {
            get;
        }

        /// <summary>
        /// 재생 전 사전 검사. 문제가 있으면 errors에 추가합니다.
        /// </summary>
        internal virtual void Validate(List<string> errors)
        {
            if (_profile == null)
            {
                errors.Add($"'{name}'의 CutsceneActor에 프로필이 비어 있습니다.");
            }
        }

        internal abstract void BeginCutscene(CutsceneContext context);
        internal abstract void EndCutscene();
        internal abstract void Teleport(Vector3 position);
        internal abstract void StepTowards(Vector3 target, float maxDistance);
        internal abstract void SetMoving(bool moving, Vector2 direction);
        internal abstract void Face(bool left);

        /// <summary>
        /// 한 지점을 겨눕니다. 무기를 겨누지 않는 인물은 그 지점 쪽을 보기만 합니다.
        /// </summary>
        internal virtual void AimAt(Vector3 point)
        {
            Face(point.x < Position.x);
        }

        /// <summary>
        /// 지금 왼쪽을 보고 있는지. 컷씬 뒤 원래 방향으로 되돌릴 때 씁니다.
        /// </summary>
        internal virtual bool IsFacingLeft => false;

        // 숨길 때 켜져 있던 렌더러. 다시 보일 때 이것만 켠다 (쉘터에서 꺼 둔 무기 스프라이트 등이 켜지지 않게)
        private List<SpriteRenderer> _hiddenRenderers;

        internal virtual void SetVisible(bool visible)
        {
            if (!visible)
            {
                _hiddenRenderers ??= new List<SpriteRenderer>();
                foreach (var spriteRenderer in GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (!spriteRenderer.enabled) continue;
                    _hiddenRenderers.Add(spriteRenderer);
                    spriteRenderer.enabled = false;
                }

                return;
            }

            if (_hiddenRenderers != null)
            {
                foreach (var spriteRenderer in _hiddenRenderers)
                {
                    if (spriteRenderer != null) spriteRenderer.enabled = true;
                }

                _hiddenRenderers = null;
                return;
            }

            foreach (var spriteRenderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                spriteRenderer.enabled = true;
            }
        }

        internal void AssignProfile(CharacterProfileSO profile)
        {
            _profile = profile;
        }

        /// <summary>
        /// Animator Controller를 바꾸지 않고 클립 하나를 잠시 덮어써서 재생합니다. StopClip으로 원래 애니메이션에 돌아갑니다.
        /// </summary>
        internal void PlayClip(AnimationClip clip)
        {
            StopClip();
            AnimationPlayableUtilities.PlayClip(Animator, clip, out _clipGraph);
        }

        internal void StopClip()
        {
            if (_clipGraph.IsValid())
            {
                _clipGraph.Destroy();
            }
        }

        protected virtual void OnDisable()
        {
            StopClip();
        }
    }
}
