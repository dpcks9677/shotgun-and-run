using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ShotgunAndRun.Player
{
    // Unity Input System에서 Move, AimPoint, Dash 입력을 읽고,
    // 이를 다른 플레이어 컴포넌트에 이벤트로 전달하는 클래스
    [DisallowMultipleComponent] // 동일한 GameObject에 PlayerInputReader가 여러 개 추가되는 것을 방지
    public sealed class PlayerInputReader : MonoBehaviour
    {
        private const string PlayerMapName = "Player";
        private const string MoveActionName = "Move";
        private const string AimActionName = "AimPoint";
        private const string DashActionName = "Dash";
        private const string FireActionName = "Fire";
        private const string ReloadActionName = "Reload";
        private const string MoveDashActionName = "MoveDash";

        [SerializeField] private InputActionAsset _inputActions;

        private InputActionMap _playerMap;
        private InputAction _moveAction;
        private InputAction _aimAction;
        private InputAction _dashAction;
        private InputAction _fireAction;
        private InputAction _reloadAction;
        private InputAction _moveDashAction;
        
        private bool _initialized;
        private bool _callbacksBound;
        private bool _gameplayEnabled = true;
        private bool _actionsEnabled = true;

        // 외부로 제공되는 이벤트들
        // 이벤트가 발생했을 때 알림을 발생하고 Value가 있다면 함께 넘겨주며 연결된 함수 실행
        // ex) Player에서 _input.MoveChanged += HandleMoveChanged;로 이벤트와 함수를 연결
        // MoveChanged 이벤트가 발생하면 HandleMoveChanged가 실행되는 것
        public event Action<Vector2> MoveChanged;
        public event Action<Vector2> AimChanged;
        public event Action<Vector2> MoveDashPressed;
        public event Action DashPressed;
        public event Action FirePressed;
        public event Action FireReleased;
        public event Action ReloadPressed;

        public bool IsFireHeld => _fireAction != null && _fireAction.IsPressed();

        public Vector2 MoveInput
        {
            get;
            private set;
        }

        public Vector2 PointerScreenPosition
        {
            get;
            private set;
        }

        public bool Initialize()
        {
            if (_initialized)
            {
                return true;
            }

            if (_inputActions == null)
            {
                Debug.LogError("InputSystem_Actions is not assigned to PlayerInputReader.", this);
                enabled = false;
                return false;
            }

            // "Player" 이름에 등록된 ActionMap을 가져와 할당
            _playerMap = _inputActions.FindActionMap(PlayerMapName, false);
            _moveAction = _playerMap?.FindAction(MoveActionName, false);
            _aimAction = _playerMap?.FindAction(AimActionName, false);
            _dashAction = _playerMap?.FindAction(DashActionName, false);
            _fireAction = _playerMap?.FindAction(FireActionName, false);
            _reloadAction = _playerMap?.FindAction(ReloadActionName, false);
            _moveDashAction = _playerMap?.FindAction(MoveDashActionName, false);

            if (_playerMap == null || _moveAction == null || _aimAction == null || _dashAction == null
                || _fireAction == null || _reloadAction == null || _moveDashAction == null)
            {
                Debug.LogError(
                    $"The '{PlayerMapName}' input map must contain '{MoveActionName}', " +
                    $"'{AimActionName}', and '{DashActionName}', and '{FireActionName}' and " +
                    $"'{ReloadActionName}' actions.",
                    this);
                enabled = false;
                return false;
            }

            _initialized = true;
            if (isActiveAndEnabled)
            {
                BindCallbacks();
                ApplyEnabledState();
            }

            return true;
        }

        public void SetGameplayEnabled(bool value)
        {
            _gameplayEnabled = value;
            if (!_initialized)
            {
                return;
            }

            ApplyEnabledState();
            if (!value)
            {
                SetMoveInput(Vector2.zero);
            }
        }

        // 조준(AimPoint)은 유지하고 이동 · 대시 · 사격 · 재장전 입력만 막습니다.
        // 순간이동 채널링처럼 캐릭터는 멈추지만 커서 방향은 계속 바라봐야 할 때 사용합니다.
        public void SetActionsEnabled(bool value)
        {
            _actionsEnabled = value;
            if (!_initialized)
            {
                return;
            }

            ApplyEnabledState();
            if (!value)
            {
                SetMoveInput(Vector2.zero);
            }
        }

        private void OnEnable()
        {
            if (!Initialize())
            {
                return;
            }

            BindCallbacks();
            ApplyEnabledState();
            SetMoveInput(Vector2.ClampMagnitude(_moveAction.ReadValue<Vector2>(), 1f));
            SetAimPosition(_aimAction.ReadValue<Vector2>());
        }

        private void OnDisable()
        {
            if (!_initialized)
            {
                return;
            }

            _playerMap.Disable();
            UnbindCallbacks();
            SetMoveInput(Vector2.zero);
        }

        // Input Action의 performed, canceled 이벤트와 처리 메서드를 연결하여
        // _callbacksBound가 중복 등록을 방지합니다.
        private void BindCallbacks()
        {
            if (_callbacksBound)
            {
                return;
            }

            _moveAction.performed += HandleMove;
            _moveAction.canceled += HandleMove;
            _aimAction.performed += HandleAim;
            _aimAction.canceled += HandleAim;
            _dashAction.performed += HandleDash;
            _fireAction.performed += HandleFire;
            _fireAction.canceled += HandleFireCanceled;
            _reloadAction.performed += HandleReload;
            _moveDashAction.performed += HandleMoveDash;
            _callbacksBound = true;
        }

        private void UnbindCallbacks()
        {
            if (!_callbacksBound)
            {
                return;
            }

            _moveAction.performed -= HandleMove;
            _moveAction.canceled -= HandleMove;
            _aimAction.performed -= HandleAim;
            _aimAction.canceled -= HandleAim;
            _dashAction.performed -= HandleDash;
            _fireAction.performed -= HandleFire;
            _fireAction.canceled -= HandleFireCanceled;
            _reloadAction.performed -= HandleReload;
            _moveDashAction.performed -= HandleMoveDash;
            _callbacksBound = false;
        }

        // 게임 플레이 입력이 허용된 상태라면 Player Action Map을 활성화
        private void ApplyEnabledState()
        {
            if (_gameplayEnabled && isActiveAndEnabled)
            {
                _playerMap.Enable();

                if (!_actionsEnabled)
                {
                    _moveAction.Disable();
                    _dashAction.Disable();
                    _fireAction.Disable();
                    _reloadAction.Disable();
                    _moveDashAction.Disable();
                }
            }
            else
            {
                _playerMap.Disable();
            }
        }

        // Action이 활성화되는 순간 현재 눌려 있는 이동키 값을 바로 읽어
        // Move Action의 현재 값을 Vector2로 가져온다.
        private void HandleMove(InputAction.CallbackContext context)
        {
            // ClampMagnitude - 벡터의 크기가 1보다 커지지 않도록 제한
            SetMoveInput(Vector2.ClampMagnitude(context.ReadValue<Vector2>(), 1f));
        }

        private void HandleAim(InputAction.CallbackContext context)
        {
            SetAimPosition(context.ReadValue<Vector2>());
        }

        private void HandleDash(InputAction.CallbackContext context)
        {
            if (_gameplayEnabled)
            {
                DashPressed?.Invoke();
            }
        }

        private void HandleMoveDash(InputAction.CallbackContext context)
        {
            if (!_gameplayEnabled)
            {
                return;
            }

            // 현재 WASD 입력 방향을 그대로 전달
            MoveDashPressed?.Invoke(MoveInput);
        }

        private static readonly List<RaycastResult> RaycastResults = new();
        private PointerEventData _pointerEventData;

        private void HandleFire(InputAction.CallbackContext context)
        {
            if (!_gameplayEnabled)
            {
                return;
            }

            // 마우스 포인터가 UI 버튼/패널 위에 있다면 사격 이벤트를 발생시키지 않음
            if (IsPointerOverUI())
            {
                return;
            }

            FirePressed?.Invoke();
        }

        private void HandleFireCanceled(InputAction.CallbackContext context)
        {
            if (_gameplayEnabled)
            {
                FireReleased?.Invoke();
            }
        }

        /// <summary>
        /// New Input System 콜백 내에서도 프레임 타이밍 경고 없이 현재 마우스 위치의 UI 히트 여부를 실시간으로 판정합니다.
        /// </summary>
        private bool IsPointerOverUI()
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            if (_pointerEventData == null)
            {
                _pointerEventData = new PointerEventData(EventSystem.current);
            }

            Vector2 mousePosition = PointerScreenPosition;
            if (mousePosition == Vector2.zero && Mouse.current != null)
            {
                mousePosition = Mouse.current.position.ReadValue();
            }

            _pointerEventData.position = mousePosition;
            RaycastResults.Clear();
            EventSystem.current.RaycastAll(_pointerEventData, RaycastResults);

            return RaycastResults.Count > 0;
        }

        private void HandleReload(InputAction.CallbackContext context)
        {
            if (_gameplayEnabled)
            {
                ReloadPressed?.Invoke();
            }
        }

        // 새로운 이동 입력을 저장하고 Invoke를 통해 외부에 알린다.
        // 그러면 이 이벤트와 연결된 함수를 실행한다.
        // MoveChanged의 경우 PlayerMove의 위치값을 변경시킨다.
        // 결과적으로 PlayerMotor에서 FixedUpdate에서 지속적으로
        // 물리적 속도를 변환하는 부분에 의해 움직이게 되는 것이다.
        private void SetMoveInput(Vector2 value)
        {
            if (MoveInput == value)
            {
                return;
            }

            MoveInput = value;
            MoveChanged?.Invoke(value);
        }

        private void SetAimPosition(Vector2 value)
        {
            if (PointerScreenPosition == value)
            {
                return;
            }

            PointerScreenPosition = value;
            AimChanged?.Invoke(value);
        }
    }
}
