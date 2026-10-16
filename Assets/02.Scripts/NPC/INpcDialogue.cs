using System;

namespace ShotgunAndRun.NPC
{
    // NPC 상호작용 중간에 끼워 넣는 대화 단계. 대화 시스템이 생기면 이 인터페이스를 구현한 컴포넌트를
    // NpcInteractable의 Dialogue 칸에 연결한다. 대화가 끝나면 onFinished를 호출해야 연결된 UI가 열린다.
    // (대화를 중간에 닫아 UI를 열지 않으려면 onFinished를 호출하지 않으면 된다)
    public interface INpcDialogue
    {
        public bool IsPlaying
        {
            get;
        }

        public void Play(NpcInteractable speaker, Action onFinished);
    }
}
