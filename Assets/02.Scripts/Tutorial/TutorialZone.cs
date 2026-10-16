using System;
using ShotgunAndRun.Cutscene;
using UnityEngine;
using PlayerComponent = ShotgunAndRun.Player.Player;

namespace ShotgunAndRun.Tutorial
{
    /// <summary>
    /// (프로토타입) 튜토리얼 진행 구역입니다. 플레이어가 들어오면 TutorialFlow가 다음 단계(안내 팝업 · 컷씬 · 전투)를 시작합니다.
    /// 트리거 콜라이더가 필요하며, 키로 구역을 구분합니다. (예: Move, Dash, OctagonEntry, Loot, ArenaEntry)
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class TutorialZone : MonoBehaviour, ICutsceneSpawnPoint
    {
        [Tooltip("구역 키 (TutorialFlow가 찾는 이름)")]
        [SerializeField] private string _key;

        public string Key => _key;

        public Vector3 SpawnPosition
        {
            get
            {
                var col = GetComponent<Collider2D>();
                return col != null ? col.bounds.center : transform.position;
            }
        }

        /// <summary>
        /// 지금 플레이어가 구역 안에 있는지
        /// </summary>
        public bool PlayerInside { get; private set; }

        /// <summary>
        /// 마지막 ResetEntered 이후 플레이어가 한 번이라도 들어왔는지. 빨리 지나쳐도 남습니다.
        /// </summary>
        public bool HasEntered { get; private set; }

        /// <summary>
        /// 들어온 기록을 지웁니다. 지금 구역 안에 있으면 들어온 것으로 칩니다. (단계가 시작될 때)
        /// </summary>
        public void ResetEntered()
        {
            HasEntered = PlayerInside;
        }

        public event Action Entered;
        public event Action Exited;

        private void Awake()
        {
            // 히트스캔 탄이 트리거를 벽으로 오인해 중간에 막히지 않도록 레이캐스트 대상에서 뺀다 (플레이어와의 트리거 감지에는 영향 없음)
            gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (PlayerInside || other.GetComponentInParent<PlayerComponent>() == null) return;

            PlayerInside = true;
            HasEntered = true;
            Entered?.Invoke();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!PlayerInside || other.GetComponentInParent<PlayerComponent>() == null) return;

            // 플레이어 콜라이더가 여럿이라 하나만 나가도 호출될 수 있으므로, 구역 안에 남은 콜라이더가 없을 때만 나갔다고 본다
            foreach (var collider in other.GetComponentInParent<PlayerComponent>().GetComponentsInChildren<Collider2D>())
            {
                if (collider != null && collider.enabled && collider.IsTouching(GetComponent<Collider2D>())) return;
            }

            PlayerInside = false;
            Exited?.Invoke();
        }

        private void OnDisable()
        {
            PlayerInside = false;
        }
    }
}
