using System.Collections.Generic;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 컷씬에서 연출로만 움직이는 무리(예: 몰려오는 괴물 떼)입니다. 적 AI · 체력 · 충돌 없이 Transform으로만 움직입니다.
    /// 이 오브젝트의 자식으로 모양 프리팹(SpriteRenderer + Animator)을 한 마리씩 손으로 배치해 두면, 컷씬 코드가 키로 찾아 한꺼번에 움직입니다.
    /// 걷기는 Animator의 Bool 'isMoving', 쓰러짐은 Trigger 'death'(적 공용 컨트롤러와 같음), 방향은 SpriteRenderer.flipX로 처리합니다.
    /// 전투가 필요한 적은 이 컴포넌트가 아니라 CutsceneEnemySpawner로 소환하세요.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CutsceneCrowd : MonoBehaviour
    {
        private static readonly int IsMovingHash = Animator.StringToHash("isMoving");
        private static readonly int DeathHash = Animator.StringToHash("death");

        [Tooltip("컷씬 코드에서 쓰는 무리 키 (예: Horde)")]
        [SerializeField] private string _key;

        [Tooltip("스프라이트 원본이 오른쪽을 보고 있으면 체크 (flipX가 켜지면 왼쪽을 봄)")]
        [SerializeField] private bool _spriteFacesRight = true;

        [Tooltip("컷씬이 시작되기 전에는 숨깁니다. 컷씬 코드가 Show를 부르면 나타납니다.")]
        [SerializeField] private bool _hiddenUntilShown = true;

        private sealed class Member
        {
            public Transform Transform;
            public Animator Animator;
            public SpriteRenderer[] Renderers;
            public bool IsDead;
        }

        private readonly List<Member> _members = new List<Member>();
        private bool _collected;

        public string Key => _key;
        public int Count
        {
            get
            {
                Collect();
                return _members.Count;
            }
        }

        /// <summary>
        /// 아직 쓰러지지 않은 개체 수
        /// </summary>
        public int AliveCount
        {
            get
            {
                Collect();
                int alive = 0;
                foreach (var member in _members)
                {
                    if (!member.IsDead) alive++;
                }

                return alive;
            }
        }

        private void Awake()
        {
            Collect();
            if (_hiddenUntilShown)
            {
                SetVisible(false);
            }
        }

        /// <summary>
        /// 재생 전 사전 검사. 문제가 있으면 errors에 추가합니다.
        /// </summary>
        internal void Validate(List<string> errors)
        {
            Collect();
            if (string.IsNullOrWhiteSpace(_key))
            {
                errors.Add($"'{name}'의 CutsceneCrowd에 키가 비어 있습니다.");
            }

            if (_members.Count == 0)
            {
                errors.Add($"'{name}' 무리(CutsceneCrowd)에 자식 오브젝트가 없습니다.");
            }

            foreach (var member in _members)
            {
                if (member.Renderers.Length == 0)
                {
                    errors.Add($"'{name}' 무리의 '{member.Transform.name}'에 SpriteRenderer가 없습니다.");
                }
            }
        }

        public void SetVisible(bool visible)
        {
            Collect();
            foreach (var member in _members)
            {
                foreach (var spriteRenderer in member.Renderers)
                {
                    spriteRenderer.enabled = visible;
                }
            }
        }

        /// <summary>
        /// 무리 전원이 마커 목록을 따라 걷습니다. keepFormation이 켜져 있으면 출발할 때의 대형(무리 중심으로부터 각자의 간격)을 그대로 유지한 채
        /// 중심이 마커를 지나가므로, 복도 폭을 채운 무리가 한 줄로 뭉치지 않습니다. 꺼져 있으면 각자 마커 한 점으로 모입니다.
        /// 각자 자기 자리에서 출발하므로 뒤에 있던 개체가 뒤따라 도착하고, startDelayStep은 개체 순번마다 늘어나는 출발 지연(초)입니다.
        /// 걷는 속도는 개체마다 ±speedJitter 비율로 조금씩 다르게 해 무리다운 흐트러짐을 줍니다.
        /// </summary>
        internal System.Collections.IEnumerator MovePath(IReadOnlyList<Vector3> points, float speed, float startDelayStep, bool keepFormation = true, float speedJitter = 0.04f)
        {
            Collect();
            var offsets = new Vector3[_members.Count];
            if (keepFormation && _members.Count > 0)
            {
                Vector3 center = Vector3.zero;
                foreach (var member in _members) center += member.Transform.position;
                center /= _members.Count;
                for (int i = 0; i < _members.Count; i++)
                {
                    Vector3 offset = _members[i].Transform.position - center;
                    offsets[i] = new Vector3(offset.x, offset.y, 0f);
                }
            }

            int running = _members.Count;
            for (int i = 0; i < _members.Count; i++)
            {
                float jitter = 1f + (Mathf.Repeat(Mathf.Sin(i * 12.9898f) * 43758.5453f, 1f) - 0.5f) * 2f * speedJitter;
                StartCoroutine(MemberRoutine(_members[i], points, offsets[i], speed * jitter, i * startDelayStep, () => running--));
            }

            while (running > 0)
            {
                yield return null;
            }
        }

        /// <summary>
        /// 무리가 대상(target) 바로 뒤를 따라 달립니다. 무리의 맨 앞 개체가 대상에서 gap(유닛)만큼 뒤에 붙도록 무리 중심을 옮기고,
        /// 멀면 catchUpSpeed로 따라잡다가 붙으면 followSpeed(대상의 속도와 같게)로 간격을 유지합니다. 대형은 지금 모양 그대로입니다.
        /// 대상이 오른쪽으로 달린다고 보고 대상의 왼쪽(x가 작은 쪽)에 붙습니다. 끝나지 않으므로 StopAll로 멈추세요.
        /// </summary>
        internal System.Collections.IEnumerator Follow(System.Func<Vector3> target, float gap, float followSpeed, float catchUpSpeed, float laneSpeed = 4f)
        {
            Collect();
            if (_members.Count == 0) yield break;

            Vector3 center = Vector3.zero;
            foreach (var member in _members) center += member.Transform.position;
            center /= _members.Count;

            var offsets = new Vector3[_members.Count];
            for (int i = 0; i < _members.Count; i++)
            {
                Vector3 offset = _members[i].Transform.position - center;
                offsets[i] = new Vector3(offset.x, offset.y, 0f);
            }

            foreach (var member in _members)
            {
                if (member.IsDead) continue;
                SetMoving(member, true);
                Face(member, left: false);
            }

            while (true)
            {
                // 맨 앞 개체는 살아 있는 개체 중에서 고르므로, 앞 개체가 쓰러지면 다음 개체가 그 자리까지 따라붙는다
                float front = float.MinValue;
                for (int i = 0; i < _members.Count; i++)
                {
                    if (!_members[i].IsDead) front = Mathf.Max(front, offsets[i].x);
                }

                if (front == float.MinValue) yield break;

                Vector3 goal = target();
                float desiredX = goal.x - gap - front;
                float behind = desiredX - center.x;

                // 멀수록 빨리 따라잡고, 붙으면 대상과 같은 속도로 간격을 유지한다
                float speed = Mathf.Lerp(followSpeed, catchUpSpeed, Mathf.Clamp01(behind / 4f));
                center.x = Mathf.MoveTowards(center.x, desiredX, speed * Time.deltaTime);
                center.y = Mathf.MoveTowards(center.y, goal.y, laneSpeed * Time.deltaTime);

                for (int i = 0; i < _members.Count; i++)
                {
                    if (_members[i].IsDead) continue;
                    Vector3 position = _members[i].Transform.position;
                    _members[i].Transform.position = new Vector3(center.x + offsets[i].x, center.y + offsets[i].y, position.z);
                }

                yield return null;
            }
        }

        /// <summary>
        /// 살아 있는 개체 중 point에 가장 가까운 개체가 쓰러지는 애니메이션을 재생합니다. 쓰러진 개체는 그 자리에 남고 이후 따라오기에서 빠집니다.
        /// </summary>
        internal void KillNearest(Vector3 point)
        {
            var nearest = FindNearestAlive(point);
            if (nearest == null) return;

            nearest.IsDead = true;
            SetMoving(nearest, false);
            if (nearest.Animator != null && nearest.Animator.isActiveAndEnabled)
            {
                nearest.Animator.SetTrigger(DeathHash);
            }
        }

        /// <summary>
        /// 살아 있는 개체 중 point에 가장 가까운 개체의 위치를 찾습니다. 살아 있는 개체가 없으면 false입니다.
        /// </summary>
        internal bool TryGetNearestAlive(Vector3 point, out Vector3 position)
        {
            var nearest = FindNearestAlive(point);
            position = nearest != null ? nearest.Transform.position : Vector3.zero;
            return nearest != null;
        }

        private Member FindNearestAlive(Vector3 point)
        {
            Collect();
            Member nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (var member in _members)
            {
                if (member.IsDead) continue;

                float distance = ((Vector2)(member.Transform.position - point)).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearest = member;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        /// <summary>
        /// 진행 중인 이동을 모두 멈춥니다. (컷씬이 중간에 끝났을 때 정리)
        /// </summary>
        internal void StopAll()
        {
            StopAllCoroutines();
            foreach (var member in _members)
            {
                SetMoving(member, false);
            }
        }

        private System.Collections.IEnumerator MemberRoutine(Member member, IReadOnlyList<Vector3> points, Vector3 offset, float speed, float delay, System.Action onDone)
        {
            try
            {
                float waited = 0f;
                while (waited < delay)
                {
                    yield return null;
                    waited += Time.deltaTime;
                }

                SetMoving(member, true);
                foreach (Vector3 point in points)
                {
                    Vector3 target = new Vector3(point.x + offset.x, point.y + offset.y, member.Transform.position.z);
                    float dx = target.x - member.Transform.position.x;
                    if (Mathf.Abs(dx) > 0.01f)
                    {
                        Face(member, left: dx < 0f);
                    }

                    while ((target - member.Transform.position).sqrMagnitude > 0.0001f)
                    {
                        member.Transform.position = Vector3.MoveTowards(member.Transform.position, target, speed * Time.deltaTime);
                        yield return null;
                    }
                }
            }
            finally
            {
                SetMoving(member, false);
                onDone();
            }
        }

        private void Face(Member member, bool left)
        {
            bool flip = left == _spriteFacesRight;
            foreach (var spriteRenderer in member.Renderers)
            {
                spriteRenderer.flipX = flip;
            }
        }

        private static void SetMoving(Member member, bool moving)
        {
            if (member.Animator != null && member.Animator.isActiveAndEnabled)
            {
                member.Animator.SetBool(IsMovingHash, moving);
            }
        }

        private void Collect()
        {
            if (_collected) return;
            _collected = true;

            foreach (Transform child in transform)
            {
                _members.Add(new Member
                {
                    Transform = child,
                    Animator = child.GetComponentInChildren<Animator>(),
                    Renderers = child.GetComponentsInChildren<SpriteRenderer>(true)
                });
            }
        }
    }
}
