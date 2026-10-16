using System.Collections;
using ShotgunAndRun.UI.Dialogue;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// (프로토타입) CS_TUTORIAL_SKY_CLEAR: 하늘색 전투가 끝난 직후. 카메라가 플레이어에게 포커스하고 샷건 소감을 말한 뒤 길을 나선다.
    /// </summary>
    [CreateAssetMenu(fileName = "CS_TUTORIAL_SKY_CLEAR", menuName = "ShotgunAndRun/Cutscene/Tutorial/Sky Clear")]
    public sealed class TutorialSkyClearCutscene : CutsceneAsset
    {
        [Header("대사")]
        [Tooltip("3줄: '이거 꽤 쓸만하잖아?' · '쓸 때마다 밀려나는 게 아프긴하지만...' · '일단 길을 따라가보자.' (화자: 플레이어)")]
        [SerializeField] private DialogueDataSO _lines;

        [Header("연출 수치")]
        [SerializeField, Min(0f)] private float _focusBlend = 0.8f;
        [SerializeField, Min(0.1f)] private float _zoomSize = 5.5f;
        [SerializeField, Min(0f)] private float _zoomSeconds = 1f;
        [SerializeField, Min(0f)] private float _focusHold = 0.4f;
        [SerializeField, Min(0f)] private float _unzoomSeconds = 0.6f;

        protected override string DefaultDebugSpawnMarker => "GunFocus";

        // 샷건 단상(기둥) 위가 아니라 그 아래쪽 바닥에 세운다. 단상 위에 서면 플레이어가 가려진다
        protected override Vector2 DefaultDebugSpawnOffset => new Vector2(0f, -4f);

        protected override void DeclareRequirements(CutsceneRequirements r)
        {
            r.Dialogue(_lines, nameof(_lines));
        }

        protected override IEnumerator Play(CutsceneContext c)
        {
            c.CameraZoom(_zoomSize, _zoomSeconds);
            yield return c.CameraToPlayer(_focusBlend);
            yield return c.Wait(_focusHold);
            yield return c.Say(_lines);
            yield return c.CameraZoomReset(_unzoomSeconds);
        }
    }
}
