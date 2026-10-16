using NUnit.Framework;
using ShotgunAndRun.Player;
using ShotgunAndRun.Weapon;
using UnityEngine;

namespace ShotgunAndRun.Player.Tests
{
    [TestFixture]
    public class PlayerLevelRulesTests
    {
        [TestCase(1, 5)]
        [TestCase(2, 5)]
        [TestCase(3, 6)]
        [TestCase(4, 6)]
        [TestCase(5, 6)]
        [TestCase(6, 7)]
        [TestCase(7, 7)]
        [TestCase(8, 7)]
        [TestCase(9, 8)]
        [TestCase(10, 8)]
        public void GetBagCapacity_ReturnsExpectedSlots(int level, int expectedSlots)
        {
            Assert.AreEqual(expectedSlots, PlayerLevelRules.GetBagCapacity(level));
        }

        [TestCase(1, 1.0f)]
        [TestCase(2, 1.05f)]
        [TestCase(3, 1.05f)]
        [TestCase(4, 1.05f)]
        [TestCase(5, 1.10f)]
        [TestCase(6, 1.10f)]
        [TestCase(7, 1.10f)]
        [TestCase(8, 1.15f)]
        [TestCase(9, 1.15f)]
        [TestCase(10, 1.15f)]
        public void GetMoveSpeedMultiplier_ReturnsExpectedMultiplier(int level, float expectedMultiplier)
        {
            Assert.AreEqual(expectedMultiplier, PlayerLevelRules.GetMoveSpeedMultiplier(level), 0.001f);
        }

        [TestCase(1, 0)]
        [TestCase(2, 0)]
        [TestCase(3, 0)]
        [TestCase(4, 1)]
        [TestCase(5, 1)]
        [TestCase(6, 1)]
        [TestCase(7, 2)]
        [TestCase(8, 2)]
        [TestCase(9, 2)]
        [TestCase(10, 3)]
        public void GetMaxHealthBonus_ReturnsExpectedBonus(int level, int expectedBonus)
        {
            Assert.AreEqual(expectedBonus, PlayerLevelRules.GetMaxHealthBonus(level));
        }

        [TestCase(1, 0.0f)]
        [TestCase(2, 0.0f)]
        [TestCase(3, 0.0f)]
        [TestCase(4, 0.5f)]
        [TestCase(5, 0.5f)]
        [TestCase(6, 0.5f)]
        [TestCase(7, 1.0f)]
        [TestCase(8, 1.0f)]
        [TestCase(9, 1.0f)]
        [TestCase(10, 1.0f)]
        public void GetDashChargeReduction_ReturnsExpectedReduction(int level, float expectedReduction)
        {
            Assert.AreEqual(expectedReduction, PlayerLevelRules.GetDashChargeReduction(level), 0.001f);
        }

        [Test]
        public void ApplyPlayerLevel_SetsFullHealth_WhenMaxHealthIncreases()
        {
            PlayerDataSO data = ScriptableObject.CreateInstance<PlayerDataSO>();
            PlayerRuntimeStats stats = new(data);
            GameObject obj = new("player-test");

            try
            {
                GameObject child = new("effect-service");
                child.transform.SetParent(obj.transform);
                child.AddComponent<Consumable.ConsumableEffectService>();

                PlayerInvulnerability inv = obj.AddComponent<PlayerInvulnerability>();
                PlayerHealth health = obj.AddComponent<PlayerHealth>();
                inv.Initialize();
                health.Initialize(stats, inv);

                // Lv 5 적용 (MaxHealth = 5 + 1 = 6)
                stats.SetMaxHealthBonus(PlayerLevelRules.LevelStatSourceId, PlayerLevelRules.GetMaxHealthBonus(5));
                health.SetHealth(health.MaxHealth);

                Assert.AreEqual(6, health.MaxHealth);
                Assert.AreEqual(6, health.CurrentHealth);
            }
            finally
            {
                Object.DestroyImmediate(obj);
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void SetUpgradeState_RefillsMagazine_ToNewCapacity()
        {
            ShotgunDataSO data = ScriptableObject.CreateInstance<ShotgunDataSO>();
            data.MagazineSize = 2;
            GameObject obj = new("weapon-test");

            try
            {
                GameObject sr1 = new("sr1");
                sr1.transform.SetParent(obj.transform);
                sr1.AddComponent<SpriteRenderer>();

                GameObject sr2 = new("sr2");
                sr2.transform.SetParent(obj.transform);
                sr2.AddComponent<SpriteRenderer>();

                GameObject feedback = new("weapon-feedback");
                feedback.transform.SetParent(obj.transform);
                feedback.SetActive(false);
                feedback.AddComponent<SpriteRenderer>();
                feedback.AddComponent<PlayerWeaponFeedback>();

                GameObject child = new("selection-controller");
                child.transform.SetParent(obj.transform);
                child.AddComponent<Consumable.ConsumableSelectionController>();

                PlayerDash dash = obj.AddComponent<PlayerDash>();
                PlayerWeapon weapon = obj.AddComponent<PlayerWeapon>();

                WeaponUpgradeState initialState = new WeaponUpgradeState { BeltLevel = 1 };
                weapon.Initialize(data, initialState, dash);

                Assert.AreEqual(2, weapon.CurrentMagazine);

                // Belt Lv 3 적용 (MagazineSize = 2 + 1 = 3)
                WeaponUpgradeState upgradedState = new WeaponUpgradeState { BeltLevel = 3 };
                weapon.SetUpgradeState(upgradedState);

                Assert.AreEqual(3, weapon.Stats.MagazineSize);
                Assert.AreEqual(3, weapon.CurrentMagazine);
            }
            finally
            {
                Object.DestroyImmediate(obj);
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void DungeonRefill_SkipsFirstCriticalShot_ButKeepsFourthShotInterval()
        {
            ShotgunDataSO data = ScriptableObject.CreateInstance<ShotgunDataSO>();
            data.MagazineSize = 6;
            GameObject obj = new("weapon-critical-test");

            try
            {
                GameObject feedback = new("weapon-feedback");
                feedback.transform.SetParent(obj.transform);
                feedback.SetActive(false);
                feedback.AddComponent<SpriteRenderer>();
                feedback.AddComponent<PlayerWeaponFeedback>();

                GameObject selection = new("selection-controller");
                selection.transform.SetParent(obj.transform);
                selection.AddComponent<Consumable.ConsumableSelectionController>();

                PlayerDash dash = obj.AddComponent<PlayerDash>();
                PlayerWeapon weapon = obj.AddComponent<PlayerWeapon>();
                WeaponUpgradeState state = new WeaponUpgradeState
                {
                    AmmoLevel = 3,
                    BeltLevel = 4
                };

                weapon.Initialize(data, state, dash);
                weapon.RefillMagazine(skipFirstCriticalShot: true);

                Assert.AreEqual(8, weapon.CurrentMagazine);
                Assert.IsFalse(weapon.IsCriticalShell(8));
                Assert.IsTrue(weapon.IsCriticalShell(4));
            }
            finally
            {
                Object.DestroyImmediate(obj);
                Object.DestroyImmediate(data);
            }
        }
    }
}
