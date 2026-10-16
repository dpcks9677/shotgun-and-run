using System.Collections;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 순간이동 도착 연출 (TeleportOutCutscene의 반대). 플레이어를 숨긴 채 시작해, 바닥의 링에서 흰 빛줄기가 솟으면
    /// 하얀 모습으로 나타난 뒤 원래 색으로 돌아온다. CS_TELEPORT_ARRIVE 하나를 쉘터 · 던전 씬에 함께 등록한다.
    /// 에셋 설정: AutoPlay ON · Priority 가장 낮게(스토리 자동 컷씬이 있으면 그쪽이 재생) · LockPlayer ON · HideHud ON · PauseGameplay ON, 플래그 없음.
    /// </summary>
    [CreateAssetMenu(fileName = "CS_TELEPORT_ARRIVE", menuName = "ShotgunAndRun/Cutscene/Common/Teleport In")]
    public sealed class TeleportInCutscene : CutsceneAsset
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
        [Tooltip("씬이 뜬 뒤 빛줄기가 솟기까지 기다리는 시간")]
        [SerializeField, Min(0f)] private float _startDelay = 0.3f;
        [Tooltip("빛줄기가 솟은 뒤 플레이어가 나타나기까지의 시간")]
        [SerializeField, Min(0f)] private float _appearDelay = 0.15f;
        [SerializeField, Min(0f)] private float _whiteSeconds = 0.6f;
        [SerializeField, Min(0f)] private float _afterAppearSeconds = 0.2f;

        // 씬이 뜬 첫 화면부터 플레이어가 보이지 않게 Director가 자동 재생 전에 숨긴다
        public override bool HidesPlayerBeforeStart => true;

        protected override void DeclareRequirements(CutsceneRequirements r)
        {
            r.Asset(_silhouette, nameof(_silhouette));
            r.Asset(_effect, nameof(_effect));
        }

        /// <summary>
        /// 다른 컷씬이 도중에 도착 연출을 이어서 재생할 때 씁니다. (예: 쉘터 붕괴 뒤 귀환)
        /// </summary>
        public IEnumerator PlayArrival(CutsceneContext c) => Play(c);

        protected override IEnumerator Play(CutsceneContext c)
        {
            // 숨긴 채 시작해 빛줄기와 함께 하얗게 나타나고 원래 색으로 돌아온다
            // 무기와 조준선은 숨겨 두었다가 컷씬 정리 때 다시 보인다
            c.SetPlayerWeaponHidden(true);
            c.SetVisible(c.Player, false);

            // 효과음은 충전부터 시작하는 클립이라 시작 지연보다 먼저 재생한다
            if (_sfx != null)
            {
                c.PlaySfx(_sfx);
            }

            // 중단 · 예외로 끝나도 플레이어가 숨은 채 남지 않게 한다
            bool shown = false;
            try
            {
                yield return c.Wait(_startDelay);

                c.SpawnEffect(_effect, c.Player.Position + (Vector3)_effectOffset);
                yield return c.Wait(_appearDelay);

                c.SetVisible(c.Player, true);
                shown = true;
                yield return c.WhiteIn(c.Player, _silhouette, _whiteSeconds);
                yield return c.Wait(_afterAppearSeconds);
            }
            finally
            {
                if (!shown)
                {
                    c.SetVisible(c.Player, true);
                }
            }
        }
    }
}
