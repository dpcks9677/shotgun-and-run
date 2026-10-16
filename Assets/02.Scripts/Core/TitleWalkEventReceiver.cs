using UnityEngine;

namespace ShotgunAndRun.Core
{
    // 타이틀의 플레이어 Body는 PlayerBody 없이 Walk 클립만 재생합니다.
    // Walk 클립의 PlayWalk 애니메이션 이벤트가 받을 곳이 없어 오류가 나므로, 빈 수신부를 둡니다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class TitleWalkEventReceiver : MonoBehaviour
    {
        public void PlayWalk()
        {
        }
    }
}
