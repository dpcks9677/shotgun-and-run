using System.Collections;
using UnityEngine;

namespace ShotgunAndRun.Player
{
    // (프로토타입) 대쉬 시작 시 발밑에 먼지 파티클을 1개 방출합니다.
    // 정식 통합 시 이 로직은 PlayerVFXFeedback.PlayDash()로 옮기고, ParticleSystem은 _dashEffect에 연결합니다.
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class DashDustVFX : MonoBehaviour
    {
        [SerializeField] private bool _drawnForLeftDash = true;

        [SerializeField] private Vector2 _offset = new Vector2(0.5f, 0f);

        private ParticleSystem _particle;
        private ParticleSystemRenderer _renderer;
        private float _lastHorizontalSign = 1f;

        private void Awake()
        {
            _particle = GetComponent<ParticleSystem>();
            _renderer = GetComponent<ParticleSystemRenderer>();
        }

        public void Play(Vector2 direction)
        {
            // 수직 대쉬는 좌우 판단이 안 되므로 직전 좌우 방향을 유지합니다.
            if (Mathf.Abs(direction.x) > 0.01f)
            {
                _lastHorizontalSign = Mathf.Sign(direction.x);
            }

            bool dashingLeft = _lastHorizontalSign < 0f;
            _renderer.flip = new Vector3(dashingLeft == _drawnForLeftDash ? 0f : 1f, 0f, 0f);

            Vector3 origin = transform.position;
            var emitParams = new ParticleSystem.EmitParams
            {
                position = origin + new Vector3(-_lastHorizontalSign * _offset.x, _offset.y, 0f),
                applyShapeToPosition = false
            };
            _particle.Emit(emitParams, 1);

            StartCoroutine(TimeWait());
        }

        private IEnumerator TimeWait()
        {
            yield return new WaitForSeconds(1f);
            gameObject.SetActive(false);
        }
    }
}
