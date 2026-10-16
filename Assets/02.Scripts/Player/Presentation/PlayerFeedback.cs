using ShotgunAndRun.Combat;
using UnityEngine;

namespace ShotgunAndRun.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerHealth))]
    [RequireComponent(typeof(PlayerDash))]
    [RequireComponent(typeof(PlayerMovement))]
    [RequireComponent(typeof(PlayerAudioFeedback))]
    [RequireComponent(typeof(PlayerVFXFeedback))]
    [RequireComponent(typeof(PlayerAnimationFeedback))]
    public sealed class PlayerFeedback : MonoBehaviour
    {
        private static readonly int HitTrigger = Animator.StringToHash("Hit");

        [SerializeField] private PlayerAnimationFeedback _animation;
        [SerializeField] private PlayerVFXFeedback _vfx;
        [SerializeField] private PlayerAudioFeedback _audio;

        private PlayerHealth _health;
        private PlayerDash _dash;
        private PlayerMovement _movement;

        private bool _weaponUsable;
        private bool _isShelter;

        private void Awake()
        {
            _health = GetComponent<PlayerHealth>();
            _dash = GetComponent<PlayerDash>();
            _movement = GetComponent<PlayerMovement>();

            _animation = GetComponent<PlayerAnimationFeedback>();
            _vfx = GetComponent<PlayerVFXFeedback>();
            _audio ??= GetComponent<PlayerAudioFeedback>();
        }

        private void OnEnable()
        {
            _health.Damaged += HandleDamaged;
            _health.Died += HandleDied;
            _health.ArmorBroken += HandleArmorBroken;
            _dash.DashStarted += HandleDashStart;
            _dash.DashEnded += HandleDashEnd;
            _movement.MoveChanged += HandleMovement;
        }

        private void OnDisable()
        {
            _health.Damaged -= HandleDamaged;
            _health.Died -= HandleDied;
            _health.ArmorBroken -= HandleArmorBroken;
            _dash.DashStarted -= HandleDashStart;
            _dash.DashEnded -= HandleDashEnd;
            _movement.MoveChanged -= HandleMovement;
        }

        private void HandleDamaged(DamageInfo damageInfo)
        {
            _animation.OnDamaged();
            _audio.PlayDamage();
            _vfx.PlayDamage(damageInfo);
            // StartCoroutine(PlayDamaged());
            // PlayAnimation();
            // PlayParticle(damageInfo);
            // PlaySound();
        }

        private void HandleDied()
        {
            _audio.PlayDead();
            _animation.PlayDead();
        }

        private void HandleDashStart(Vector2 direction)
        {
            _audio.StopWalk();
            _audio.PlayDash();
            _vfx.PlayDash(direction, _weaponUsable);
        }

        private void HandleDashEnd()
        {
        }

        private void HandleMovement()
        {
            // _audio.PlayWalk();
            _animation.UpdateWalkAnimation(_movement.MoveInput);
            
            if (!_movement.IsMoving)
            {
                _audio.StopWalk();
            }
        }

        private void HandleArmorBroken()
        {
            _audio.PlayArmorBroken();
        }


        public void SetWeaponUsable(bool usable)
        {
            _weaponUsable = usable;
        }

        public void SetShelterMode(bool isShelter)
        {
            _isShelter = isShelter;
            _animation ??= GetComponent<PlayerAnimationFeedback>();
            _animation?.SetShelterMode(isShelter);
        }
    }
}
