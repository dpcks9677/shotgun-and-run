using System.Collections;
using ShotgunAndRun.Core;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 퀘스트 디펜스 중 존 출입구 하나를 막는 문입니다 (game-spec 4.3). QuestDefenseEvent가 시작할 때 닫고, 클리어 · 초기화 때 엽니다.
    /// 막는 콜라이더는 Wall 레이어에 두세요. 적 길찾기(EnemyNavigationGrid)가 Wall 레이어를 매번 물리로 검사하므로 닫힌 문을 벽으로 봅니다.
    /// 닫히는 순간 문간에 있던 적은 방 안쪽(Inward Offset 방향)으로 밀어 넣어 콜라이더에 끼지 않게 합니다.
    /// 문 그림은 위에서 내려와 쾅 하고 닫히며, 내려앉는 순간 카메라가 흔들립니다(닫힘음의 쾅 소리와 맞춤).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DefenseGate : MonoBehaviour
    {
        [Tooltip("닫혔을 때 통로를 막는 콜라이더 (Wall 레이어). 평소에는 꺼 둡니다")]
        [SerializeField] private BoxCollider2D _blocker;

        [Tooltip("닫혔을 때만 보이는 문 그림")]
        [SerializeField] private GameObject _closedVisual;

        [Tooltip("문 위치에서 방 안쪽까지의 월드 오프셋. 문간에 걸친 적을 여기로 옮깁니다 (문 두께 + 적 몸 반지름보다 크게)")]
        [SerializeField] private Vector2 _inwardOffset = new Vector2(0f, 2f);

        [Header("닫힘 연출")]
        [Tooltip("문 그림이 내려오기 시작하는 높이(칸). 0이면 연출 없이 바로 닫힘")]
        [SerializeField, Min(0f)] private float _dropHeight = 2f;

        [Tooltip("내려오는 시간(초). 닫힘음의 쾅(약 0.16초)에 착지를 맞춘다")]
        [SerializeField, Min(0.01f)] private float _dropSeconds = 0.16f;

        [Tooltip("내려앉을 때 카메라 흔들림 세기(칸)와 시간(초). 0이면 흔들지 않음")]
        [SerializeField, Min(0f)] private float _landShake = 0.25f;
        [SerializeField, Min(0f)] private float _landShakeSeconds = 0.25f;

        private static readonly Collider2D[] OverlapResults = new Collider2D[32];

        private Vector3 _visualRestPosition;
        private Coroutine _dropRoutine;

        public bool IsClosed
        {
            get; private set;
        }

        private void Awake()
        {
            if (_closedVisual != null) _visualRestPosition = _closedVisual.transform.localPosition;
            Apply(false);
        }

        public void Close()
        {
            if (IsClosed) return;

            PushEnemiesInside();
            Apply(true);
            if (_closedVisual != null && _dropHeight > 0f) _dropRoutine = StartCoroutine(DropRoutine());
        }

        public void Open()
        {
            if (!IsClosed) return;

            Apply(false);
        }

        private void Apply(bool closed)
        {
            IsClosed = closed;
            if (_dropRoutine != null)
            {
                StopCoroutine(_dropRoutine);
                _dropRoutine = null;
            }

            if (_blocker != null) _blocker.enabled = closed;
            if (_closedVisual != null)
            {
                _closedVisual.SetActive(closed);
                _closedVisual.transform.localPosition = _visualRestPosition;
            }
        }

        // 충돌은 바로 막히고, 문 그림만 위에서 가속하며 내려와 쾅 하고 닫힌다
        private IEnumerator DropRoutine()
        {
            Transform visual = _closedVisual.transform;
            Vector3 start = _visualRestPosition + Vector3.up * _dropHeight;
            for (float elapsed = 0f; elapsed < _dropSeconds; elapsed += Time.deltaTime)
            {
                float progress = elapsed / _dropSeconds;
                visual.localPosition = Vector3.Lerp(start, _visualRestPosition, progress * progress);
                yield return null;
            }

            visual.localPosition = _visualRestPosition;
            _dropRoutine = null;
            if (_landShake > 0f) CameraShakeSignal.Request(_landShake, _landShakeSeconds);
        }

        // 꺼진 콜라이더는 bounds가 비어 있으므로 박스 설정값으로 문 영역을 계산한다 (회전 없는 문 기준)
        private void PushEnemiesInside()
        {
            if (_blocker == null) return;

            Transform box = _blocker.transform;
            Vector2 center = box.TransformPoint(_blocker.offset);
            Vector2 size = Vector2.Scale(_blocker.size, box.lossyScale);
            ContactFilter2D filter = new ContactFilter2D { useLayerMask = true, layerMask = LayerMask.GetMask("Enemy") };
            int count = Physics2D.OverlapBox(center, size, 0f, filter, OverlapResults);

            Vector2 gatePosition = transform.position;
            Vector2 inward = _inwardOffset.normalized;
            for (int i = 0; i < count; i++)
            {
                Rigidbody2D body = OverlapResults[i].attachedRigidbody;
                if (body == null) continue;

                // 문과 나란한 위치는 유지하고 안쪽 방향으로만 옮긴다 (같은 바디가 여러 번 잡혀도 결과가 같음)
                Vector2 relative = body.position - gatePosition;
                Vector2 lateral = relative - inward * Vector2.Dot(relative, inward);
                body.position = gatePosition + _inwardOffset + lateral;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.95f, 0.3f, 0.2f, 0.9f);
            Vector3 from = transform.position;
            Gizmos.DrawLine(from, from + (Vector3)_inwardOffset);
            Gizmos.DrawWireSphere(from + (Vector3)_inwardOffset, 0.3f);
        }
    }
}
