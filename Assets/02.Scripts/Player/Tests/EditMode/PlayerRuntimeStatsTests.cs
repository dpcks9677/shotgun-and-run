using Consumable;
using NUnit.Framework;
using ShotgunAndRun.Combat;
using UnityEditor;
using UnityEngine;

namespace ShotgunAndRun.Player.Tests
{
    public sealed class PlayerRuntimeStatsTests
    {
        private PlayerDataSO _data;

        [TearDown]
        public void TearDown()
        {
            if (_data != null)
            {
                Object.DestroyImmediate(_data);
            }
        }

        [Test]
        public void Modifiers_StackReplaceAndRemoveBySource()
        {
            PlayerRuntimeStats stats = CreateStats(maxHealth: 10, moveSpeed: 8f);
            int changeCount = 0;
            stats.StatsChanged += _ => changeCount++;

            stats.SetMaxHealthBonus("item", 5);
            stats.SetMaxHealthBonus("item", -2);
            stats.SetMoveSpeedMultiplier("slow", 0.5f);
            stats.SetMoveSpeedMultiplier("buff", 2f);

            Assert.That(stats.MaxHealth, Is.EqualTo(8));
            Assert.That(stats.MoveSpeed, Is.EqualTo(8f).Within(0.001f));
            Assert.That(changeCount, Is.EqualTo(4));
            Assert.That(stats.RemoveMaxHealthBonus("item"), Is.True);
            Assert.That(stats.RemoveMoveSpeedMultiplier("missing"), Is.False);
            Assert.That(stats.MaxHealth, Is.EqualTo(10));
        }

        [Test]
        public void MaxHealthMultiplier_AppliesAfterBonus_AndRejectsNegative()
        {
            PlayerRuntimeStats stats = CreateStats(maxHealth: 10);

            stats.SetMaxHealthBonus("item", 5);
            stats.SetMaxHealthMultiplier("overtime", 0.5f);
            Assert.That(stats.MaxHealth, Is.EqualTo(7));

            Assert.That(stats.RemoveMaxHealthMultiplier("overtime"), Is.True);
            Assert.That(stats.MaxHealth, Is.EqualTo(15));

            Assert.Throws<System.ArgumentOutOfRangeException>(() => stats.SetMaxHealthMultiplier("overtime", -0.5f));
        }

        [Test]
        public void Health_MaxHealthDecreaseClampsCurrent_IncreaseDoesNotHeal()
        {
            PlayerRuntimeStats stats = CreateStats(maxHealth: 5);
            GameObject gameObject = new("health-test");

            try
            {
                PlayerInvulnerability invulnerability = gameObject.AddComponent<PlayerInvulnerability>();
                AddConsumableEffectService(gameObject);
                PlayerHealth health = gameObject.AddComponent<PlayerHealth>();
                invulnerability.Initialize();
                health.Initialize(stats, invulnerability);

                stats.SetMaxHealthBonus("curse", -2);
                Assert.That(health.MaxHealth, Is.EqualTo(3));
                Assert.That(health.CurrentHealth, Is.EqualTo(3));

                stats.RemoveMaxHealthBonus("curse");
                Assert.That(health.MaxHealth, Is.EqualTo(5));
                Assert.That(health.CurrentHealth, Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Damage_RespectsOverlappingInvulnerability_AndDiesOnce()
        {
            PlayerRuntimeStats stats = CreateStats(maxHealth: 5, hitInvincibilityDuration: 0.5f);
            GameObject gameObject = new("damage-test");

            try
            {
                PlayerInvulnerability invulnerability = gameObject.AddComponent<PlayerInvulnerability>();
                AddConsumableEffectService(gameObject);
                PlayerHealth health = gameObject.AddComponent<PlayerHealth>();
                invulnerability.Initialize();
                health.Initialize(stats, invulnerability);
                int deathCount = 0;
                health.Died += () => deathCount++;

                Assert.That(health.TryTakeDamage(new DamageInfo(2, Vector2.zero, Vector2.right)), Is.True);
                Assert.That(health.CurrentHealth, Is.EqualTo(3));
                Assert.That(health.TryTakeDamage(new DamageInfo(1, Vector2.zero, Vector2.right)), Is.False);

                invulnerability.SetActive(InvulnerabilityReason.Dash, true);
                invulnerability.Tick(0.5f);
                Assert.That(invulnerability.IsInvulnerable, Is.True);
                invulnerability.SetActive(InvulnerabilityReason.Dash, false);

                Assert.That(health.TryTakeDamage(new DamageInfo(3, Vector2.zero, Vector2.right)), Is.True);
                Assert.That(health.IsDead, Is.True);
                Assert.That(health.TryTakeDamage(new DamageInfo(1, Vector2.zero, Vector2.right)), Is.False);
                Assert.That(deathCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Dash_LocksDirection_EndsByDuration_AndHonorsCooldown()
        {
            PlayerRuntimeStats stats = CreateStats(
                dashDistance: 2f,
                dashDuration: 0.2f);
            GameObject gameObject = new("dash-test");

            try
            {
                PlayerDash dash = gameObject.AddComponent<PlayerDash>();
                dash.Initialize(stats);

                Assert.That(dash.TryStart(new Vector2(2f, 0f)), Is.True);
                Assert.That(dash.DashDirection, Is.EqualTo(Vector2.right));
                Assert.That(dash.DashVelocity, Is.EqualTo(Vector2.right * 10f));
                Assert.That(dash.TryStart(Vector2.up), Is.False);

                while (dash.IsDashing)
                {
                    dash.ConsumeDashVelocity(0.02f);
                }

                Assert.That(dash.IsDashing, Is.False);
                dash.Tick(stats.DashCooldown * 0.5f);
                Assert.That(dash.TryStart(Vector2.up), Is.False);

                dash.Tick(stats.DashCooldown);
                Assert.That(dash.TryStart(Vector2.up), Is.True);
                Assert.That(dash.DashDirection, Is.EqualTo(Vector2.up));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [TestCase(0.02f)]
        [TestCase(1f / 60f)]
        [TestCase(0.03f)]
        public void Dash_TravelsExactDistance_RegardlessOfFixedDeltaTime(float fixedDeltaTime)
        {
            PlayerRuntimeStats stats = CreateStats(
                dashDistance: 1.5f,
                dashDuration: 0.25f);
            GameObject gameObject = new("dash-distance-test");

            try
            {
                PlayerDash dash = gameObject.AddComponent<PlayerDash>();
                dash.Initialize(stats);

                Assert.That(dash.TryStart(Vector2.right), Is.True);

                float distance = 0f;
                int steps = 0;
                while (dash.IsDashing && steps < 1000)
                {
                    distance += dash.ConsumeDashVelocity(fixedDeltaTime).x * fixedDeltaTime;
                    steps++;
                }

                Assert.That(dash.IsDashing, Is.False);
                Assert.That(distance, Is.EqualTo(1.5f).Within(0.0001f));
                Assert.That(steps, Is.EqualTo(
                    Mathf.CeilToInt(0.25f / fixedDeltaTime - 0.0001f)));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        // PlayerHealth는 자식의 ConsumableEffectService를 쓰고, 이 서비스는 부모의 이동·대쉬를 요구한다.
        private static void AddConsumableEffectService(GameObject root)
        {
            root.AddComponent<PlayerMovement>();
            root.AddComponent<PlayerDash>();
            GameObject child = new("consumable-effects");
            child.transform.SetParent(root.transform);
            child.AddComponent<ConsumableEffectService>();
        }

        private PlayerRuntimeStats CreateStats(
            int maxHealth = 5,
            float moveSpeed = 4f,
            float dashDistance = 2.4f,
            float dashCooldown = 3f,
            float dashDuration = 0.2f,
            float hitInvincibilityDuration = 1f)
        {
            _data = ScriptableObject.CreateInstance<PlayerDataSO>();
            SerializedObject serializedData = new(_data);
            serializedData.FindProperty("_maxHealth").intValue = maxHealth;
            serializedData.FindProperty("_moveSpeed").floatValue = moveSpeed;
            serializedData.FindProperty("_dashDistance").floatValue = dashDistance;
            serializedData.FindProperty("_dashCooldown").floatValue = dashCooldown;
            serializedData.FindProperty("_dashDuration").floatValue = dashDuration;
            serializedData.FindProperty("_hitInvincibilityDuration").floatValue = hitInvincibilityDuration;
            serializedData.ApplyModifiedPropertiesWithoutUndo();
            return new PlayerRuntimeStats(_data);
        }
    }
}
