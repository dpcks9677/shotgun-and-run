using Consumable;

namespace ShotgunAndRun.Cutscene
{
    // 플레이어 상태 명령: 체력 · 탄창 · 무기 사용 여부 · 소모품 지급. (튜토리얼처럼 연출이 상태를 직접 맞출 때)
    // 컷씬 밖(튜토리얼 진행)에서는 CutscenePlayerState를 직접 쓰세요.
    public sealed partial class CutsceneContext
    {
        /// <summary>
        /// 플레이어 체력을 1~최대 체력 사이 값으로 맞춥니다. (예: 시작 체력 3칸, 기절 후 1칸)
        /// </summary>
        public void SetPlayerHealth(int health)
        {
            CutscenePlayerState.SetHealth(health);
        }

        /// <summary>
        /// 장전된 탄 수를 맞춥니다. 예비 탄약은 무한입니다.
        /// </summary>
        public void SetPlayerMagazine(int count)
        {
            CutscenePlayerState.SetMagazine(count);
        }

        /// <summary>
        /// 플레이어가 무기를 든 상태(true)인지 맨손(false)인지 바꿉니다. 맨손이면 사격 · 소모품 사용이 막힙니다.
        /// </summary>
        public void SetPlayerArmed(bool armed)
        {
            CutscenePlayerState.SetArmed(armed);
        }

        /// <summary>
        /// 플레이어가 지금 바라보는 방향으로 한 발 쏩니다. 무기를 든 상태여야 하고, 탄창 · 연사 간격은 실제 무기를 따릅니다. 실제로 쐈으면 true입니다.
        /// </summary>
        public bool PlayerFire()
        {
            return CutscenePlayerState.Fire();
        }

        /// <summary>
        /// 플레이어 무기에 장전된 탄 수입니다.
        /// </summary>
        public int PlayerMagazine => CutscenePlayerState.Magazine;

        /// <summary>
        /// 플레이어가 재장전을 시작합니다. 실제 무기처럼 한 발씩 장전 모션이 나오고, 도중에 PlayerFire를 부르면 장전을 끊고 쏩니다.
        /// </summary>
        public void PlayerReload()
        {
            CutscenePlayerState.Reload();
        }

        /// <summary>
        /// 플레이어의 무기를 숨기거나 다시 보입니다. 컷씬이 끝나기 전에 보이지 않으면 컷씬 정리 때 자동으로 되돌립니다.
        /// </summary>
        public void SetPlayerWeaponHidden(bool hidden)
        {
            CutscenePlayerState.SetWeaponHidden(hidden);
            _weaponHiddenByCutscene = hidden;
        }

        /// <summary>
        /// 플레이어 퀵슬롯의 소모품을 모두 비웁니다.
        /// </summary>
        public void ClearConsumables()
        {
            CutscenePlayerState.ClearConsumables();
        }

        /// <summary>
        /// 소모품을 플레이어 퀵슬롯에 지급합니다. 넣지 못하면 false입니다.
        /// </summary>
        public bool GiveConsumable(ConsumableData data, int amount = 1)
        {
            return CutscenePlayerState.GiveConsumable(data, amount);
        }
    }
}
