using System.Collections;
using ShotgunAndRun.UI.Dialogue;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// (프로토타입) CS_TUTORIAL_SKY_INTRO: 샷건 획득 직후. 카메라가 아래 복도에서 올라오는 일반 적 3마리를 비추고, 플레이어가 혼잣말을 한다.
    /// 마지막 대사("이걸 누르면 되는건가?") 뒤에 TutorialFlow가 시간을 멈추고 플레이어가 직접 발사하게 한다.
    /// </summary>
    [CreateAssetMenu(fileName = "CS_TUTORIAL_SKY_INTRO", menuName = "ShotgunAndRun/Cutscene/Tutorial/Sky Intro")]
    public sealed class TutorialSkyIntroCutscene : CutsceneAsset
    {
        private const string SkyFocus = "SkyFocus";
        private const string SkyArrive = "SkyArrive";
        private const string SkyGroup = "SkyEnemies";

        [Header("대사")]
        [Tooltip("3줄: '이런, 고블린이잖아...' · '이거라도 한 번 써봐야 하나?' · '이걸 누르면 되는건가?' (화자: 플레이어)")]
        [SerializeField] private DialogueDataSO _lines;

        [Header("효과음 (임시, 비워 두면 소리 없이 진행)")]
        [Tooltip("컷씬이 끝나 적이 달려들기 직전의 포효 · 경보음")]
        [SerializeField] private AudioClip _alertSfx;

        [Header("연출 수치")]
        [SerializeField, Min(0.1f)] private float _walkSpeed = 2.5f;
        [SerializeField, Min(0f)] private float _focusBlend = 1.2f;
        [SerializeField, Min(0f)] private float _focusHold = 0.6f;
        [SerializeField, Min(0f)] private float _returnBlend = 0.8f;

        protected override string DefaultDebugSpawnMarker => "GunFocus";

        // 샷건 단상(기둥) 위가 아니라 그 아래쪽 바닥에 세운다. 단상 위에 서면 플레이어가 가려진다
        protected override Vector2 DefaultDebugSpawnOffset => new Vector2(0f, -4f);

        protected override void DeclareRequirements(CutsceneRequirements r)
        {
            r.Dialogue(_lines, nameof(_lines));
            r.Marker(SkyFocus, SkyArrive);
            r.EnemySpawner();
        }

        protected override IEnumerator Play(CutsceneContext c)
        {
            // 복도 아래에 세워 둔 적이 걸어 올라오고(AI는 컷씬이 끝난 뒤에 시작), 카메라가 그쪽을 비춘다
            var enemies = c.EnemyGroup(SkyGroup);
            var climb = c.MoveEnemies(enemies, SkyArrive, _walkSpeed);
            c.FaceTo(c.Player, SkyFocus);
            yield return c.CameraTo(SkyFocus, _focusBlend);
            yield return c.Wait(_focusHold);

            // 대사는 카메라가 적을 비추는 동안 이어진다. 적이 아직 걷는 중이면 도착을 기다린다. 끝나면 플레이어에게 돌아온다
            yield return c.Say(_lines);
            yield return climb;
            yield return c.CameraToPlayer(_returnBlend);

            // 이 컷씬이 끝나면 적이 달려든다
            if (_alertSfx != null)
            {
                c.PlaySfx(_alertSfx);
            }

            c.CameraShake(0.08f, 0.4f);
        }
    }
}
