using System;
using Consumable;
using ShotgunAndRun.Combat;
using UnityEngine;

namespace ShotgunAndRun.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour, IDamageable
    {
        private PlayerRuntimeStats _stats;
        private PlayerInvulnerability _invulnerability;
        private ConsumableEffectService _effectService;
        private int _lastMaxHealth;
        private bool _initialized;
        private bool _hasArmor;
        private bool _deathBlocked;
        public bool HasArmor => _hasArmor;

        public event Action<DamageInfo> Damaged;
        public event Action<int, int> HealthChanged;
        public event Action Died;
        public event Action<string> DiedWithCause;
        public event Action ArmorActivated;
        public event Action ArmorBroken;

        /// <summary>
        /// 사망 방지 중에 치명 피해를 받아 체력이 1로 남았을 때 (튜토리얼 · 컷씬 연출용)
        /// </summary>
        public event Action LethalDamageBlocked;

        public int CurrentHealth
        {
            get;
            private set;
        }

        public int MaxHealth => _initialized ? _stats.MaxHealth : 0;

        public bool IsDead
        {
            get;
            private set;
        }

        public DamageInfo LastFatalDamage
        {
            get;
            private set;
        }
        public string LastDeathCause
        {
            get;
            private set;
        } = string.Empty;

        public PlayerRuntimeStats Stats => _stats;

        public void Initialize(PlayerRuntimeStats stats, PlayerInvulnerability invulnerability)
        {
            if (_initialized)
            {
                return;
            }

            _stats = stats ?? throw new ArgumentNullException(nameof(stats));
            _invulnerability = invulnerability != null
                ? invulnerability
                : throw new ArgumentNullException(nameof(invulnerability));
            _lastMaxHealth = _stats.MaxHealth;
            CurrentHealth = _lastMaxHealth;
            _effectService = GetComponentInChildren<ConsumableEffectService>();

            _effectService.StartUseConsumable += HandleUsingConsumable;
            _stats.StatsChanged += HandleStatsChanged;
            _initialized = true;
        }

        public bool TryTakeDamage(DamageInfo damageInfo)
        {
            if (!_initialized || IsDead || damageInfo.Damage <= 0 || _invulnerability.IsInvulnerable)
            {
                return false;
            }

            Debug.Log("getDamage");

            if (_hasArmor)
            {
                Debug.Log("아머로 인해 피격 무효화!");
                _hasArmor = false;
                ArmorBroken?.Invoke();
                return true;
            }

            int resultingHealth = CurrentHealth - damageInfo.Damage;
            bool lethalBlocked = _deathBlocked && resultingHealth <= 0;
            CurrentHealth = lethalBlocked ? 1 : Mathf.Max(0, resultingHealth);
            IsDead = CurrentHealth <= 0;

            if (!IsDead)
            {
                _invulnerability.ActivateTimed(
                    InvulnerabilityReason.Hit,
                    _stats.HitInvincibilityDuration);
            }

            Damaged?.Invoke(damageInfo);
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);

            if (lethalBlocked)
            {
                LethalDamageBlocked?.Invoke();
            }

            if (IsDead)
            {
                LastFatalDamage = damageInfo;
                LastDeathCause = ResolveDeathCause(damageInfo);
                Debug.Log($"플레이어 사망 - 원인: {LastDeathCause}");
                Died?.Invoke();
                DiedWithCause?.Invoke(LastDeathCause);
            }

            return true;
        }

        private static string ResolveDeathCause(DamageInfo damageInfo)
        {
            if (damageInfo.Source != null)
            {
                IAttacker attacker = damageInfo.Source.GetComponentInParent<IAttacker>();
                if (attacker != null && !string.IsNullOrWhiteSpace(attacker.DisplayName))
                {
                    return attacker.DisplayName;
                }

                return damageInfo.Source.name;
            }

            return "원인 불명의 공격";
        }

        /// <summary>
        /// 플레이어를 즉시 사망 상태로 만듭니다 (자결, 치트, 게임 포기 등).
        /// </summary>
        public void Kill(string deathCause = null)
        {
            if (!_initialized || IsDead)
            {
                return;
            }

            CurrentHealth = 0;
            IsDead = true;
            LastFatalDamage = default;
            LastDeathCause = string.IsNullOrWhiteSpace(deathCause) ? "사망" : deathCause;
            Debug.Log($"플레이어 즉사 - 원인: {LastDeathCause}");

            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
            Died?.Invoke();
            DiedWithCause?.Invoke(LastDeathCause);
        }

        /// <summary>
        /// 켜면 치명 피해를 받아도 체력이 1로 남고 죽지 않습니다. (튜토리얼 · 컷씬 연출용)
        /// </summary>
        public void SetDeathBlocked(bool blocked)
        {
            _deathBlocked = blocked;
        }

        /// <summary>
        /// 체력을 1~최대 체력 사이 값으로 직접 맞춥니다. 사망한 뒤에는 쓸 수 없습니다. (튜토리얼 · 컷씬 연출용)
        /// </summary>
        public void SetHealth(int health)
        {
            if (!_initialized || IsDead)
            {
                return;
            }

            int clamped = Mathf.Clamp(health, 1, MaxHealth);
            if (clamped == CurrentHealth)
            {
                return;
            }

            CurrentHealth = clamped;
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        public void Heal(int amount)
        {
            if (!_initialized || IsDead || amount <= 0 || CurrentHealth >= MaxHealth)
            {
                return;
            }

            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        private void OnDestroy()
        {
            if (_stats != null)
            {
                _stats.StatsChanged -= HandleStatsChanged;
                _effectService.StartUseConsumable -= HandleUsingConsumable;
            }
        }

        private void HandleStatsChanged(PlayerStatMask changedStats)
        {
            if ((changedStats & PlayerStatMask.MaxHealth) == 0)
            {
                return;
            }

            int newMaxHealth = _stats.MaxHealth;
            int previousHealth = CurrentHealth;
            CurrentHealth = Mathf.Min(CurrentHealth, newMaxHealth);
            bool maxChanged = _lastMaxHealth != newMaxHealth;
            _lastMaxHealth = newMaxHealth;

            if (maxChanged || previousHealth != CurrentHealth)
            {
                HealthChanged?.Invoke(CurrentHealth, newMaxHealth);
            }
        }

        private void HandleUsingConsumable(ConsumableType type, float duration)
        {
            if (type == ConsumableType.Armor)
            {
                _hasArmor = true;
                ArmorActivated?.Invoke();
            }
        }
    }
}
