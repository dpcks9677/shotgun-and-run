using System.Collections;
using ShotgunAndRun.Cutscene;
using UnityEngine;

namespace ShotgunAndRun.UI
{
    /// <summary>
    /// CS_GRUN_REINFORCE: 무기 강화 연출 틀. HUD · NPC 하이라이트를 끄고 카메라를 확대한 뒤 그룬의 망치질이 끝나면 원래대로 되돌립니다.
    /// 되돌리기(HUD 복구 · NPC 하이라이트 · 조작 잠금)는 컷씬 종료 처리가, 카메라는 이 코드가 부드럽게 맡습니다.
    /// 망치질 자체는 씬의 GrunForgeSequence가 하고, 이 컷씬은 GrunForge 신호로 시작시킨 뒤 끝날 때까지 기다립니다.
    /// GrunForgeSequence를 볼 수 있도록 Cutscene 어셈블리 밖(UI 폴더)에 둡니다.
    /// 에셋 설정: LockPlayer ON · HideHud ON · PauseGameplay OFF, 플래그 없음, AutoPlay OFF
    /// </summary>
    [CreateAssetMenu(fileName = "CS_GRUN_REINFORCE", menuName = "ShotgunAndRun/Cutscene/Shelter/Grun Reinforce")]
    public sealed class GrunReinforceCutscene : CutsceneAsset
    {
        // 씬에 같은 키의 CutsceneSignal이 있어야 한다 (WeaponUpgradeUI.BeginForge 연결)
        private const string ForgeSignal = "GrunForge";

        [Tooltip("카메라 확대 배율 (1.1 = 1.1배 확대)")]
        [SerializeField, Min(1f)] private float _zoomIn = 1.1f;

        [SerializeField, Min(0f)] private float _zoomSeconds = 0.4f;

        protected override void DeclareRequirements(CutsceneRequirements r)
        {
            r.Signal(ForgeSignal);
        }

        protected override IEnumerator Play(CutsceneContext c)
        {
            yield return c.CameraZoomScale(1f / _zoomIn, _zoomSeconds);

            c.Signal(ForgeSignal);
            yield return c.WaitUntil(() => !GrunForgeSequence.IsAnyPlaying);

            yield return c.CameraZoomReset(_zoomSeconds);
        }
    }
}
