using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using PlayerComponent = ShotgunAndRun.Player.Player;

namespace ShotgunAndRun.NPC
{
    // 쉘터 NPC 상호작용. 플레이어가 범위 안에 들어오면 스프라이트 외곽선을 하얗게 켜고, E를 누르면 연결된 UI를 연다.
    // 대화(Dialogue)가 연결되어 있으면 대화를 먼저 재생하고, 대화가 끝난 뒤 UI를 연다. 없으면 바로 연다.
    // 여러 NPC의 범위가 겹치면 가장 가까운 NPC 하나만 외곽선 · 입력을 받는다.
    // 범위는 TeleportPoint와 같은 이유로 콜라이더 대신 플레이어 위치로 판정한다. (기준점은 이 오브젝트 위치)
    public sealed class NpcInteractable : MonoBehaviour
    {
        private static readonly List<NpcInteractable> Instances = new List<NpcInteractable>();

        // E 상호작용을 막고 있는 주체 (예: 열려 있는 강화 창). 하나라도 있으면 어떤 NPC도 E에 반응하지 않는다
        private static readonly HashSet<Object> Blockers = new HashSet<Object>();

        [SerializeField] private string _displayName;
        [Tooltip("상호작용 가능 반경 (월드 단위)")]
        [SerializeField, Min(0.5f)] private float _radius = 1.5f;
        [SerializeField] private SpriteOutline _outline;

        [Tooltip("INpcDialogue를 구현한 컴포넌트. 비워 두면 대화 없이 바로 UI를 연다")]
        [SerializeField] private MonoBehaviour _dialogue;

        [Tooltip("대화가 끝난 뒤(대화가 없으면 E를 누른 즉시) 호출. 씬에서 연결할 UI의 Open을 등록한다")]
        [SerializeField] private UnityEvent _onInteract = new UnityEvent();

        [Tooltip("플레이어가 상호작용 반경 밖으로 나가는 순간 호출. 열어 둔 UI의 Close를 등록한다")]
        [SerializeField] private UnityEvent _onLeaveRange = new UnityEvent();

        private bool _wasPlayerInRange;

        public string DisplayName => _displayName;

        public UnityEvent OnInteract => _onInteract;

        public UnityEvent OnLeaveRange => _onLeaveRange;

        private INpcDialogue Dialogue => _dialogue as INpcDialogue;

        /// <summary>
        /// owner가 E 상호작용을 막거나(blocked) 푼다. 창이 열려 있는 동안 다시 대화가 시작되지 않게 할 때 쓴다.
        /// </summary>
        public static void SetBlocked(Object owner, bool blocked)
        {
            if (owner == null) return;

            if (blocked)
            {
                Blockers.Add(owner);
            }
            else
            {
                Blockers.Remove(owner);
            }
        }

        private void OnEnable()
        {
            Instances.Add(this);
        }

        private void OnDisable()
        {
            Instances.Remove(this);
            SetHighlight(false);
            _wasPlayerInRange = false;
        }

        private void OnValidate()
        {
            if (_dialogue != null && _dialogue is not INpcDialogue)
            {
                Debug.LogWarning($"{name}: Dialogue에는 INpcDialogue를 구현한 컴포넌트만 연결할 수 있습니다.", this);
                _dialogue = null;
            }
        }

        private void Update()
        {
            PlayerComponent player = PlayerComponent.PlayerInstance;

            // 포커스(가장 가까운 NPC)와 상관없이 반경 이탈만 본다. 사망해도 범위 밖으로 취급되어 UI가 닫힌다.
            bool isInRange = CanInteract(player);
            if (_wasPlayerInRange && !isInRange)
            {
                _onLeaveRange.Invoke();
            }
            _wasPlayerInRange = isInRange;

            bool isFocused = IsFocused(player);
            SetHighlight(isFocused);

            if (!isFocused) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame && Blockers.Count == 0)
            {
                Interact();
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, _radius);
        }

        public void Interact()
        {
            INpcDialogue dialogue = Dialogue;
            if (dialogue == null)
            {
                _onInteract.Invoke();
                return;
            }

            if (!dialogue.IsPlaying)
            {
                dialogue.Play(this, _onInteract.Invoke);
            }
        }

        private bool IsFocused(PlayerComponent player)
        {
            if (!CanInteract(player)) return false;

            float distance = SqrDistanceTo(player);
            foreach (NpcInteractable other in Instances)
            {
                if (other == this || !other.CanInteract(player)) continue;

                float otherDistance = other.SqrDistanceTo(player);
                // 거리가 같으면 인스턴스 ID로 한쪽만 고른다.
                if (otherDistance < distance
                    || (Mathf.Approximately(otherDistance, distance) && other.GetInstanceID() < GetInstanceID()))
                {
                    return false;
                }
            }

            return true;
        }

        private bool CanInteract(PlayerComponent player)
        {
            return player != null
                && player.Health != null
                && !player.Health.IsDead
                && SqrDistanceTo(player) <= _radius * _radius;
        }

        private float SqrDistanceTo(PlayerComponent player)
        {
            return ((Vector2)player.transform.position - (Vector2)transform.position).sqrMagnitude;
        }

        private void SetHighlight(bool value)
        {
            if (_outline != null && _outline.Visible != value)
            {
                _outline.SetVisible(value);
            }
        }
    }
}
