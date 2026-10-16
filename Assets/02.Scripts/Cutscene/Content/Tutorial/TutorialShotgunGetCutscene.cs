using System.Collections;
using ShotgunAndRun.UI.Dialogue;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// (프로토타입) CS_TUTORIAL_SHOTGUN_GET: 샷건 획득. 빛 번쩍임 + 카메라 줌 + 심장박동 효과음 뒤에 대사를 말하고, 무기를 든 상태가 된다.
    /// 대본: Docs/quests/CS_TUTORIAL.md 3-3. 끝나면 탄약 3발 · 무기 UI가 열리고 바로 전투 튜토리얼(하늘색)이 이어진다.
    /// </summary>
    [CreateAssetMenu(fileName = "CS_TUTORIAL_SHOTGUN_GET", menuName = "ShotgunAndRun/Cutscene/Tutorial/Shotgun Get")]
    public sealed class TutorialShotgunGetCutscene : CutsceneAsset
    {
        private const string HidePickupSignal = "HideShotgunPickup";

        [Header("대사")]
        [Tooltip("1줄: '처음 보는 무기인데... 피가 끓는 듯한 이 느낌은 뭐지?' (화자: 플레이어)")]
        [SerializeField] private DialogueDataSO _line;

        [Header("효과음 (임시, 비워 두면 소리 없이 진행)")]
        [SerializeField] private AudioClip _heartbeat;
        [Tooltip("샷건이 손에 들어오는 순간의 장전음 (예: 철컥)")]
        [SerializeField] private AudioClip _equipSfx;

        [Header("연출 수치")]
        [SerializeField, Min(1)] private int _startMagazine = 3;
        [SerializeField, Min(0.1f)] private float _zoomSize = 5.5f;
        [SerializeField, Min(0f)] private float _zoomSeconds = 1.2f;
        [SerializeField, Min(0f)] private float _flashSeconds = 0.9f;
        [SerializeField, Min(0f)] private float _beatInterval = 0.7f;
        [SerializeField, Min(0f)] private float _holdSeconds = 1.4f;
        [SerializeField, Min(0f)] private float _unzoomSeconds = 0.6f;

        protected override string DefaultDebugSpawnMarker => "GunFocus";

        // 샷건 단상(기둥) 위가 아니라 그 아래쪽 바닥에 세운다. 단상 위에 서면 플레이어가 가려진다
        protected override Vector2 DefaultDebugSpawnOffset => new Vector2(0f, -4f);

        protected override void DeclareRequirements(CutsceneRequirements r)
        {
            r.Dialogue(_line, nameof(_line));
            r.Signal(HidePickupSignal);
        }

        protected override IEnumerator Play(CutsceneContext c)
        {
            // 샷건이 빛나며 심장이 뛴다
            if (_heartbeat != null)
            {
                c.PlaySfxBurst(_heartbeat, 3, _beatInterval);
            }

            c.CameraZoom(_zoomSize, _zoomSeconds);
            yield return c.Flash(new Color(1f, 0.95f, 0.8f, 1f), _flashSeconds);

            // 샷건이 손에 들어온다
            c.Signal(HidePickupSignal);
            if (_equipSfx != null)
            {
                c.PlaySfx(_equipSfx);
            }

            c.SetPlayerArmed(true);
            c.SetPlayerMagazine(_startMagazine);
            yield return c.Wait(_holdSeconds);

            yield return c.Say(_line);

            yield return c.CameraZoomReset(_unzoomSeconds);
            c.ShowHud("Ammo");
        }
    }
}
