using System;
using System.Collections.Generic;

namespace ShotgunAndRun.UI.Dialogue
{
    /// <summary>
    /// 단일 대화의 순차 진행 상태와 선택지 데이터를 관리하는 순수 도메인 세션 클래스 (SRP, POCO)
    /// 유니티 엔진과 결합되지 않아 단위 테스트가 가능합니다.
    /// </summary>
    public class DialogueSession
    {
        private readonly IReadOnlyList<DialogueLine> _lines;
        private readonly IReadOnlyList<DialogueChoice> _choices;
        private int _currentIndex;

        public int CurrentIndex => _currentIndex;
        public int TotalLines => _lines?.Count ?? 0;
        public bool HasLines => TotalLines > 0;
        public bool IsLastLine => _currentIndex >= TotalLines - 1;
        public bool HasRemainingLines => _currentIndex < TotalLines - 1;
        public bool HasChoices => _choices != null && _choices.Count > 0;
        public IReadOnlyList<DialogueChoice> Choices => _choices;

        public DialogueSession(IReadOnlyList<DialogueLine> lines, IReadOnlyList<DialogueChoice> choices)
        {
            _lines = lines ?? Array.Empty<DialogueLine>();
            _choices = choices ?? Array.Empty<DialogueChoice>();
            _currentIndex = 0;
        }

        /// <summary>
        /// 현재 인덱스의 대사를 가져옵니다.
        /// </summary>
        public DialogueLine GetCurrentLine()
        {
            if (!HasLines || _currentIndex < 0 || _currentIndex >= _lines.Count)
                return default;

            return _lines[_currentIndex];
        }

        /// <summary>
        /// 다음 대사로 인덱스를 전진시킵니다.
        /// </summary>
        /// <returns>전진에 성공했으면 true, 이미 마지막이어서 전진하지 못했으면 false</returns>
        public bool Advance()
        {
            if (HasRemainingLines)
            {
                _currentIndex++;
                return true;
            }

            return false;
        }
    }
}
