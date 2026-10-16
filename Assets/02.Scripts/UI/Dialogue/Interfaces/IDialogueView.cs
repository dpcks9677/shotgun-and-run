using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShotgunAndRun.UI.Dialogue
{
    /// <summary>
    /// 대화창 화면 표출(View)에 대한 계약 인터페이스 (DIP, ISP)
    /// </summary>
    public interface IDialogueView
    {
        /// <summary>
        /// 대화창이 현재 열려 있는지 여부
        /// </summary>
        public bool IsOpen
        {
            get;
        }

        /// <summary>
        /// 텍스트 애니메이션(타이핑 등)이 진행 중인지 여부
        /// </summary>
        public bool IsAnimatingText
        {
            get;
        }

        /// <summary>
        /// 선택지가 화면에 표시되어 입력을 기다리는 중인지 여부
        /// </summary>
        public bool IsShowingChoices
        {
            get;
        }

        /// <summary>
        /// 대화창을 열고 가이드 이미지를 설정합니다.
        /// </summary>
        public void Open(Sprite guideImage);

        /// <summary>
        /// 화자 이름과 초상화를 설정합니다. 대사마다 화자가 바뀔 수 있습니다.
        /// </summary>
        public void SetSpeaker(string speakerName, Sprite speakerPortrait);

        /// <summary>
        /// 대화창을 닫습니다.
        /// </summary>
        public void Close();

        /// <summary>
        /// 대사 한 줄을 화면에 표시합니다.
        /// </summary>
        public void DisplaySentence(string sentence, bool isLastSentence, Action onComplete);

        /// <summary>
        /// 진행 중인 텍스트 애니메이션을 즉시 완료시킵니다.
        /// </summary>
        public void CompleteSentenceImmediately(bool isLastSentence, Action onComplete);

        /// <summary>
        /// 선택지 버튼들을 표시합니다.
        /// </summary>
        public void ShowChoices(IReadOnlyList<DialogueChoice> choices, Action<string> onChoiceSelected);

        /// <summary>
        /// 선택된 선택지를 위(-1) 또는 아래(1)로 이동합니다.
        /// </summary>
        public void MoveChoiceSelection(int direction);

        /// <summary>
        /// 현재 선택된 선택지를 확정합니다.
        /// </summary>
        public void SubmitSelectedChoice();

        /// <summary>
        /// 다음 대사 표시 인디케이터(▼ 등)의 활성화 여부를 설정합니다.
        /// </summary>
        public void SetNextIndicator(bool active);
    }
}
