using System;
using TMPro;

namespace ShotgunAndRun.UI.Dialogue
{
    /// <summary>
    /// 텍스트 표출 애니메이션(타이핑, 페이드 등)을 담당하는 컴포넌트 인터페이스 (SRP, DIP)
    /// </summary>
    public interface ITextAnimator
    {
        /// <summary>
        /// 현재 텍스트 애니메이션이 진행 중인지 여부
        /// </summary>
        public bool IsAnimating
        {
            get;
        }

        /// <summary>
        /// 주어진 대상 Text 컴포넌트에 목표 텍스트를 애니메이션으로 출력합니다.
        /// </summary>
        public void AnimateText(TextMeshProUGUI targetText, string fullContent, Action onComplete);

        /// <summary>
        /// 진행 중인 애니메이션을 즉시 완료하고 전체 텍스트를 표시합니다.
        /// </summary>
        public void CompleteImmediately(TextMeshProUGUI targetText, Action onComplete);

        /// <summary>
        /// 진행 중인 애니메이션을 중단합니다.
        /// </summary>
        public void Stop();
    }
}
