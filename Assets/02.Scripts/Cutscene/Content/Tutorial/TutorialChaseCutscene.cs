using System.Collections;
using ShotgunAndRun.UI.Dialogue;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// (프로토타입) CS_TUTORIAL_CHASE: 회색 구간. 회복 직후 혼잣말을 하면, 카메라가 복도 중간으로 옮겨 가고 복도를 가득 채운 괴물 무리 15마리가
    /// 복도 초입에서 끝까지 화면을 가로질러 달려 지나간다. 플레이어가 두 마디("떼거지로 몰려오잖아?" · "이건 혼자 상대할 수 없어... 도망가야해!")를 하고
    /// 두 번째 대사가 나오면 카메라가 플레이어에게 돌아와 회색 통로 끝까지 달린다. 이때 무리는 플레이어 뒤 1.8유닛까지 아슬아슬하게 따라붙어 같은 속도로 쫓아온다.
    /// 카메라는 도착 지점에서 멈추고 플레이어가 화면 밖으로 퇴장하며 페이드아웃으로 튜토리얼이 끝난다. 대본: Docs/quests/CS_TUTORIAL.md 3-7.
    /// 무리는 연출 전용(CutsceneCrowd)이라 플레이어를 공격하지 않고 대형을 유지한 채 움직인다. 무리가 지나가는 줄은 복도 아래쪽이라 위쪽 벽의 상자를 밟지 않는다.
    /// </summary>
    [CreateAssetMenu(fileName = "CS_TUTORIAL_CHASE", menuName = "ShotgunAndRun/Cutscene/Tutorial/Chase")]
    public sealed class TutorialChaseCutscene : CutsceneAsset
    {
        private const string HordeKey = "Horde";
        private const string HordeCamera = "HordeCamera";
        private const string HordeRun1 = "HordeRun1";
        private const string ChaseRun1 = "ChaseRun1";
        private const string ChaseEnd = "ChaseEnd";
        private const string CompleteSignal = "TutorialComplete";

        [Header("대사")]
        [Tooltip("2줄: '떼거지로 몰려오잖아?' · '이건 혼자 상대할 수 없어... 도망가야해!' (화자: 플레이어)")]
        [SerializeField] private DialogueDataSO _lines;

        [Tooltip("컷씬 맨 앞, 회복을 마친 직후의 혼잣말 (선택). 무리가 나타나기 전에 나옵니다.")]
        [SerializeField] private DialogueDataSO _opening;

        [Header("효과음 (임시, 비워 두면 소리 없이 진행)")]
        [SerializeField] private AudioClip _hordeRoar;

        [Header("연출 수치")]
        [Tooltip("카메라가 플레이어에서 복도 중간(HordeCamera)으로 옮겨 가는 시간 (끊겨 보이지 않게 순간 이동 대신 부드럽게)")]
        [SerializeField, Min(0.1f)] private float _cameraToHordeBlend = 1.2f;
        [Tooltip("무리가 복도 초입에서 끝(HordeRun1)까지 달려 지나가는 속도")]
        [SerializeField, Min(0.1f)] private float _hordeArriveSpeed = 10f;
        [Tooltip("플레이어가 뛰기 시작하면 무리의 맨 앞 개체가 플레이어 뒤 이만큼(유닛)까지 붙는다. 작을수록 아슬아슬하다")]
        [SerializeField, Min(0.5f)] private float _hordeGap = 1.8f;
        [Tooltip("플레이어 뒤로 붙기까지 무리가 따라잡는 속도")]
        [SerializeField, Min(0.1f)] private float _hordeCatchUpSpeed = 16f;
        [SerializeField, Min(0f)] private float _hordeStartDelayStep = 0.02f;
        [SerializeField, Min(0.1f)] private float _playerRunSpeed = 9.5f;
        [Tooltip("대사가 끝난 뒤 카메라가 플레이어에게 돌아오는 시간")]
        [SerializeField, Min(0f)] private float _cameraFollowBlend = 1f;
        [SerializeField, Min(0f)] private float _fadeOutSeconds = 1.5f;
        [Tooltip("카메라가 도착 지점(ChaseEnd)에 멈춘 뒤, 플레이어가 화면 오른쪽 끝에서 이만큼(유닛) 더 달려 완전히 퇴장합니다.")]
        [SerializeField, Min(0f)] private float _exitMargin = 3f;

        protected override string DefaultDebugSpawnMarker => "ChaseRun1";

        protected override void DeclareRequirements(CutsceneRequirements r)
        {
            r.Dialogue(_lines, nameof(_lines));
            if (_opening != null)
            {
                r.Dialogue(_opening, nameof(_opening));
            }

            r.Crowd(HordeKey);
            r.Marker(HordeCamera, HordeRun1, ChaseRun1, ChaseEnd);
            r.Signal(CompleteSignal);
        }

        protected override IEnumerator Play(CutsceneContext c)
        {
            var horde = c.Crowd(HordeKey);
            var player = c.Player;

            // 무리는 복도 초입(카메라 왼쪽 밖)에서 복도 폭을 채운 채 출발한다. 플레이어는 무리가 오는 쪽(왼쪽)을 본다
            c.Letterbox(true);
            c.Face(player, left: true);

            // 회복을 마친 직후 한마디. 무리는 이 대사가 끝난 뒤에 나타난다
            if (_opening != null)
            {
                yield return c.Say(_opening);
            }

            c.SetVisible(horde, true);

            if (_hordeRoar != null)
            {
                c.PlaySfx(_hordeRoar);
            }

            // 카메라는 복도 중간에 두고, 무리가 복도 초입에서 끝까지 화면을 가로질러 달려 지나간다.
            // 끝 지점(HordeRun1)은 카메라 화면 오른쪽 밖이라, 도망이 시작되길 기다리는 동안에는 화면에 보이지 않는다
            c.CrowdMovePath(horde, _hordeArriveSpeed, _hordeStartDelayStep, HordeRun1);
            c.CameraShake(0.12f, 2.5f);
            yield return c.CameraTo(HordeCamera, _cameraToHordeBlend);

            // 첫 대사("떼거지로 몰려오잖아?")를 넘겨 두 번째 대사가 나오면 도망이 시작된다. 두 번째 대사는 달리는 동안 떠 있다
            bool flee = false;
            var say = c.Say(_lines, line =>
            {
                if (line == 1)
                {
                    flee = true;
                    c.CameraShake(0.1f, 1.5f);
                }
            });
            while (!flee && !say.IsDone)
            {
                yield return null;
            }

            // 카메라가 플레이어를 따라가며 회색 통로 끝까지 달린다. 무리는 끝까지 갈 것 없이 플레이어 바로 뒤에 아슬아슬하게 붙어 따라 달린다
            c.CameraToPlayer(_cameraFollowBlend);
            c.CrowdFollow(horde, player, _hordeGap, _playerRunSpeed, _hordeCatchUpSpeed);
            yield return c.MovePath(player, _playerRunSpeed, ChaseRun1, ChaseEnd);

            // 카메라는 도착 지점에서 멈추고, 플레이어는 통로를 계속 달려 화면 밖으로 퇴장한다. 퇴장에 맞춰 페이드 아웃이 끝난다
            c.CameraTo(ChaseEnd, 0f);
            Vector3 stop = c.MarkerPoint(ChaseEnd);
            float exitDistance = c.CameraHalfWidth + _exitMargin;
            var exit = c.MoveToPosition(player, new Vector3(stop.x + exitDistance, stop.y, stop.z), _playerRunSpeed);

            float runSeconds = exitDistance / _playerRunSpeed;
            yield return c.Wait(Mathf.Max(0f, runSeconds - _fadeOutSeconds * 0.8f));
            var fade = c.FadeOut(_fadeOutSeconds);
            yield return exit;
            yield return fade;
            c.Signal(CompleteSignal);
        }
    }
}
