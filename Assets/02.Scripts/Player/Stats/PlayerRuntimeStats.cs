using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShotgunAndRun.Player
{
    public sealed class PlayerRuntimeStats
    {
        private readonly int _baseMaxHealth;
        private readonly float _baseMoveSpeed;
        private readonly float _baseDashSpeed;
        private readonly float _baseDashCooldown;

        private readonly Dictionary<string, int> _maxHealthBonuses = new();
        private readonly Dictionary<string, float> _maxHealthMultipliers = new();
        private readonly Dictionary<string, float> _moveSpeedMultipliers = new();
        private readonly Dictionary<string, float> _dashSpeedMultipliers = new();
        private readonly Dictionary<string, float> _dashCooldownMultipliers = new();

        public event Action<PlayerStatMask> StatsChanged;

        public int MaxHealth => Mathf.Max(1, Mathf.FloorToInt(
            (_baseMaxHealth + Sum(_maxHealthBonuses.Values)) *
            Product(_maxHealthMultipliers.Values)));

        public float MoveSpeed => Mathf.Max(0f, _baseMoveSpeed * Product(_moveSpeedMultipliers.Values));
        public float DashSpeed => Mathf.Max(0f, _baseDashSpeed * Product(_dashSpeedMultipliers.Values));
        public float DashCooldown => Mathf.Max(0f, _baseDashCooldown * Product(_dashCooldownMultipliers.Values));

        public float DashDuration
        {
            get;
        }

        public float DashInvincibilityDuration
        {
            get;
        }

        public float HitInvincibilityDuration
        {
            get;
        }

        public PlayerRuntimeStats(PlayerDataSO data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            _baseMaxHealth = data.MaxHealth;
            _baseMoveSpeed = data.MoveSpeed;
            _baseDashSpeed = data.DashSpeed;
            _baseDashCooldown = data.DashCooldown;
            DashDuration = data.DashDuration;
            DashInvincibilityDuration = data.DashInvincibilityDuration;
            HitInvincibilityDuration = data.HitInvincibilityDuration;
        }

        public void SetMaxHealthBonus(string sourceId, int value)
        {
            ValidateSourceId(sourceId);
            int before = MaxHealth;
            _maxHealthBonuses[sourceId] = value;
            NotifyIfChanged(before, MaxHealth, PlayerStatMask.MaxHealth);
        }

        public void SetMaxHealthMultiplier(string sourceId, float value)
        {
            SetMultiplier(
                sourceId,
                value,
                _maxHealthMultipliers,
                () => MaxHealth,
                PlayerStatMask.MaxHealth);
        }

        public bool RemoveMaxHealthBonus(string sourceId)
        {
            return RemoveModifier(sourceId, _maxHealthBonuses, () => MaxHealth, PlayerStatMask.MaxHealth);
        }

        public bool RemoveMaxHealthMultiplier(string sourceId)
        {
            return RemoveModifier(
                sourceId,
                _maxHealthMultipliers,
                () => MaxHealth,
                PlayerStatMask.MaxHealth);
        }

        public void SetMoveSpeedMultiplier(string sourceId, float value)
        {
            SetMultiplier(sourceId, value, _moveSpeedMultipliers, () => MoveSpeed, PlayerStatMask.MoveSpeed);
        }

        public bool RemoveMoveSpeedMultiplier(string sourceId)
        {
            return RemoveModifier(sourceId, _moveSpeedMultipliers, () => MoveSpeed, PlayerStatMask.MoveSpeed);
        }

        public bool FindMoveSpeedMultiplier(string sourceId)
        {
            return _moveSpeedMultipliers.ContainsKey(sourceId);
        }

        public void SetDashSpeedMultiplier(string sourceId, float value)
        {
            SetMultiplier(sourceId, value, _dashSpeedMultipliers, () => DashSpeed, PlayerStatMask.DashSpeed);
        }

        public bool RemoveDashSpeedMultiplier(string sourceId)
        {
            return RemoveModifier(sourceId, _dashSpeedMultipliers, () => DashSpeed, PlayerStatMask.DashSpeed);
        }

        public void SetDashCooldownMultiplier(string sourceId, float value)
        {
            SetMultiplier(sourceId, value, _dashCooldownMultipliers, () => DashCooldown, PlayerStatMask.DashCooldown);
        }

        public bool RemoveDashCooldownMultiplier(string sourceId)
        {
            return RemoveModifier(sourceId, _dashCooldownMultipliers, () => DashCooldown, PlayerStatMask.DashCooldown);
        }

        private void SetMultiplier(
            string sourceId,
            float value,
            IDictionary<string, float> modifiers,
            Func<float> getValue,
            PlayerStatMask stat)
        {
            ValidateSourceId(sourceId);
            if (value < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "A stat multiplier cannot be negative.");
            }

            float before = getValue();
            modifiers[sourceId] = value;
            NotifyIfChanged(before, getValue(), stat);
        }

        private bool RemoveModifier<T>(
            string sourceId,
            IDictionary<string, T> modifiers,
            Func<int> getValue,
            PlayerStatMask stat)
        {
            ValidateSourceId(sourceId);
            int before = getValue();
            if (!modifiers.Remove(sourceId))
            {
                return false;
            }

            NotifyIfChanged(before, getValue(), stat);
            return true;
        }

        private bool RemoveModifier<T>(
            string sourceId,
            IDictionary<string, T> modifiers,
            Func<float> getValue,
            PlayerStatMask stat)
        {
            ValidateSourceId(sourceId);
            float before = getValue();
            if (!modifiers.Remove(sourceId))
            {
                return false;
            }

            NotifyIfChanged(before, getValue(), stat);
            return true;
        }

        private void NotifyIfChanged(int before, int after, PlayerStatMask stat)
        {
            if (before != after)
            {
                StatsChanged?.Invoke(stat);
            }
        }

        private void NotifyIfChanged(float before, float after, PlayerStatMask stat)
        {
            if (!Mathf.Approximately(before, after))
            {
                StatsChanged?.Invoke(stat);
            }
        }

        private static int Sum(IEnumerable<int> values)
        {
            int result = 0;
            foreach (int value in values)
            {
                result += value;
            }

            return result;
        }

        private static float Product(IEnumerable<float> values)
        {
            float result = 1f;
            foreach (float value in values)
            {
                result *= value;
            }

            return result;
        }

        private static void ValidateSourceId(string sourceId)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                throw new ArgumentException("A modifier source ID is required.", nameof(sourceId));
            }
        }
    }
}
