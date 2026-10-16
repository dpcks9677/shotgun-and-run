using System.Collections;
using ShotgunAndRun.UI.Dialogue;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// CS_ELENA_TELEPORT: 먼저 갈게요. 대본: Docs/quests/CS_Q2_MAGE_RESCUE.md 6장
    /// 결박이 풀린 엘레나가 쓰러진 자세에서 일어서 감사 인사를 하고, 지팡이를 들어 마법진(씬 FX)을 띄우며 쉘터로 먼저 건너간다.
    /// 흰 섬광과 함께 엘레나가 사라진다. 카메라는 움직이지 않는다.
    /// 에셋 설정: PauseGameplay ON · 플레이어 조작 잠금 · HUD 숨김, AutoPlay OFF (이벤트 컴포넌트가 재생),
    /// Required FLAG_Q2_ACCEPTED, Blocked FLAG_ELENA_RESCUED + FLAG_ELENA_TELEPORT_SEEN, Completion FLAG_ELENA_TELEPORT_SEEN, SaveCompletionFlagsImmediately ON.
    /// </summary>
    [CreateAssetMenu(fileName = "CS_ELENA_TELEPORT", menuName = "ShotgunAndRun/Cutscene/Dungeon/Elena Teleport")]
    public sealed class ElenaTeleportDungeonCutscene : CutsceneAsset
    {
        // 씬에 같은 키의 CutsceneSignal이 있어야 한다
        private const string CastFx = "ElenaCastFx";
        private const string ElenaGone = "ElenaGone";

        [Header("등장인물")]
        [SerializeField] private CharacterProfileSO _elena;

        [Header("대사")]
        [Tooltip("감사 · 대화. 줄 수는 자유이며, 마지막 줄(전이 선언)이 나올 때 지팡이를 들어 마법진을 띄운다")]
        [SerializeField] private DialogueDataSO _lines;

        [Header("애니메이션 클립")]
        [Tooltip("쓰러진 모습에서 일어서는 모습 (Elena_GetUp, 반복 아님). 끝나면 애니메이터의 대기 모습(Elena_Idle)으로 돌아간다.")]
        [SerializeField] private AnimationClip _getUpClip;

        [Tooltip("지팡이를 드는 시전 모습 (Elena_Cast)")]
        [SerializeField] private AnimationClip _castClip;

        [Header("효과음 (비워 두면 소리 없이 진행)")]
        [SerializeField] private AudioClip _ropeRelease;
        [Tooltip("플레이어 순간이동과 같은 클립(teleport_out). 충전부터 시작하는 클립이라 섬광 Charge Seconds 전에 재생한다")]
        [SerializeField] private AudioClip _teleport;

        [Header("연출 수치")]
        [Tooltip("마지막 대사가 끝나고 섬광까지의 시간. 플레이어 순간이동의 물드는 시간(CS_TELEPORT_ENTER의 White Seconds)과 맞춘다")]
        [SerializeField, Min(0f)] private float _chargeSeconds = 1f;
        [SerializeField, Min(0f)] private float _flashSeconds = 0.6f;
        [SerializeField, Min(0f)] private float _afterFlashSeconds = 0.4f;

        protected override void DeclareRequirements(CutsceneRequirements r)
        {
            r.Actor(_elena, nameof(_elena));
            r.Dialogue(_lines, nameof(_lines));
            r.Asset(_getUpClip, nameof(_getUpClip));
            r.Asset(_castClip, nameof(_castClip));
            r.Signal(CastFx, ElenaGone);
        }

        protected override IEnumerator Play(CutsceneContext c)
        {
            var elena = c.Actor(_elena);
            c.FaceTo(elena, c.Player);

            // 1비트: 밧줄이 풀리고 쓰러져 있던 엘레나가 일어선다 (사망 모습을 거꾸로, 끝나면 대기 모습으로 돌아간다)
            PlaySfx(c, _ropeRelease);
            c.PlayAnimation(elena, _getUpClip);

            yield return c.Say(_lines, line =>
            {
                if (line != _lines.Lines.Count - 1) return;

                // 2비트: 마지막 줄에서 지팡이를 들자 발밑에 마법진이 뜬다
                c.PlayAnimation(elena, _castClip);
                c.Signal(CastFx);
            });

            // 3비트: 순간이동음이 충전되는 동안 시전 모습(반복 클립)을 유지한 뒤 흰 섬광과 함께 엘레나가 사라진다
            PlaySfx(c, _teleport);
            yield return c.Wait(_chargeSeconds);
            var flash = c.Flash(Color.white, _flashSeconds);
            c.SetVisible(elena, false);
            c.Signal(ElenaGone);
            yield return flash;
            yield return c.Wait(_afterFlashSeconds);
        }

        private static void PlaySfx(CutsceneContext c, AudioClip clip)
        {
            if (clip != null)
            {
                c.PlaySfx(clip);
            }
        }
    }
}
