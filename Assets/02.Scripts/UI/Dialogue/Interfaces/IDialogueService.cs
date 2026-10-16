using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShotgunAndRun.UI.Dialogue
{
    /// <summary>
    /// 외부 시스템에서 대화 기능을 요청하고 제어하기 위한 서비스 인터페이스 (DIP, ISP)
    /// </summary>
    public interface IDialogueService
    {
        /// <summary>
        /// 현재 대화창이 활성화되어 있는지 여부
        /// </summary>
        public bool IsDialogueActive
        {
            get;
        }

        /// <summary>
        /// 대사 한 줄이 화면에 표시되기 시작할 때 발생합니다. (대화 데이터, 대사 인덱스, 대사)
        /// 컷씬이 대사별 말풍선 · 효과음 · 신호를 처리할 때 사용합니다.
        /// </summary>
        public event Action<DialogueDataSO, int, DialogueLine> LineStarted;

        /// <summary>
        /// 대화 데이터(ScriptableObject)를 기반으로 대화를 시작합니다.
        /// </summary>
        public void StartDialogue(
            DialogueDataSO data,
            Action<string> onChoiceSelected = null,
            Action onDialogueComplete = null);

        /// <summary>
        /// 확인/취소 팝업을 표시합니다.
        /// </summary>
        public void ShowConfirm(
            string message,
            Action onConfirm,
            Action onCancel = null,
            string confirmText = "확인",
            string cancelText = "취소",
            string speakerName = "");

        /// <summary>
        /// 단순 안내 팝업을 표시합니다.
        /// </summary>
        public void ShowAlert(
            string message,
            Action onComplete = null,
            string confirmText = "확인",
            string speakerName = "안내");

        /// <summary>
        /// 튜토리얼 / 조작 가이드 창을 표시합니다.
        /// </summary>
        public void ShowTutorial(
            IEnumerable<string> sentences,
            Sprite guideImage = null,
            Action onComplete = null,
            string speakerName = "튜토리얼",
            bool pauseGame = true);

        /// <summary>
        /// 대화창을 강제로 닫습니다.
        /// </summary>
        public void CloseDialogue();
    }
}
