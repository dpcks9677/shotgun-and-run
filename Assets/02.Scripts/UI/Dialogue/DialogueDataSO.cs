using System.Collections.Generic;
using UnityEngine;

namespace ShotgunAndRun.UI.Dialogue
{
    /// <summary>
    /// 대화 및 튜토리얼 데이터를 정의하는 ScriptableObject입니다.
    /// 에디터 프로젝트 창에서 우클릭하여 손쉽게 대화 데이터를 생성할 수 있습니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewDialogueData", menuName = "ShotgunAndRun/Dialogue/Dialogue Data")]
    public class DialogueDataSO : ScriptableObject
    {
        [Header("기본 화자 (Default Speaker)")]
        [Tooltip("대사에 화자를 따로 지정하지 않았을 때의 화자 이름 (비어있으면 이름 표시 패널이 자동으로 숨겨집니다)")]
        [SerializeField] private string _speakerName;

        [Tooltip("기본 화자 초상화 스프라이트 (없으면 숨겨집니다)")]
        [SerializeField] private Sprite _speakerPortrait;

        [Header("튜토리얼 / 보조 가이드 이미지")]
        [Tooltip("조작법 다이어그램, 튜토리얼 힌트 등 대화창 우측이나 중앙에 띄울 보조 이미지 (선택 사항)")]
        [SerializeField] private Sprite _guideImage;

        [Header("대사 내용 (Lines)")]
        [Tooltip("순차적으로 출력할 대사 목록입니다. 한 줄씩 넘기면서 읽습니다. 화자가 바뀌는 대사만 화자 이름을 채우세요.")]
        [SerializeField] private List<DialogueLine> _lines = new List<DialogueLine>();

        [Header("선택지 (Choices)")]
        [Tooltip("마지막 대사 출력 후 표시될 선택지 버튼 목록입니다. 비어있으면 기본 [확인/닫기] 버튼이 제공됩니다.")]
        [SerializeField] private List<DialogueChoice> _choices = new List<DialogueChoice>();

        [Header("게임 설정 (Settings)")]
        [Tooltip("대화 창이 열려있는 동안 게임 시간(Time.timeScale)을 멈출지 여부입니다. (튜토리얼 조작 안내 등에 유용)")]
        [SerializeField] private bool _pauseGame = false;

        // 접근용 프로퍼티
        public string SpeakerName => _speakerName;
        public Sprite SpeakerPortrait => _speakerPortrait;
        public Sprite GuideImage => _guideImage;
        public IReadOnlyList<DialogueLine> Lines => _lines;
        public IReadOnlyList<DialogueChoice> Choices => _choices;
        public bool PauseGame => _pauseGame;

        /// <summary>
        /// 대사의 실제 화자 이름을 반환합니다. 화자 프로필이 있으면 프로필 이름, 대사에 화자가 없으면 기본 화자입니다.
        /// </summary>
        public string GetSpeakerName(DialogueLine line)
        {
            if (line.Speaker != null)
                return line.Speaker.DisplayName;

            return line.HasOwnSpeaker ? line.SpeakerName : _speakerName;
        }

        /// <summary>
        /// 대사의 실제 화자 초상화를 반환합니다. 화자 프로필이 있으면 대사의 표정에 맞는 초상화입니다.
        /// 다른 화자를 지정한 대사는 기본 화자의 초상화를 이어받지 않습니다.
        /// </summary>
        public Sprite GetSpeakerPortrait(DialogueLine line)
        {
            if (line.Speaker != null)
                return line.Speaker.GetPortrait(line.Expression);

            return line.HasOwnSpeaker ? line.SpeakerPortrait : _speakerPortrait;
        }

        /// <summary>
        /// 런타임 데이터 초기화 편의용 생성자 (SO가 아닌 동적 요청 생성용)
        /// </summary>
        public static DialogueDataSO CreateRuntimeInstance(
            string speakerName,
            Sprite speakerPortrait,
            Sprite guideImage,
            IEnumerable<string> sentences,
            IEnumerable<DialogueChoice> choices,
            bool pauseGame = false)
        {
            var instance = CreateInstance<DialogueDataSO>();
            instance._speakerName = speakerName;
            instance._speakerPortrait = speakerPortrait;
            instance._guideImage = guideImage;

            if (sentences != null)
            {
                foreach (var sentence in sentences)
                    instance._lines.Add(new DialogueLine(sentence));
            }

            if (choices != null)
                instance._choices.AddRange(choices);

            instance._pauseGame = pauseGame;
            return instance;
        }
    }
}
