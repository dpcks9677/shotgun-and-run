using System.Collections;
using Consumable;
using ShotgunAndRun.UI.Dialogue;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// (프로토타입) CS_TUTORIAL_FAINT_*: 보라 구간 종료. 적을 모두 처치했든 체력이 바닥났든 플레이어가 한마디를 하고 화면이 암전되며,
    /// 암전 중에 남은 적이 정리된다. 3초 기다린 뒤 검은 마커 위에서 체력 1칸으로 깨어나 소형 구급상자를 받고, 회복 안내(TutorialFlow)가 이어진다.
    /// 같은 코드로 에셋 둘을 만든다: 패배(대사 → 암전 → "(철컥" → 총성 3발 → 3초) / 모두 처치(대사 2줄 → 암전 → 3초).
    /// 사망 연출(사망음)은 쓰지 않는다. 대본: Docs/quests/CS_TUTORIAL.md 3-6.
    /// </summary>
    [CreateAssetMenu(fileName = "CS_TUTORIAL_FAINT", menuName = "ShotgunAndRun/Cutscene/Tutorial/Faint")]
    public sealed class TutorialFaintCutscene : CutsceneAsset
    {
        private const string FaintSpot = "FaintSpot";
        private const string ClearEnemiesSignal = "ClearArenaEnemies";

        [Header("대사")]
        [Tooltip("암전 전에 플레이어가 하는 말")]
        [SerializeField] private DialogueDataSO _beforeFade;

        [Tooltip("암전된 뒤에 나오는 문구 (선택, 예: '(철컥')")]
        [SerializeField] private DialogueDataSO _inBlackout;

        [Tooltip("깨어난 직후 플레이어의 혼잣말 (선택). 회복 안내가 뜨기 전에 나옵니다.")]
        [SerializeField] private DialogueDataSO _afterWake;

        [Header("지급 아이템")]
        [Tooltip("깨어난 뒤 퀵슬롯에 지급할 1칸 회복 소모품 (SmallKit)")]
        [SerializeField] private ConsumableData _healItem;

        [Header("쓰러지는 모습")]
        [Tooltip("쓰러질 때 재생하고 마지막 모습을 유지할 클립 (플레이어의 Dead 애니메이션을 기절 표현으로 재사용)")]
        [SerializeField] private AnimationClip _faintClip;

        [Header("효과음 (임시, 비워 두면 소리 없이 진행)")]
        [SerializeField] private AudioClip _gunshot;
        [Tooltip("쓰러지는 순간의 소리 (예: 이명 · 심장박동). 사망음은 쓰지 않습니다.")]
        [SerializeField] private AudioClip _collapseSfx;

        [Header("연출 수치")]
        [Tooltip("암전 중 울릴 총성 횟수 (0이면 없음)")]
        [SerializeField, Min(0)] private int _gunshotCount = 3;
        [SerializeField, Min(0f)] private float _shotInterval = 0.7f;
        [Tooltip("암전된 채로 기다리는 시간 (총성이 있으면 총성이 끝난 뒤부터)")]
        [SerializeField, Min(0f)] private float _blackoutWaitSeconds = 3f;
        [SerializeField, Min(1)] private int _wakeHealth = 1;
        [SerializeField, Min(0f)] private float _collapsedHoldSeconds = 0.6f;
        [SerializeField, Min(0f)] private float _fadeOutSeconds = 0.8f;
        [SerializeField, Min(0f)] private float _wakeFadeSeconds = 2f;

        protected override void DeclareRequirements(CutsceneRequirements r)
        {
            r.Dialogue(_beforeFade, nameof(_beforeFade));
            if (_inBlackout != null)
            {
                r.Dialogue(_inBlackout, nameof(_inBlackout));
            }

            if (_afterWake != null)
            {
                r.Dialogue(_afterWake, nameof(_afterWake));
            }

            r.Asset(_healItem, nameof(_healItem));
            r.Asset(_faintClip, nameof(_faintClip));
            r.Check(_gunshotCount == 0 || _gunshot != null, "총성 횟수가 있는데 Gunshot 칸이 비어 있습니다.");
            r.Marker(FaintSpot);
            r.Signal(ClearEnemiesSignal);
        }

        protected override IEnumerator Play(CutsceneContext c)
        {
            var player = c.Player;

            // 쓰러지기 전에 한마디
            c.Letterbox(true);
            yield return c.Say(_beforeFade);

            // 쓰러진다 (사망 애니메이션의 마지막 모습을 유지). 사망음은 쓰지 않는다. 총은 손에서 놓쳤으므로 숨긴다
            c.SetPlayerWeaponHidden(true);
            if (_collapseSfx != null)
            {
                c.PlaySfx(_collapseSfx);
            }

            yield return c.PlayAnimation(player, _faintClip, holdLastFrame: true);
            yield return c.Wait(_collapsedHoldSeconds);

            // 암전. 그 사이 남은 적은 정리된다
            yield return c.FadeOut(_fadeOutSeconds);
            c.ClearBubbles();
            c.Signal(ClearEnemiesSignal);

            if (_inBlackout != null)
            {
                yield return c.Say(_inBlackout);
            }

            // 기절한 사이 자기도 모르게 쏘는 총성
            if (_gunshotCount > 0 && _gunshot != null)
            {
                yield return c.PlaySfxBurst(_gunshot, _gunshotCount, _shotInterval);
            }

            yield return c.Wait(_blackoutWaitSeconds);

            // 검은 마커 위에서 체력 1칸으로 깨어난다 (사망 연출은 쓰지 않는다)
            c.Teleport(player, FaintSpot);
            c.StopAnimation(player);
            c.SetPlayerWeaponHidden(false);
            c.CameraToPlayer(0f);
            c.SetPlayerHealth(_wakeHealth);
            c.GiveConsumable(_healItem, 1);
            yield return c.FadeIn(_wakeFadeSeconds);

            // 깨어난 직후 혼잣말. 끝나면 퀵슬롯이 열리고 회복 안내(TutorialFlow)가 이어진다
            if (_afterWake != null)
            {
                yield return c.Say(_afterWake);
            }

            c.Letterbox(false);
            c.ShowHud("QuickSlot");
        }
    }
}
