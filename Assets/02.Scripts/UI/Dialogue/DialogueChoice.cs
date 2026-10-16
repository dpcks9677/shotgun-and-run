using System;
using UnityEngine;

namespace ShotgunAndRun.UI.Dialogue
{
    /// <summary>
    /// 대화 종료 시 플레이어가 선택할 수 있는 선택지 데이터입니다.
    /// </summary>
    [Serializable]
    public struct DialogueChoice
    {
        [Tooltip("버튼에 표시될 텍스트 (예: '수락', '거절', '강화하기', '다음에')")]
        public string ChoiceText;

        [Tooltip("선택 시 콜백으로 전달될 고유 식별자 ID (예: 'CONFIRM', 'CANCEL', 'ACCEPT')")]
        public string ChoiceId;

        public DialogueChoice(string choiceText, string choiceId)
        {
            ChoiceText = choiceText;
            ChoiceId = choiceId;
        }
    }
}
