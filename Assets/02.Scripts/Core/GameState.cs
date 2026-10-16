namespace ShotgunAndRun.Core
{
    // GameManager가 관리하는 게임 흐름 상태
    // None은 등록되지 않은 씬(테스트 씬, 샌드박스 씬)에서의 대기 상태입니다.
    // SceneRouter 항목에 정수로 저장되므로 새 상태는 맨 뒤에 추가합니다.
    public enum GameState
    {
        None,
        Title,
        Shelter,
        Run,
        Result,
        Tutorial
    }
}
