using Consumable;
using ShotgunAndRun.Player;
using UnityEngine;
using PlayerComponent = ShotgunAndRun.Player.Player;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 컷씬과 튜토리얼 진행이 함께 쓰는 플레이어 상태 조작 모음입니다.
    /// 체력 · 탄창 · 무기 사용 여부 · 사망 방지 · 소모품 지급처럼 연출이 플레이어 상태를 직접 맞춰야 할 때 사용합니다.
    /// </summary>
    public static class CutscenePlayerState
    {
        /// <summary>
        /// 체력을 1~최대 체력 사이 값으로 맞춥니다.
        /// </summary>
        public static void SetHealth(int health)
        {
            if (!TryGetPlayer(out var player)) return;
            player.Health.SetHealth(health);
        }

        /// <summary>
        /// 장전된 탄 수를 맞춥니다. 예비 탄약은 무한이라 별도 값이 없습니다.
        /// </summary>
        public static void SetMagazine(int count)
        {
            if (!TryGetPlayer(out var player)) return;
            var weapon = player.GetComponent<PlayerWeapon>();
            if (weapon == null)
            {
                Debug.LogWarning("[CutscenePlayerState] 플레이어에 PlayerWeapon이 없어 탄창을 맞추지 못했습니다.");
                return;
            }

            weapon.SetMagazine(count);
        }

        /// <summary>
        /// 무기를 든 상태(true)와 맨손 상태(false)를 바꿉니다. 맨손이면 사격 · 소모품 사용이 막히고 맨손 애니메이션을 씁니다.
        /// </summary>
        public static void SetArmed(bool armed)
        {
            if (!TryGetPlayer(out var player)) return;
            player.SetShelterMode(!armed);
        }

        /// <summary>
        /// 지금 조준 방향으로 한 발 쏩니다. 맨손이거나 탄창이 비었거나 연사 간격이 지나지 않았으면 쏘지 않습니다. 실제로 쐈으면 true입니다.
        /// </summary>
        public static bool Fire()
        {
            if (!TryGetPlayer(out var player)) return false;
            var weapon = player.GetComponent<PlayerWeapon>();
            if (weapon == null)
            {
                Debug.LogWarning("[CutscenePlayerState] 플레이어에 PlayerWeapon이 없어 사격하지 못했습니다.");
                return false;
            }

            // 쏘면 탄이 한 발 줄어든다
            int magazine = weapon.CurrentMagazine;
            weapon.TryFire(player.Aim.WeaponAimDirection, player.Aim.AimDistance);
            return weapon.CurrentMagazine < magazine;
        }

        /// <summary>
        /// 장전된 탄 수입니다. 플레이어나 무기가 없으면 0입니다.
        /// </summary>
        public static int Magazine => TryGetWeapon(out var weapon) ? weapon.CurrentMagazine : 0;

        /// <summary>
        /// 재장전을 시작합니다. 실제 무기처럼 한 발씩 장전 모션과 함께 탄창이 찰 때까지 장전하고, 도중에 Fire를 부르면 장전을 끊고 쏩니다.
        /// </summary>
        public static void Reload()
        {
            if (TryGetWeapon(out var weapon))
            {
                weapon.TryReload();
            }
        }

        // SetWeaponHidden이 숨기기 전의 사용 가능 여부. 보여 줄 때 이전 상태가 무기를 든 상태였을 때만 되돌린다
        private static bool _weaponWasUsable;

        /// <summary>
        /// 무기(샷건)를 숨기거나 다시 보입니다. 쓰러지는 연출처럼 무기를 든 채로는 어색한 장면에 씁니다.
        /// 숨기면 조준선과 사격 · 장전도 함께 막히고, 보일 때는 숨기기 전에 무기를 쓸 수 있었던 경우에만 되돌립니다.
        /// </summary>
        public static void SetWeaponHidden(bool hidden)
        {
            if (!TryGetPlayer(out var player)) return;

            var weapon = player.GetComponent<PlayerWeapon>();
            if (weapon == null)
            {
                Debug.LogWarning("[CutscenePlayerState] 플레이어에 PlayerWeapon이 없어 무기를 숨기지 못했습니다.");
                return;
            }

            if (hidden)
            {
                _weaponWasUsable = weapon.IsWeaponUsable;
                weapon.SetWeaponUsable(false);
            }
            else if (_weaponWasUsable)
            {
                weapon.SetWeaponUsable(true);
                _weaponWasUsable = false;
            }
        }

        /// <summary>
        /// 켜면 치명 피해를 받아도 체력이 1로 남고 죽지 않습니다. 이때 PlayerHealth.LethalDamageBlocked가 발생합니다.
        /// </summary>
        public static void SetDeathBlocked(bool blocked)
        {
            if (!TryGetPlayer(out var player)) return;
            player.Health.SetDeathBlocked(blocked);
        }

        /// <summary>
        /// 소모품을 퀵슬롯에 지급합니다. 슬롯이 가득 차 못 넣으면 false입니다.
        /// </summary>
        public static bool GiveConsumable(ConsumableData data, int amount = 1)
        {
            if (!TryGetPlayer(out var player)) return false;
            if (data == null)
            {
                Debug.LogWarning("[CutscenePlayerState] 지급할 소모품 데이터가 비어 있습니다.");
                return false;
            }

            return player.ConsumableInventory.TryAddConsumable(data, amount);
        }

        /// <summary>
        /// 퀵슬롯의 소모품을 모두 비웁니다. (프리팹에 기본으로 들어 있는 소모품을 지우고 시작할 때)
        /// </summary>
        public static void ClearConsumables()
        {
            if (!TryGetPlayer(out var player)) return;

            // 슬롯 UI에도 알려야 하므로 비어 있는 상태로 복원하는 공개 API를 쓴다
            var empty = new (string typeName, int amount)[player.ConsumableInventory.SlotCount];
            for (int i = 0; i < empty.Length; i++) empty[i] = (string.Empty, 0);
            player.ConsumableInventory.RestoreFromSnapshots(empty);

            // 소모품 목록(카탈로그)이 없어 복원이 건너뛰어졌다면 슬롯만이라도 비운다
            foreach (var slot in player.ConsumableInventory.Slots)
            {
                if (!slot.IsEmpty)
                {
                    slot.Clear();
                }
            }
        }

        private static bool TryGetWeapon(out PlayerWeapon weapon)
        {
            weapon = null;
            if (!TryGetPlayer(out var player)) return false;

            weapon = player.GetComponent<PlayerWeapon>();
            if (weapon == null)
            {
                Debug.LogWarning("[CutscenePlayerState] 플레이어에 PlayerWeapon이 없습니다.");
                return false;
            }

            return true;
        }

        private static bool TryGetPlayer(out PlayerComponent player)
        {
            player = PlayerComponent.PlayerInstance;
            if (player == null)
            {
                Debug.LogWarning("[CutscenePlayerState] 씬에 플레이어가 없습니다.");
                return false;
            }

            return true;
        }
    }
}
