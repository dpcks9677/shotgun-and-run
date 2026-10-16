namespace ShotgunAndRun.UI.Dialogue
{
    /// <summary>
    /// 대화나 튜토리얼 진행 중 게임 시간(Time.timeScale) 일시정지 및 복원을 관리하는 인터페이스 (SRP, DIP)
    /// </summary>
    public interface ITimePauseHandler
    {
        /// <summary>
        /// 현재 일시정지 상태인지 여부
        /// </summary>
        public bool IsPaused
        {
            get;
        }

        /// <summary>
        /// 현재 TimeScale을 백업하고 0으로 일시정지합니다.
        /// </summary>
        public void Pause();

        /// <summary>
        /// 일시정지 전의 원래 TimeScale로 안전하게 복원합니다.
        /// </summary>
        public void Resume();
    }
}
