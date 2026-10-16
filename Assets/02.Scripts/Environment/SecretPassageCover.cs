using UnityEngine;
using UnityEngine.Tilemaps;

namespace ShotgunAndRun.Environment
{
    // 비밀 통로 덮개. 통로가 뚫리기 전 모습(벽 · 창살 · 벽 바깥 어둠)을 그린 자식 타일맵들로 통로를 가려 두고,
    // 플레이어가 트리거(통로 입구 칸)에 들어오면 덮개를 서서히 투명하게 만들어 통로를 드러낸다. 한 번 드러나면 다시 가리지 않는다.
    // 덮개 타일맵에는 충돌이 없다 — 통로 바닥은 실제로 뚫려 있어 그대로 걸어 들어갈 수 있다.
    // 사용처: R7 회랑 감방 비밀방 (오른쪽에서 세 번째 감방 x257~260 → 통로 R7-S1 22u → 비밀방 S1).
    // 추후 패치 예정 (현재 동작은 유지): 감방 앞에 상호작용 NPC를 두고, 관련 퀘스트를 받은 상태에서 아이템을 얻은 뒤
    // 그 NPC와 상호작용해야 통로를 지나갈 수 있게 바꾼다. 그때는 입구를 막는 충돌을 따로 두고 조건을 채우면 풀어 주어야 한다.
    // 현재 비활성 (2026-10-14): 트리거 콜라이더를 꺼 두고, 자식 R7 Secret Blocker(Wall 레이어 박스)가 통로와 S1 전체를 막는다.
    // 플레이어 진입 · 적 길찾기 · 습격 포탈 스폰이 모두 막힌다. 다시 열 때는 블로커를 끄고 트리거를 켠다.
    [RequireComponent(typeof(Collider2D))]
    public sealed class SecretPassageCover : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _fadeDuration = 0.4f;

        private Tilemap[] _covers;
        private bool _revealed;
        private float _alpha = 1f;

        private void Awake()
        {
            _covers = GetComponentsInChildren<Tilemap>();
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void Update()
        {
            if (!_revealed || _alpha <= 0f) return;

            _alpha = _fadeDuration > 0f ? Mathf.Max(0f, _alpha - Time.deltaTime / _fadeDuration) : 0f;
            foreach (var cover in _covers)
            {
                var color = cover.color;
                color.a = _alpha;
                cover.color = color;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_revealed) return;

            var body = other.attachedRigidbody;
            if (other.CompareTag("Player") || (body != null && body.CompareTag("Player"))) _revealed = true;
        }
    }
}
