using System.Collections.Generic;
using ShotgunAndRun.Player;
using UnityEngine;
using PlayerComponent = ShotgunAndRun.Player.Player;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 플레이어용 컷씬 인물. 컷씬이 시작될 때 Director가 플레이어에 자동으로 붙입니다.
    /// 이동 중에는 모터를 끄고 위치를 직접 옮기며, 걷기 애니메이션은 이동 입력으로, 방향은 조준점을 고정해서 처리합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCutsceneActor : CutsceneActor
    {
        // 바라볼 방향을 정하려고 조준점을 플레이어 옆 이 거리만큼 떨어진 곳에 둔다
        private const float FacingAimDistance = 5f;

        private PlayerComponent _player;
        private PlayerMotor _motor;
        private PlayerWeapon _weapon;
        private Transform _weaponPivot;
        private Rigidbody2D _rigidbody;
        private Animator _animator;
        private Camera _aimCamera;
        private bool _invulnerable;

        private Collider2D[] _colliders;
        private bool[] _colliderTriggerStates;
        private bool _collidersAsTrigger;

        private bool _holdFacing;

        // 플레이어에서 조준점까지의 거리. 플레이어가 움직여도 같은 방향을 겨눈다
        private Vector3 _aimOffset;

        protected override Animator Animator => _animator;

        /// <summary>
        /// 플레이어에 붙은 PlayerCutsceneActor를 돌려주고, 없으면 붙입니다.
        /// </summary>
        internal static PlayerCutsceneActor GetOrAdd(PlayerComponent player)
        {
            // 에디터에서 GetComponent는 없을 때 가짜 null 객체를 돌려주므로 ?? 대신 == null로 확인한다
            var actor = player.GetComponent<PlayerCutsceneActor>();
            if (actor == null)
            {
                actor = player.gameObject.AddComponent<PlayerCutsceneActor>();
            }
            return actor;
        }

        private void Awake()
        {
            _player = GetComponent<PlayerComponent>();
            _motor = GetComponent<PlayerMotor>();
            _weapon = GetComponent<PlayerWeapon>();
            _weaponPivot = transform.Find("AimOrigin/WeaponPivot");
            _rigidbody = GetComponent<Rigidbody2D>();
            _animator = GetComponentInChildren<PlayerBody>(true).Animator;
        }

        internal override void SetVisible(bool visible)
        {
            base.SetVisible(visible);

            // 무기를 숨기기 전에 플레이어를 숨겼으면 무기 스프라이트도 함께 다시 켜지므로, 무기를 쓸 수 없는 상태면 다시 끈다
            if (visible && _weapon != null && !_weapon.IsWeaponUsable && _weaponPivot != null)
            {
                var weaponSprite = _weaponPivot.GetComponentInChildren<SpriteRenderer>(true);
                if (weaponSprite != null) weaponSprite.enabled = false;
            }
        }

        internal override void Validate(List<string> errors)
        {
            base.Validate(errors);
            if (_player == null) errors.Add("PlayerCutsceneActor가 Player 컴포넌트가 없는 오브젝트에 붙어 있습니다.");
            if (_motor == null) errors.Add("플레이어에 PlayerMotor가 없습니다.");
            if (_rigidbody == null) errors.Add("플레이어에 Rigidbody2D가 없습니다.");
        }

        internal override void BeginCutscene(CutsceneContext context)
        {
            _aimCamera = context.Camera;
            _motor.SetMotorEnabled(false);
            _rigidbody.linearVelocity = Vector2.zero;

            if (context.Asset.PauseGameplay && _player.Invulnerability != null)
            {
                _player.Invulnerability.SetActive(InvulnerabilityReason.Cutscene, true);
                _invulnerable = true;
            }
        }

        internal override void EndCutscene()
        {
            SetMoving(false, Vector2.zero);
            StopClip();
            _holdFacing = false;
            _aimCamera = null;

            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.position = transform.position;

            // 죽은 채 끝난 컷씬(사망 연출)은 사망 때 꺼진 모터를 되살리지 않는다
            if (_player.Health == null || !_player.Health.IsDead)
            {
                _motor.SetMotorEnabled(true);
            }

            if (_invulnerable)
            {
                _player.Invulnerability.SetActive(InvulnerabilityReason.Cutscene, false);
                _invulnerable = false;
            }
        }

        internal override void Teleport(Vector3 position)
        {
            position.z = transform.position.z;
            transform.position = position;
            _rigidbody.position = position;
            _rigidbody.linearVelocity = Vector2.zero;
        }

        internal override void StepTowards(Vector3 target, float maxDistance)
        {
            target.z = transform.position.z;
            Teleport(Vector3.MoveTowards(transform.position, target, maxDistance));
        }

        internal override void SetMoving(bool moving, Vector2 direction)
        {
            // 걷기 애니메이션은 이동 입력이 있을 때 재생되므로 방향만 넣어 준다 (실제 이동은 StepTowards가 한다)
            _player.Movement.SetInput(moving ? direction.normalized : Vector2.zero);

            // 이동 중에는 게이트 차단벽 등에 끼지 않도록 콜라이더를 잠시 트리거로 바꾼다
            SetCollidersAsTrigger(moving);
        }

        internal override void Face(bool left)
        {
            AimAt(transform.position + (left ? Vector3.left : Vector3.right) * FacingAimDistance);
        }

        internal override void AimAt(Vector3 point)
        {
            _aimOffset = point - transform.position;
            _aimOffset.z = 0f;
            _holdFacing = true;
            ApplyFacing();
        }

        private void Update()
        {
            if (_holdFacing)
            {
                ApplyFacing();
            }
        }

        // 조준은 화면 좌표를 매 프레임 월드로 다시 바꾸므로, 카메라가 움직여도 방향이 유지되도록 매 프레임 화면 좌표를 갱신한다
        private void ApplyFacing()
        {
            if (_aimCamera == null) return;

            _player.Aim.SetPointerScreenPosition(_aimCamera.WorldToScreenPoint(transform.position + _aimOffset));
        }

        private void SetCollidersAsTrigger(bool asTrigger)
        {
            if (_collidersAsTrigger == asTrigger) return;
            _collidersAsTrigger = asTrigger;

            if (asTrigger)
            {
                _colliders = GetComponentsInChildren<Collider2D>();
                _colliderTriggerStates = new bool[_colliders.Length];
                for (int i = 0; i < _colliders.Length; i++)
                {
                    _colliderTriggerStates[i] = _colliders[i].isTrigger;
                    _colliders[i].isTrigger = true;
                }
                return;
            }

            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] != null)
                {
                    _colliders[i].isTrigger = _colliderTriggerStates[i];
                }
            }
            Physics2D.SyncTransforms();
        }
    }
}
