using System.Collections;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 런 사망 연출. 쓰러지는 애니메이션 · 사망음은 플레이어가 죽을 때 이미 시작되고(PlayerFeedback),
    /// 이 컷씬은 무기를 숨기고 레터박스와 함께 쓰러진 플레이어를 확대해 잠시 보여 준 뒤 화면을 검게 덮는다.
    /// 컷씬이 끝나면 화면 덮개가 걷히므로, 검은 화면을 이어받아 결과창을 띄우는 일은 호출한 쪽(RunEndSequence)이 맡는다.
    /// 에셋 설정: AutoPlay OFF · LockPlayer ON · HideHud ON · PauseGameplay ON(적이 멈춤), 플래그 없음.
    /// </summary>
    [CreateAssetMenu(fileName = "CS_RUN_DEATH", menuName = "ShotgunAndRun/Cutscene/Common/Run Death")]
    public sealed class RunDeathCutscene : CutsceneAsset
    {
        [Header("연출 수치")]
        [Tooltip("컷씬 시작 때 카메라 크기 대비 확대 비율 (1보다 작을수록 가까이)")]
        [SerializeField, Range(0.3f, 1f)] private float _zoomRatio = 0.7f;
        [SerializeField, Min(0f)] private float _zoomSeconds = 1.2f;
        [SerializeField, Min(0f)] private float _letterboxSeconds = 0.4f;
        [Tooltip("확대가 끝난 뒤 쓰러진 모습을 보여 주는 시간")]
        [SerializeField, Min(0f)] private float _holdSeconds = 1f;
        [Tooltip("쓰러진 모습을 보여 준 뒤 화면이 검게 덮이는 시간")]
        [SerializeField, Min(0f)] private float _fadeSeconds = 1f;

        [Header("효과음 (비워 두면 소리 없이 진행)")]
        [Tooltip("종소리 뒤에 이어지는 짧은 음악. 확대가 끝나고 쓰러진 모습을 보여 줄 때 재생")]
        [SerializeField] private AudioClip _stinger;

        protected override void DeclareRequirements(CutsceneRequirements r)
        {
        }

        protected override IEnumerator Play(CutsceneContext c)
        {
            // 총은 손에서 놓쳤으므로 숨긴다 (컷씬 정리 때 다시 보임)
            c.SetPlayerWeaponHidden(true);
            c.Letterbox(true, _letterboxSeconds);
            c.CameraToPlayer(0f);
            yield return c.CameraZoomScale(_zoomRatio, _zoomSeconds);

            if (_stinger != null)
            {
                c.PlaySfx(_stinger);
            }

            yield return c.Wait(_holdSeconds);
            yield return c.FadeOut(_fadeSeconds);
        }
    }
}
