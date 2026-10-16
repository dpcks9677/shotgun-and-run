using System.Collections;
using UnityEngine;

namespace ShotgunAndRun.Enemy
{
    /// <summary>
    /// 습격 웨이브 포탈. 바닥에서 솟아오르고(Rise) → 대기 루프(Idle) → 닫힘(Close, 등장의 역재생) 후 제거된다.
    /// 방향은 정면 / 대각선 왼쪽 / 대각선 오른쪽 3종 중 목표(플레이어)를 바라보는 쪽을 고른다.
    /// 상태 이름은 Rise_{방향} · Idle_{방향} · Close_{방향}이고, 전이(Rise→Idle, Idle→Close 트리거)는 컨트롤러가 가진다.
    /// 열려 있는 동안 벽 판정(Wall 레이어 충돌체, 문 바닥 폭)을 가진다. 적은 충돌체 바로 앞(SpawnPosition)에서 나온다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class RaidPortal : MonoBehaviour
    {
        private const string IdlePrefix = "Idle_";
        private const string RisePrefix = "Rise_";
        private const string ClosePrefix = "Close_";
        private static readonly int CloseTrigger = Animator.StringToHash("Close");

        // 목표와 가로 거리가 이 값보다 작으면 정면, 크면 목표 쪽을 바라보는 대각선
        [SerializeField] private float _frontRange = 4f;

        // 벽 판정 충돌체 (Wall 레이어 자식). 열리는 동안 켜고, 닫기 시작하면 끈다.
        [SerializeField] private Collider2D _blockCollider;

        // 적이 나오는 위치: 포탈 바닥 중심에서 문 앞쪽으로의 오프셋. 중심은 벽 충돌체 안이라 스폰 검사에 걸린다.
        [SerializeField] private Vector2 _frontSpawnOffset = new Vector2(0f, -1.1f);
        [SerializeField] private Vector2 _diagonalSpawnOffset = new Vector2(0.7f, -1.1f); // x는 바라보는 쪽 부호를 곱한다

        private Animator _animator;
        private string _facing = "Front";
        private bool _isClosing;

        /// <summary>적을 내보낼 위치 (문 앞). Open 이후에 유효하다.</summary>
        public Vector2 SpawnPosition
        {
            get; private set;
        }
        public Bounds BlockBounds => _blockCollider.bounds;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        /// <summary>목표 위치를 바라보는 방향을 골라 등장 애니메이션을 시작한다.</summary>
        public void Open(Vector2 lookTarget)
        {
            float dx = lookTarget.x - transform.position.x;
            Vector2 offset = _frontSpawnOffset;

            if (dx < -_frontRange)
            {
                _facing = "DiagL"; // 문이 왼쪽 아래를 봄
                offset = new Vector2(-_diagonalSpawnOffset.x, _diagonalSpawnOffset.y);
            }
            else if (dx > _frontRange)
            {
                _facing = "DiagR"; // 문이 오른쪽 아래를 봄
                offset = _diagonalSpawnOffset;
            }
            else
            {
                _facing = "Front";
            }

            SpawnPosition = (Vector2)transform.position + offset;

            if (_blockCollider != null)
            {
                _blockCollider.enabled = true;
            }

            _animator.Play(RisePrefix + _facing, 0, 0f);
        }

        /// <summary>등장이 끝나 대기 상태가 될 때까지 기다린다.</summary>
        public IEnumerator WaitForOpen()
        {
            while (!_animator.GetCurrentAnimatorStateInfo(0).IsName(IdlePrefix + _facing))
            {
                yield return null;
            }
        }

        /// <summary>닫힘 애니메이션을 재생하고 끝나면 오브젝트를 제거한다.</summary>
        public void Close()
        {
            if (_isClosing) return;

            _isClosing = true;

            // 닫히는 동안에는 막지 않는다 (지나가던 유닛이 끼지 않게)
            if (_blockCollider != null)
            {
                _blockCollider.enabled = false;
            }

            StartCoroutine(CloseRoutine());
        }

        private IEnumerator CloseRoutine()
        {
            _animator.SetTrigger(CloseTrigger);

            // 등장 중에 닫기를 요청해도 대기 상태에 도달하면 곧바로 닫힘으로 넘어간다.
            while (!_animator.GetCurrentAnimatorStateInfo(0).IsName(ClosePrefix + _facing))
            {
                yield return null;
            }

            while (_animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
            {
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
