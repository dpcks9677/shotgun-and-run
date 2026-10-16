namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 디버그 재생 옵션. Cutscene Debugger 창이 CutsceneDirector.PlayDebug에 넘깁니다.
    /// </summary>
    public struct CutsceneDebugOptions
    {
        /// <summary>
        /// 재생 전에 플레이어를 연출 위치로 텔레포트합니다.
        /// </summary>
        public bool Teleport;

        /// <summary>
        /// 컷씬이 끝까지 재생되면 그 지점부터 씬의 실제 진행 흐름을 이어서 진행합니다.
        /// </summary>
        public bool ResumeAfter;

        /// <summary>
        /// 컷씬의 완료 플래그를 저장합니다. 끄면 세이브에 아무것도 남기지 않습니다.
        /// </summary>
        public bool SaveCompletionFlags;
    }

    /// <summary>
    /// 씬의 진행 흐름(튜토리얼 등)이 컷씬 디버그 재생을 받아 주기 위한 인터페이스입니다.
    /// 디버그로 컷씬을 고르면 흐름을 멈추고, 그 컷씬이 원래 재생되는 시점의 게임 상태를 만들어 줘야 합니다.
    /// CutsceneDirector가 씬에서 이 인터페이스를 구현한 컴포넌트를 찾아 호출하므로 Director는 씬 고유 코드를 모릅니다.
    /// </summary>
    public interface ICutsceneDebugHost
    {
        /// <summary>
        /// 디버그 재생 직전에 호출됩니다. 진행 중이던 흐름을 멈추고 asset의 시작 직전 상태를 만듭니다.
        /// 이 구현체가 모르는 컷씬이면 아무것도 하지 않고 true를 돌려줍니다. 상태를 만들지 못했으면 false입니다.
        /// </summary>
        public bool PrepareForDebug(CutsceneAsset asset);

        /// <summary>
        /// 디버그로 재생한 컷씬이 끝났을 때(중단 포함) 호출됩니다.
        /// completed는 끝까지 재생했는지, resume은 옵션의 "이어서 진행" 값입니다.
        /// </summary>
        public void OnDebugFinished(CutsceneAsset asset, bool completed, bool resume);

        /// <summary>
        /// 디버거 창에 보여 줄 현재 진행 상태 요약. 보여 줄 것이 없으면 null입니다.
        /// </summary>
        public string DescribeDebugState();
    }
}
