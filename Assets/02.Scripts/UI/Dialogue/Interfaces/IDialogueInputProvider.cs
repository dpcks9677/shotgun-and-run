namespace ShotgunAndRun.UI.Dialogue
{
    /// <summary>
    /// 대화 진행 및 즉시 스킵을 위한 사용자 입력을 감지하는 인터페이스 (SRP, DIP)
    /// </summary>
    public interface IDialogueInputProvider
    {
        /// <summary>
        /// 대사 넘기기 또는 스킵 입력이 발생했는지 확인합니다.
        /// </summary>
        public bool IsAdvanceTriggered();

        /// <summary>
        /// 현재 선택된 선택지를 확정하는 입력이 발생했는지 확인합니다. (마우스 클릭은 버튼이 직접 처리하므로 제외)
        /// </summary>
        public bool IsSubmitTriggered();

        /// <summary>
        /// 선택지 이동 입력을 반환합니다. (-1: 위, 1: 아래, 0: 입력 없음)
        /// </summary>
        public int GetChoiceNavigation();
    }
}
