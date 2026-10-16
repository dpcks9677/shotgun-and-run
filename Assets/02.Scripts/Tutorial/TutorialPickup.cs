using System;
using UnityEngine;
using UnityEngine.InputSystem;
using PlayerComponent = ShotgunAndRun.Player.Player;

namespace ShotgunAndRun.Tutorial
{
    /// <summary>
    /// (프로토타입) 튜토리얼에서 줍는 물건(샷건)입니다. 켜져 있는 동안 플레이어가 트리거 구역 안에서 E키를 누르면 Interacted가 발생합니다.
    /// 별도 안내 문구는 띄우지 않습니다. 트리거 콜라이더가 필요합니다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class TutorialPickup : MonoBehaviour
    {
        private bool _active;
        private int _playerColliders;

        public event Action Interacted;

        /// <summary>
        /// 지금 줍기를 받을 수 있는지 (SetActive로 켠 뒤 줍기 전까지)
        /// </summary>
        public bool IsActive => _active;

        /// <summary>
        /// 플레이어가 줍기 범위(트리거) 안에 있는지
        /// </summary>
        public bool PlayerInRange => _playerColliders > 0;

        private void Awake()
        {
            // 히트스캔 탄이 트리거를 벽으로 오인해 중간에 막히지 않도록 레이캐스트 대상에서 뺀다 (플레이어와의 트리거 감지에는 영향 없음)
            gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        }

        /// <summary>
        /// 상호작용을 받을 수 있게 켜거나 끕니다. (컷씬 도중에는 꺼 둡니다)
        /// </summary>
        public void SetActive(bool active)
        {
            _active = active;
        }

        private void Update()
        {
            if (!_active || _playerColliders <= 0) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                _active = false;
                Interacted?.Invoke();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerComponent>() != null) _playerColliders++;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerComponent>() != null) _playerColliders = Mathf.Max(0, _playerColliders - 1);
        }

        private void OnDisable()
        {
            _playerColliders = 0;
        }
    }
}
