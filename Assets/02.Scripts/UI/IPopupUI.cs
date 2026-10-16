namespace ShotgunAndRun.UI
{
    /// <summary>
    /// ESC 키 입력 등으로 최상단부터 순차적으로 닫힐 수 있는 인게임 팝업 창 인터페이스입니다.
    /// </summary>
    public interface IPopupUI
    {
        bool IsOpen { get; }
        void Close();
    }
}
