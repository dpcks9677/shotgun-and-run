using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using PlayerComponent = ShotgunAndRun.Player.Player;

namespace ShotgunAndRun.Core
{
    // 쉘터의 출격 구역
    // 플레이어가 구역에 들어오면 런을 시작합니다. (스테이지 씬 로드는 GameManager가 담당)
    // ExtractionZone과 같은 이유로 콜라이더 대신 플레이어 위치로 판정합니다.
    public sealed class RunEntranceZone : MonoBehaviour
    {
        [Tooltip("구역 크기 (로컬 단위). 오브젝트 위치가 중심입니다.")]
        [SerializeField] private Vector2 _size = new Vector2(4f, 2f);

        [Tooltip("이 영구 플래그가 모두 있어야 출격합니다. (예: FLAG_Q1_ACCEPTED)")]
        [SerializeField] private List<string> _requiredFlags = new List<string>();

        [Tooltip("플래그가 부족한 상태로 플레이어가 구역에 들어오는 순간 호출. 안내 말풍선 등을 연결합니다.")]
        [SerializeField] private UnityEvent _onLockedEnter = new UnityEvent();

        private bool _entered;
        private bool _wasLockedInside;

        private void Update()
        {
            if (_entered)
            {
                return;
            }

            if (!ContainsPlayer())
            {
                _wasLockedInside = false;
                return;
            }

            // 플래그가 부족하면 출격하지 않고, 구역에 들어올 때마다 한 번만 안내합니다.
            if (!HasRequiredFlags())
            {
                if (!_wasLockedInside)
                {
                    _wasLockedInside = true;
                    _onLockedEnter.Invoke();
                }

                return;
            }

            // 씬 로드가 끝날 때까지 여러 번 호출되지 않도록 한 번만 처리합니다.
            _entered = true;

            GameManager gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                Debug.LogWarning("GameManager is not available.", this);
                return;
            }

            gameManager.StartRun();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.9f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, _size);
        }

        private void OnValidate()
        {
            _size = Vector2.Max(_size, Vector2.zero);
        }

        public bool Contains(Vector2 worldPosition)
        {
            Vector2 local = transform.InverseTransformPoint(worldPosition);
            Vector2 half = _size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y;
        }

        // GameManager/진행도가 없으면 기존 동작(출격 시도)을 유지합니다.
        private bool HasRequiredFlags()
        {
            var quests = GameManager.Instance != null ? GameManager.Instance.Progress?.Quests : null;
            if (quests == null)
            {
                return true;
            }

            foreach (string flag in _requiredFlags)
            {
                if (!quests.HasFlag(flag))
                {
                    return false;
                }
            }

            return true;
        }

        private bool ContainsPlayer()
        {
            PlayerComponent player = PlayerComponent.PlayerInstance;
            return player != null && Contains(player.transform.position);
        }
    }
}
