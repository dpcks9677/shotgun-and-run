using System.Collections;
using ShotgunAndRun.UI.Dialogue;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// CS_ELENA_AMBUSH: 함정. 대본: Docs/quests/CS_Q2_MAGE_RESCUE.md 5장
    /// 결박된 엘레나가 함정을 외치고, 경보음과 함께 H2 홀 입구들에서 1웨이브 적이 정지한 채 나타나며,
    /// 카메라가 가까운 입구를 비춘 뒤 홀 중앙으로 줌아웃해 포위를 보여 주고 플레이어에게 돌아온다.
    /// 웨이브 시작(적 정지 해제 · 전투)은 컷씬이 끝난 뒤 이벤트 컴포넌트가 처리하며, 이 컷씬은 등장만 연출한다.
    /// 에셋 설정: PauseGameplay ON · 플레이어 조작 잠금 · HUD 숨김, AutoPlay OFF (이벤트 컴포넌트가 재생),
    /// Required FLAG_Q2_ACCEPTED, Blocked FLAG_ELENA_RESCUED + FLAG_ELENA_AMBUSH_SEEN, Completion FLAG_ELENA_AMBUSH_SEEN, SaveCompletionFlagsImmediately ON.
    /// </summary>
    [CreateAssetMenu(fileName = "CS_ELENA_AMBUSH", menuName = "ShotgunAndRun/Cutscene/Dungeon/Elena Ambush")]
    public sealed class ElenaAmbushDungeonCutscene : CutsceneAsset
    {
        // 씬에 같은 키의 CutsceneMarker가 있어야 한다
        private const string AmbushEntrance1 = "AmbushEntrance1";
        private const string AmbushCenter = "AmbushCenter";

        // 씬에 같은 키의 CutsceneSignal이 있어야 한다. 1웨이브 적을 정지 상태로 등장시킨다
        private const string AmbushReveal = "AmbushReveal";

        [Header("등장인물")]
        [SerializeField] private CharacterProfileSO _elena;

        [Header("대사")]
        [Tooltip("1줄: '함정이에요! 뒤를 조심해요!' (화자: 엘레나, 말풍선 Shock)")]
        [SerializeField] private DialogueDataSO _warning;

        [Header("경보음 (비워 두면 소리 없이 진행)")]
        [SerializeField] private AudioClip _alarm;

        [Header("연출 수치")]
        [SerializeField, Min(0f)] private float _shakePower = 0.08f;
        [SerializeField, Min(0f)] private float _shakeSeconds = 0.5f;
        [SerializeField, Min(0f)] private float _entranceBlend = 1.2f;
        [SerializeField, Min(0f)] private float _entranceHold = 0.8f;
        [SerializeField, Min(0f)] private float _centerBlend = 1.6f;
        [SerializeField, Min(0.1f)] private float _zoomOutSize = 9f;
        [SerializeField, Min(0f)] private float _zoomOutSeconds = 1.6f;
        [SerializeField, Min(0f)] private float _centerHold = 0.8f;
        [SerializeField, Min(0f)] private float _returnBlend = 0.8f;

        protected override void DeclareRequirements(CutsceneRequirements r)
        {
            r.Actor(_elena, nameof(_elena));
            r.Dialogue(_warning, nameof(_warning));
            r.Marker(AmbushEntrance1, AmbushCenter);
            r.Signal(AmbushReveal);
        }

        protected override IEnumerator Play(CutsceneContext c)
        {
            var elena = c.Actor(_elena);

            // 1비트: 경고. 경보음과 약한 흔들림 속에 1웨이브 적이 입구들에 정지 상태로 선다 (Shock 말풍선은 대사 데이터에 있음)
            c.FaceTo(elena, c.Player);
            c.Signal(AmbushReveal);
            if (_alarm != null)
            {
                c.PlaySfx(_alarm);
            }

            c.CameraShake(_shakePower, _shakeSeconds);
            yield return c.Say(_warning);

            // 2비트: 가장 가까운 입구의 적을 비춘다
            yield return c.CameraTo(AmbushEntrance1, _entranceBlend);
            yield return c.Wait(_entranceHold);

            // 3비트: 홀 중앙으로 옮기며 줌아웃해 모든 입구의 적을 한 화면에 보여 준다
            var pan = c.CameraTo(AmbushCenter, _centerBlend);
            var zoom = c.CameraZoom(_zoomOutSize, _zoomOutSeconds);
            yield return c.WaitAll(pan, zoom);
            yield return c.Wait(_centerHold);

            // 4비트: 플레이어에게 돌아오고 줌이 원래대로 돌아온다
            var back = c.CameraToPlayer(_returnBlend);
            var reset = c.CameraZoomReset(_returnBlend);
            yield return c.WaitAll(back, reset);
        }
    }
}
