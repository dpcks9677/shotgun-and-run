using System;
using ShotgunAndRun.Player;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 컷씬 동안 Main Camera를 넘겨받아 움직입니다.
    /// 시작할 때 PlayerCameraFollow를 끄고, 끝날 때 다시 켜서 플레이어 추적으로 돌려줍니다.
    /// </summary>
    internal sealed class CutsceneCamera
    {
        private readonly PlayerCameraFollow _follow;
        private readonly Transform _rig;
        private bool _followWasEnabled;
        private bool _active;

        private Vector2 _base;
        private Vector2 _from;
        private Func<Vector3> _target;
        private object _targetKey;
        private float _duration;
        private float _elapsed;
        private CutsceneHandle _moveHandle;

        private float _baseSize;
        private float _zoomFrom;
        private float _zoomTo;
        private float _zoomDuration;
        private float _zoomElapsed;
        private CutsceneHandle _zoomHandle;

        private float _shakePower;
        private float _shakeDuration;
        private float _shakeElapsed;
        private CutsceneHandle _shakeHandle;

        public Camera Camera
        {
            get;
        }

        /// <summary>
        /// 화면에 그리는 카메라(Target Texture 없음)의 PlayerCameraFollow를 찾습니다. 미니맵 카메라도 PlayerCameraFollow를 쓰므로 첫 번째 것을 고르면 안 됩니다.
        /// </summary>
        public static PlayerCameraFollow FindMainFollow()
        {
            PlayerCameraFollow fallback = null;
            foreach (var follow in UnityEngine.Object.FindObjectsByType<PlayerCameraFollow>(FindObjectsSortMode.None))
            {
                var camera = follow.GetComponentInChildren<Camera>();
                if (camera != null && camera.targetTexture == null) return follow;
                fallback ??= follow;
            }

            return fallback;
        }

        public CutsceneCamera(PlayerCameraFollow follow)
        {
            _follow = follow;
            _rig = follow.transform;
            Camera = follow.GetComponentInChildren<Camera>();
        }

        public void Begin()
        {
            _active = true;
            _baseSize = Camera.orthographicSize;
            _zoomHandle = null;
            _followWasEnabled = _follow.enabled;
            _follow.enabled = false;

            _base = _rig.position;
            Vector3 fixedPosition = _rig.position;
            _target = () => fixedPosition;
            _targetKey = null;
            _duration = 0f;
            _elapsed = 0f;
        }

        /// <summary>
        /// target 위치로 duration초 동안 부드럽게 이동하고, 그 뒤에도 target을 계속 따라갑니다.
        /// key는 같은 대상을 다시 지정했는지 확인할 때 씁니다.
        /// </summary>
        public CutsceneHandle MoveTo(Func<Vector3> target, object key, float duration)
        {
            _moveHandle?.Complete();
            _moveHandle = new CutsceneHandle();

            _from = _base;
            _target = target;
            _targetKey = key;
            _duration = Mathf.Max(0f, duration);
            _elapsed = 0f;

            if (_duration <= 0f)
            {
                _base = _target();
                _moveHandle.Complete();
            }

            return _moveHandle;
        }

        public bool IsSettledOn(object key)
        {
            return key != null && Equals(_targetKey, key) && _elapsed >= _duration;
        }

        /// <summary>
        /// 카메라 크기(orthographicSize)를 duration초 동안 부드럽게 바꿉니다. 작을수록 확대입니다. 컷씬이 끝나면 원래 크기로 돌아갑니다.
        /// </summary>
        public CutsceneHandle ZoomTo(float size, float duration)
        {
            _zoomHandle?.Complete();
            _zoomHandle = new CutsceneHandle();

            _zoomFrom = Camera.orthographicSize;
            _zoomTo = Mathf.Max(0.1f, size);
            _zoomDuration = Mathf.Max(0f, duration);
            _zoomElapsed = 0f;

            if (_zoomDuration <= 0f)
            {
                Camera.orthographicSize = _zoomTo;
                _zoomHandle.Complete();
            }

            return _zoomHandle;
        }

        /// <summary>
        /// 컷씬이 시작할 때의 카메라 크기
        /// </summary>
        public float BaseSize => _baseSize;

        /// <summary>
        /// 카메라 흔들림이 시작될 때 (세기, 지속 시간)으로 알립니다. 먼지 · 잔해 같은 씬 연출이 흔들림에 맞춰 반응하는 용도입니다.
        /// </summary>
        public static event Action<float, float> Shaken;

        public CutsceneHandle Shake(float power, float duration)
        {
            Shaken?.Invoke(power, duration);
            _shakeHandle?.Complete();
            _shakeHandle = new CutsceneHandle();
            _shakePower = power;
            _shakeDuration = Mathf.Max(0.01f, duration);
            _shakeElapsed = 0f;
            return _shakeHandle;
        }

        public void LateTick(float deltaTime)
        {
            if (_zoomHandle != null && !_zoomHandle.IsDone)
            {
                _zoomElapsed += deltaTime;
                float zoomT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_zoomElapsed / _zoomDuration));
                Camera.orthographicSize = Mathf.Lerp(_zoomFrom, _zoomTo, zoomT);
                if (_zoomElapsed >= _zoomDuration)
                {
                    _zoomHandle.Complete();
                }
            }

            Vector2 goal = _target();
            if (_elapsed < _duration)
            {
                _elapsed += deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_elapsed / _duration));
                _base = Vector2.Lerp(_from, goal, t);
            }
            else
            {
                _base = goal;
            }

            if (_elapsed >= _duration)
            {
                _moveHandle?.Complete();
            }

            Vector2 offset = Vector2.zero;
            if (_shakeHandle != null && !_shakeHandle.IsDone)
            {
                _shakeElapsed += deltaTime;
                float strength = _shakePower * (1f - Mathf.Clamp01(_shakeElapsed / _shakeDuration));
                offset = UnityEngine.Random.insideUnitCircle * strength;
                if (_shakeElapsed >= _shakeDuration)
                {
                    _shakeHandle.Complete();
                }
            }

            _rig.position = new Vector3(_base.x + offset.x, _base.y + offset.y, _rig.position.z);
        }

        public void End()
        {
            _moveHandle?.Complete();
            _shakeHandle?.Complete();
            _zoomHandle?.Complete();
            if (!_active) return;

            // 플레이 종료 · 씬 해제 중에는 카메라가 이미 파괴되어 있을 수 있다
            if (Camera != null)
            {
                Camera.orthographicSize = _baseSize;
            }

            _active = false;
            if (_follow != null)
            {
                _follow.enabled = _followWasEnabled;
            }
        }
    }
}
