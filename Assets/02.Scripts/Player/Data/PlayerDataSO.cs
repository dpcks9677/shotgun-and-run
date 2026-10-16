using UnityEngine;
using UnityEngine.Serialization;

namespace ShotgunAndRun.Player
{
    [CreateAssetMenu(fileName = "PlayerDataSO", menuName = "ShotgunAndRun/Player/Player Data")]
    public sealed class PlayerDataSO : ScriptableObject
    {
        [FormerlySerializedAs("maxHp")]
        [SerializeField, Min(1)]
        private int _maxHealth = 5;

        [FormerlySerializedAs("moveSpeed")]
        [SerializeField, Min(0f)]
        private float _moveSpeed = 4f;

        [SerializeField, Min(0f)] private float _dashDistance = 1.5f; // 대시 이동 거리 (unit)

        [FormerlySerializedAs("dashCoolDown")]
        [SerializeField, Min(0f)]
        private float _dashCooldown = 3f;

        [SerializeField, Min(0.01f)] private float _dashDuration = 0.25f; // 대시 지속시간
        [SerializeField, Min(0f)] private float _dashInvincibilityDuration = 0.25f; // 대시 무적시간 (대시 지속시간보다 짧으면 지속시간을 따름)

        [FormerlySerializedAs("invincibilityDuration")]
        [SerializeField, Min(0f)]
        private float _hitInvincibilityDuration = 1f; // 무적시간

        public int MaxHealth => _maxHealth;
        public float MoveSpeed => _moveSpeed;
        public float DashDistance => _dashDistance;
        public float DashSpeed => _dashDistance / _dashDuration;
        public float DashCooldown => _dashCooldown;
        public float DashDuration => _dashDuration;
        public float DashInvincibilityDuration => _dashInvincibilityDuration;
        public float HitInvincibilityDuration => _hitInvincibilityDuration;

        private void OnValidate()
        {
            _maxHealth = Mathf.Max(1, _maxHealth);
            _moveSpeed = Mathf.Max(0f, _moveSpeed);
            _dashDistance = Mathf.Max(0f, _dashDistance);
            _dashCooldown = Mathf.Max(0f, _dashCooldown);
            _dashDuration = Mathf.Max(0.01f, _dashDuration);
            _dashInvincibilityDuration = Mathf.Max(0f, _dashInvincibilityDuration);
            _hitInvincibilityDuration = Mathf.Max(0f, _hitInvincibilityDuration);
        }
    }
}
