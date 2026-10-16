using System;
using System.Linq;
using ShotgunAndRun.UI.Dialogue;

namespace ShotgunAndRun.Cutscene
{
    // 대화 명령: 대사 출력 · 감정 말풍선
    public sealed partial class CutsceneContext
    {
        private DialogueDataSO _activeDialogue;
        private CutsceneActor _lineBubbleActor;
        private Action<int> _onLineStarted;

        /// <summary>
        /// 대사를 출력합니다. 끝나면 핸들의 ChoiceId로 고른 선택지를 확인할 수 있습니다.
        /// 대사별 말풍선 · 효과음 · 신호는 대사 데이터(DialogueLine)에 적힌 대로 처리됩니다.
        /// onLineStarted를 넘기면 각 줄이 나오기 시작할 때 그 줄의 번호(0부터)로 호출합니다. (줄마다 카메라 · 이동 · 효과음을 맞출 때)
        /// </summary>
        public DialogueHandle Say(DialogueDataSO dialogue, Action<int> onLineStarted = null)
        {
            if (dialogue == null || !_requirements.Dialogues.Contains(dialogue))
            {
                throw new InvalidOperationException($"대사 '{(dialogue != null ? dialogue.name : "null")}'는 DeclareRequirements에 선언되지 않았습니다.");
            }

            if (_dialogue.IsDialogueActive)
            {
                throw new InvalidOperationException($"다른 대화가 열려 있어 '{dialogue.name}'를 시작할 수 없습니다. 이전 Say를 yield return으로 기다리세요.");
            }

            var handle = new DialogueHandle();
            _activeDialogue = dialogue;
            _onLineStarted = onLineStarted;
            _dialogue.StartDialogue(
                dialogue,
                onChoiceSelected: choiceId => handle.ChoiceId = choiceId,
                onDialogueComplete: () =>
                {
                    EndLineBubble();
                    _activeDialogue = null;
                    _onLineStarted = null;
                    handle.Complete();
                });

            if (!_dialogue.IsDialogueActive && !handle.IsDone)
            {
                _activeDialogue = null;
                _onLineStarted = null;
                throw new InvalidOperationException($"대화창을 열지 못해 '{dialogue.name}'를 출력할 수 없습니다.");
            }

            return handle;
        }

        /// <summary>
        /// 인물 머리 위에 감정 말풍선을 띄웁니다. seconds를 생략하면 ClearBubble을 부를 때까지 유지합니다.
        /// 핸들은 seconds가 지나면 끝납니다.
        /// </summary>
        public CutsceneHandle Bubble(CutsceneActor actor, EmotionType emotion, float seconds = 0f)
        {
            RequireActor(actor);
            if (seconds <= 0f)
            {
                _director.Bubbles.Show(actor.BubbleTarget, emotion, float.PositiveInfinity);
                return CutsceneHandle.Done();
            }

            _director.Bubbles.Show(actor.BubbleTarget, emotion, seconds);
            return Wait(seconds);
        }

        /// <summary>
        /// 인물의 말풍선을 닫습니다.
        /// </summary>
        public void ClearBubble(CutsceneActor actor)
        {
            RequireActor(actor);
            _director.Bubbles.Dismiss(actor.BubbleTarget);
        }

        /// <summary>
        /// 모든 말풍선을 닫습니다.
        /// </summary>
        public void ClearBubbles()
        {
            _director.Bubbles.DismissAll();
        }

        private void HandleLineStarted(DialogueDataSO dialogue, int index, DialogueLine line)
        {
            if (dialogue != _activeDialogue) return;

            EndLineBubble();
            if (line.Bubble != EmotionType.None && line.Speaker != null && _actors.TryGetValue(line.Speaker, out var speaker))
            {
                _director.Bubbles.Show(speaker.BubbleTarget, line.Bubble, float.PositiveInfinity);
                _lineBubbleActor = speaker;
            }

            if (line.Sfx != null)
            {
                PlaySfx(line.Sfx);
            }

            if (!string.IsNullOrWhiteSpace(line.Signal))
            {
                Signal(line.Signal);
            }

            _onLineStarted?.Invoke(index);
        }

        private void EndLineBubble()
        {
            if (_lineBubbleActor == null) return;
            _director.Bubbles.Dismiss(_lineBubbleActor.BubbleTarget);
            _lineBubbleActor = null;
        }
    }
}
