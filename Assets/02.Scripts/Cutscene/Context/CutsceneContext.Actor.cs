using System;
using System.Collections;
using System.Linq;
using ShotgunAndRun.UI.Dialogue;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    // 인물 명령: 찾기 · 이동 · 방향 · 애니메이션 · 보이기
    public sealed partial class CutsceneContext
    {
        /// <summary>
        /// 프로필로 인물을 찾습니다. DeclareRequirements에 선언한 인물만 찾을 수 있습니다.
        /// </summary>
        public CutsceneActor Actor(CharacterProfileSO profile)
        {
            if (profile == null || !_actors.TryGetValue(profile, out var actor) ||
                (actor != Player && !_requirements.Actors.Contains(profile)))
            {
                throw new InvalidOperationException($"'{(profile != null ? profile.name : "null")}' 인물은 DeclareRequirements에 선언되지 않았습니다.");
            }

            return actor;
        }

        /// <summary>
        /// 인물을 마커 위치로 즉시 옮깁니다.
        /// </summary>
        public void Teleport(CutsceneActor actor, string marker)
        {
            RequireActor(actor);
            Cancel(MoveOwner(actor));
            actor.Teleport(MarkerPosition(marker));
        }

        /// <summary>
        /// 인물을 월드 좌표로 즉시 옮깁니다. 컷씬 전에 서 있던 자리로 되돌릴 때 씁니다.
        /// </summary>
        public void TeleportToPosition(CutsceneActor actor, Vector3 position)
        {
            RequireActor(actor);
            Cancel(MoveOwner(actor));
            actor.Teleport(position);
        }

        /// <summary>
        /// 인물을 마커까지 걸어가게 합니다. speed를 생략하면 인물의 기본 이동 속도입니다. 이동 방향을 바라봅니다.
        /// </summary>
        public CutsceneHandle MoveTo(CutsceneActor actor, string marker, float speed = 0f)
        {
            return MovePath(actor, speed, marker);
        }

        /// <summary>
        /// 인물을 여러 마커를 차례로 거쳐 걸어가게 합니다.
        /// </summary>
        public CutsceneHandle MovePath(CutsceneActor actor, float speed, params string[] markers)
        {
            RequireActor(actor);
            var points = new Vector3[markers.Length];
            for (int i = 0; i < markers.Length; i++)
            {
                points[i] = MarkerPosition(markers[i]);
            }

            return MovePathToPositions(actor, speed, points);
        }

        /// <summary>
        /// 인물을 월드 좌표 여러 점을 차례로 거쳐 걸어가게 합니다. 마커로 경유지를 잡고 마지막만 계산한 좌표로 보낼 때 씁니다.
        /// </summary>
        public CutsceneHandle MovePathToPositions(CutsceneActor actor, float speed, params Vector3[] points)
        {
            RequireActor(actor);
            return Run(MoveRoutine(actor, points, speed > 0f ? speed : actor.DefaultMoveSpeed), MoveOwner(actor));
        }

        /// <summary>
        /// 인물을 지금 바라보는 방향 그대로 둔 채 마커까지 뒷걸음질로 옮깁니다. (뒤를 겨누며 물러서는 연출)
        /// </summary>
        public CutsceneHandle MoveBackward(CutsceneActor actor, string marker, float speed = 0f)
        {
            RequireActor(actor);
            var points = new[] { MarkerPosition(marker) };
            return Run(MoveRoutine(actor, points, speed > 0f ? speed : actor.DefaultMoveSpeed, faceMovement: false), MoveOwner(actor));
        }

        /// <summary>
        /// 인물을 지금 바라보는 방향 그대로 둔 채 월드 좌표 한 점까지 뒷걸음질로 옮깁니다.
        /// </summary>
        public CutsceneHandle MoveBackwardToPosition(CutsceneActor actor, Vector3 position, float speed = 0f)
        {
            RequireActor(actor);
            return Run(MoveRoutine(actor, new[] { position }, speed > 0f ? speed : actor.DefaultMoveSpeed, faceMovement: false), MoveOwner(actor));
        }

        /// <summary>
        /// 인물을 월드 좌표 한 점까지 걸어가게 합니다. 마커가 없는 곳(화면 밖으로 퇴장 등)으로 보낼 때 씁니다.
        /// </summary>
        public CutsceneHandle MoveToPosition(CutsceneActor actor, Vector3 position, float speed = 0f)
        {
            RequireActor(actor);
            return Run(MoveRoutine(actor, new[] { position }, speed > 0f ? speed : actor.DefaultMoveSpeed), MoveOwner(actor));
        }

        /// <summary>
        /// DeclareRequirements에 선언한 마커의 월드 좌표
        /// </summary>
        public Vector3 MarkerPoint(string marker)
        {
            return MarkerPosition(marker);
        }

        /// <summary>
        /// 인물이 왼쪽 또는 오른쪽을 보게 합니다.
        /// </summary>
        public void Face(CutsceneActor actor, bool left)
        {
            RequireActor(actor);
            actor.Face(left);
        }

        /// <summary>
        /// 인물이 한 지점을 겨누게 합니다. 플레이어는 총구가 그 지점을 향하고(인물이 움직여도 같은 방향 유지), 다른 인물은 그쪽을 보기만 합니다.
        /// </summary>
        public void AimAt(CutsceneActor actor, Vector3 point)
        {
            RequireActor(actor);
            actor.AimAt(point);
        }

        /// <summary>
        /// 인물이 지금 왼쪽을 보고 있는지 확인합니다. 컷씬 뒤 원래 방향으로 되돌릴 때 씁니다.
        /// </summary>
        public bool IsFacingLeft(CutsceneActor actor)
        {
            RequireActor(actor);
            return actor.IsFacingLeft;
        }

        /// <summary>
        /// 인물이 다른 인물 쪽을 보게 합니다.
        /// </summary>
        public void FaceTo(CutsceneActor actor, CutsceneActor target)
        {
            RequireActor(actor);
            RequireActor(target);
            actor.Face(target.Position.x < actor.Position.x);
        }

        /// <summary>
        /// 인물이 마커 쪽을 보게 합니다.
        /// </summary>
        public void FaceTo(CutsceneActor actor, string marker)
        {
            RequireActor(actor);
            actor.Face(MarkerPosition(marker).x < actor.Position.x);
        }

        /// <summary>
        /// 두 인물이 서로 마주 보게 합니다.
        /// </summary>
        public void FaceEachOther(CutsceneActor a, CutsceneActor b)
        {
            FaceTo(a, b);
            FaceTo(b, a);
        }

        /// <summary>
        /// 인물의 Animator Controller를 바꾸지 않고 클립 하나를 재생합니다. (예: Elena_Cast)
        /// 반복 클립이 아니면 클립이 끝날 때 원래 애니메이션으로 돌아가고 핸들이 끝납니다.
        /// 반복 클립이면 핸들은 바로 끝나고, StopAnimation을 부를 때까지 반복합니다.
        /// holdLastFrame이 true이면 반복 클립이 아니어도 끝난 뒤 원래 애니메이션으로 돌아가지 않고 마지막 모습을 유지합니다. (예: 쓰러진 모습)
        /// 핸들은 클립 길이만큼 지나면 끝나며, 유지한 모습은 StopAnimation을 부르거나 컷씬이 끝날 때 풀립니다.
        /// </summary>
        public CutsceneHandle PlayAnimation(CutsceneActor actor, AnimationClip clip, bool holdLastFrame = false)
        {
            RequireActor(actor);
            if (clip == null) throw new ArgumentNullException(nameof(clip));

            Cancel(AnimationOwner(actor));
            actor.PlayClip(clip);
            if (clip.isLooping) return CutsceneHandle.Done();

            if (holdLastFrame)
            {
                return Run(WaitRoutine(clip.length), AnimationOwner(actor));
            }

            return Run(PlayOnceRoutine(actor, clip.length), AnimationOwner(actor));
        }

        /// <summary>
        /// PlayAnimation으로 재생 중인 클립을 멈추고 원래 애니메이션으로 돌아갑니다.
        /// </summary>
        public void StopAnimation(CutsceneActor actor)
        {
            RequireActor(actor);
            Cancel(AnimationOwner(actor));
            actor.StopClip();
        }

        /// <summary>
        /// 인물의 스프라이트를 보이거나 숨깁니다. 컷씬이 끝나도 되돌리지 않습니다.
        /// </summary>
        public void SetVisible(CutsceneActor actor, bool visible)
        {
            RequireActor(actor);
            actor.SetVisible(visible);
        }

        private static string MoveOwner(CutsceneActor actor) => $"move:{actor.GetInstanceID()}";
        private static string AnimationOwner(CutsceneActor actor) => $"anim:{actor.GetInstanceID()}";

        private static IEnumerator MoveRoutine(CutsceneActor actor, Vector3[] points, float speed, bool faceMovement = true)
        {
            try
            {
                foreach (Vector3 point in points)
                {
                    Vector2 direction = point - actor.Position;
                    if (faceMovement && Mathf.Abs(direction.x) > 0.01f)
                    {
                        actor.Face(direction.x < 0f);
                    }
                    actor.SetMoving(true, direction);

                    while (((Vector2)(point - actor.Position)).sqrMagnitude > 0.0001f)
                    {
                        actor.StepTowards(point, speed * Time.deltaTime);
                        yield return null;
                    }
                    actor.Teleport(point);
                }
            }
            finally
            {
                actor.SetMoving(false, Vector2.zero);
            }
        }

        private static IEnumerator PlayOnceRoutine(CutsceneActor actor, float length)
        {
            try
            {
                yield return WaitRoutine(length);
            }
            finally
            {
                actor.StopClip();
            }
        }
    }
}
