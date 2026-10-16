namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 컷씬 명령 하나의 진행 상태입니다.
    /// 명령은 호출 즉시 시작되며, 연출 코드에서 yield return 하면 그 명령이 끝날 때까지 기다립니다.
    /// </summary>
    public class CutsceneHandle
    {
        public bool IsDone
        {
            get; private set;
        }

        internal void Complete()
        {
            IsDone = true;
        }

        internal static CutsceneHandle Done()
        {
            var handle = new CutsceneHandle();
            handle.Complete();
            return handle;
        }
    }

    /// <summary>
    /// 대사 명령의 진행 상태입니다. 끝난 뒤 고른 선택지 ID를 확인할 수 있습니다.
    /// </summary>
    public sealed class DialogueHandle : CutsceneHandle
    {
        /// <summary>
        /// 고른 선택지 ID. 선택지가 없는 대화면 null입니다.
        /// </summary>
        public string ChoiceId
        {
            get; internal set;
        }
    }
}
