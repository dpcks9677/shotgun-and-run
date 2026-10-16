using System.Collections;
using ShotgunAndRun.UI.Dialogue;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// CS_Q2_PORTAL: 다시 열린 문. 대본: Docs/quests/CS_Q2_MAGE_RESCUE.md 7장
    /// 탈출 뒤 쉘터에 돌아오면 엘레나가 플레이어를 맞이해 무너진 계단 자리의 포탈로 데려가고, 시전으로 포탈에 불을 넣은 뒤 랜덤 스폰을 설명합니다.
    /// 에셋 설정: Required FLAG_ELENA_RESCUED / Blocked FLAG_Q2_PORTAL_DONE /
    /// Completion FLAG_Q2_PORTAL_DONE / AutoPlay 켜기 (완료 플래그 즉시 저장은 쓰지 않음)
    /// </summary>
    [CreateAssetMenu(fileName = "CS_Q2_PORTAL", menuName = "ShotgunAndRun/Cutscene/Shelter/Q2 Portal")]
    public sealed class Q2PortalShelterCutscene : CutsceneAsset
    {
        // 씬에 같은 키의 CutsceneMarker가 있어야 한다
        private const string ElenaCenter = "ElenaCenter";
        private const string PlayerMeetElena = "PlayerMeetElena";
        private const string PortalFront = "PortalFront";
        private const string PlayerPortal = "PlayerPortal";
        private const string PortalCamera = "PortalCamera";

        // 씬에 같은 키의 CutsceneSignal이 있어야 한다 (포탈 상태 교체 + FX)
        private const string PortalIgniteSignal = "PortalIgnite";

        [Header("등장인물")]
        [SerializeField] private CharacterProfileSO _elena;

        [Header("대사")]
        [Tooltip("1비트~2비트 시작: 대본 01~02 (재회 · 따라오라는 말)")]
        [SerializeField] private DialogueDataSO _reunion;

        [Tooltip("3비트: 대본 03 (포탈 프레임 앞)")]
        [SerializeField] private DialogueDataSO _frame;

        [Tooltip("4비트: 대본 04~06 (랜덤 스폰 설명)")]
        [SerializeField] private DialogueDataSO _explain;

        [Header("애니메이션")]
        [Tooltip("엘레나가 포탈에 불을 넣을 때 재생할 시전 클립 (Elena_Cast)")]
        [SerializeField] private AnimationClip _castClip;

        [Header("연출 수치")]
        [SerializeField, Min(0f)] private float _fadeSeconds = 1f;
        [SerializeField, Min(0.1f)] private float _walkSpeed = 2.5f;
        [SerializeField, Min(0f)] private float _followBlend = 0.6f;
        [SerializeField, Min(0f)] private float _portalCameraBlend = 1f;
        [SerializeField, Min(0f)] private float _igniteShakePower = 0.08f;
        [SerializeField, Min(0f)] private float _igniteShakeSeconds = 0.6f;

        protected override void DeclareRequirements(CutsceneRequirements r)
        {
            r.Actor(_elena, nameof(_elena));
            r.Dialogue(_reunion, nameof(_reunion));
            r.Dialogue(_frame, nameof(_frame));
            r.Dialogue(_explain, nameof(_explain));
            r.Asset(_castClip, nameof(_castClip));
            r.Marker(ElenaCenter, PlayerMeetElena, PortalFront, PlayerPortal, PortalCamera);
            r.Signal(PortalIgniteSignal);
        }

        protected override IEnumerator Play(CutsceneContext c)
        {
            var elena = c.Actor(_elena);

            // 배치: 엘레나는 쉘터 중앙, 플레이어는 귀환 위치 그대로. 화면은 검게 덮어 둔다
            c.Teleport(elena, ElenaCenter);
            c.CameraTo(ElenaCenter, 0f);
            c.FadeOut(0f);

            // 1비트: 페이드 인, 플레이어가 엘레나 앞까지 걸어오고 엘레나가 맞이한다
            c.FadeIn(_fadeSeconds);
            c.CameraTo(c.Player, _followBlend);
            yield return c.MoveTo(c.Player, PlayerMeetElena, _walkSpeed);
            c.FaceEachOther(c.Player, elena);
            yield return c.Say(_reunion);

            // 2비트: 둘이 포탈 앞까지 걸어간다. 카메라는 플레이어를 따라간다
            var elenaWalk = c.MoveTo(elena, PortalFront, _walkSpeed);
            var playerWalk = c.MoveTo(c.Player, PlayerPortal, _walkSpeed);
            yield return c.WaitAll(elenaWalk, playerWalk);
            c.FaceEachOther(c.Player, elena);

            // 3비트: 불 꺼진 포탈 프레임을 비추고, 엘레나가 시전해 포탈에 불을 넣는다 (포탈 교체는 씬이 신호를 받아 처리)
            yield return c.CameraTo(PortalCamera, _portalCameraBlend);
            yield return c.Say(_frame);
            // 시전 클립은 반복 클립이라 핸들이 바로 끝난다: 클립 길이만큼 직접 기다린 뒤 점화하고 멈춘다
            c.PlayAnimation(elena, _castClip);
            yield return c.Wait(_castClip.length);
            c.Signal(PortalIgniteSignal);
            c.StopAnimation(elena);
            yield return c.CameraShake(_igniteShakePower, _igniteShakeSeconds);

            // 4비트: 엘레나가 플레이어 쪽으로 돌아서서 랜덤 스폰을 설명한다 (대사별 말풍선은 대사 데이터에 있음)
            c.FaceEachOther(c.Player, elena);
            yield return c.Say(_explain);

            // 마무리: 페이드 아웃 뒤 카메라를 플레이어에게 돌리고 페이드 인
            yield return c.FadeOut(_fadeSeconds);
            yield return c.CameraToPlayer(0f);
            yield return c.FadeIn(_fadeSeconds);
        }
    }
}
