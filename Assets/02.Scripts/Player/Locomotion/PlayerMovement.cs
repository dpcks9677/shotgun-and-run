using System;
using UnityEngine;

namespace ShotgunAndRun.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerMovement : MonoBehaviour
    {
        private PlayerRuntimeStats _stats;
        private bool _initialized;
        private bool _movementEnabled = true;

        public PlayerRuntimeStats Stats => _stats;

        public event Action MoveChanged;

        public Vector2 MoveInput
        {
            get;
            private set;
        }

        public bool IsMoving => _movementEnabled && MoveInput.sqrMagnitude > 0.01f;

        // _movementEnabled에 따라 속도를 지정
        public Vector2 DesiredVelocity => _initialized && _movementEnabled
            ? MoveInput * _stats.MoveSpeed
            : Vector2.zero;

        public void Initialize(PlayerRuntimeStats stats)
        {
            _stats = stats ?? throw new System.ArgumentNullException(nameof(stats));
            _initialized = true;
        }

        public void SetInput(Vector2 input)
        {
            Vector2 clampedInput = Vector2.ClampMagnitude(input, 1f);

            if (MoveInput == clampedInput)
            {
                return;
            }

            MoveInput = clampedInput;

            MoveChanged?.Invoke();
        }

        public void SetMovementEnabled(bool value)
        {
            _movementEnabled = value;
            if (!value)
            {
                MoveInput = Vector2.zero;
            }

            MoveChanged?.Invoke();
        }
    }
}
