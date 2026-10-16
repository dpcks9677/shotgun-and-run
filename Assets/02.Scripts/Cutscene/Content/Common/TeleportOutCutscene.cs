using System.Collections;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 순간이동 퇴장 연출. 플레이어가 1초에 걸쳐 하얗게 물들고, 바닥의 링에서 흰 빛줄기가 솟구친 뒤 사라진다.
    /// CS_TELEPORT_ENTER(쉘터 포털, 끝나면 호출한 쪽이 던전 런을 시작)와 CS_TELEPORT_EXIT(탈출, 끝나면 호출한 쪽이 결과 화면을 띄움)가 같은 연출을 쓴다.
    /// 에셋 설정: AutoPlay OFF · LockPlayer ON · HideHud ON · PauseGameplay ON, 플래그 없음.
    /// </summary>
    [CreateAssetMenu(fileName = "CS_TELEPORT_ENTER", menuName = "ShotgunAndRun/Cutscene/Common/Teleport Out")]
    public sealed class TeleportOutCutscene : CutsceneAsset
    {
        [Header("이펙트")]
        [Tooltip("스프라이트 알파를 SpriteRenderer 색으로 칠하는 머티리얼 (셰이더 ShotgunAndRun/Sprite Solid Color)")]
        [SerializeField] private Material _silhouette;

        [Tooltip("순간이동 VFX 프리팹 (바닥 링에서 솟는 흰 빛줄기). 사라지는 것은 프리팹이 맡습니다.")]
        [SerializeField] private GameObject _effect;

        [Tooltip("플레이어 위치(발밑)에서 이펙트를 띄울 오프셋")]
        [SerializeField] private Vector2 _effectOffset = Vector2.zero;

        [Header("효과음 (비워 두면 소리 없이 진행)")]
        [SerializeField] private AudioClip _sfx;

        [Header("연출 수치")]
        [SerializeField, Min(0f)] private float _whiteSeconds = 1f;
        [SerializeField, Min(0f)] private float _effectDelay = 0.05f;
        [SerializeField, Min(0f)] private float _afterEffectSeconds = 0.8f;

        protected override void DeclareRequirements(CutsceneRequirements r)
        {
            r.Asset(_silhouette, nameof(_silhouette));
            r.Asset(_effect, nameof(_effect));
        }

        protected override IEnumerator Play(CutsceneContext c)
        {
            // 하얗게 물든 뒤 이펙트와 함께 사라진다. 플레이어는 컷씬이 끝나도 숨겨진 채로 남는다
            // 무기와 조준선은 먼저 숨긴다 (컷씬 정리 때 다시 보임)
            c.SetPlayerWeaponHidden(true);

            // 효과음은 충전부터 시작하는 클립이라 물들기 시작할 때 재생한다
            if (_sfx != null)
            {
                c.PlaySfx(_sfx);
            }

            yield return c.WhiteOut(c.Player, _silhouette, _whiteSeconds);

            c.SpawnEffect(_effect, c.Player.Position + (Vector3)_effectOffset);
            yield return c.Wait(_effectDelay);
            c.SetVisible(c.Player, false);

            yield return c.Wait(_afterEffectSeconds);
        }
    }
}
