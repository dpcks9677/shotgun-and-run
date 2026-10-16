using UnityEngine;

namespace ShotgunAndRun.Core
{
    // 외곽선 렌더러들이 원본 렌더러의 현재 스프라이트를 따라가게 합니다.
    // 순간이동 지점의 외곽선(기둥을 1픽셀씩 밀어 단색으로 그린 복사본)은 정적 스프라이트였는데,
    // 기둥을 애니메이션 포탈로 바꾸면 프레임이 바뀔 때마다 외곽선도 같이 바뀌어야 합니다.
    // 외곽선 오브젝트가 켜져 있는 동안(= 사용 가능 범위 안)에만 돕니다.
    public sealed class OutlineSpriteSync : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _source;
        [SerializeField] private SpriteRenderer[] _outlines;

        private void OnEnable()
        {
            Sync();
        }

        private void LateUpdate()
        {
            Sync();
        }

        private void Sync()
        {
            if (_source == null)
            {
                return;
            }

            foreach (SpriteRenderer outline in _outlines)
            {
                if (outline == null)
                {
                    continue;
                }

                outline.sprite = _source.sprite;
                outline.flipX = _source.flipX;
                outline.flipY = _source.flipY;
            }
        }
    }
}
