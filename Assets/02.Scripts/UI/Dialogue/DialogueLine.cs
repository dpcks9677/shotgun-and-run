using System;
using UnityEngine;

namespace ShotgunAndRun.UI.Dialogue
{
    /// <summary>
    /// 대사 한 줄(한 페이지)의 데이터입니다. 화자를 비워두면 DialogueDataSO의 기본 화자가 말합니다.
    /// </summary>
    [Serializable]
    public struct DialogueLine
    {
        [Tooltip("이 대사의 화자 프로필. 지정하면 이름 · 초상화를 프로필에서 가져오며 아래 화자 이름 · 초상화보다 우선합니다.")]
        public CharacterProfileSO Speaker;

        [Tooltip("화자 프로필의 표정 키 (비우면 기본 초상화)")]
        public string Expression;

        [Tooltip("이 대사의 화자 이름 (화자 프로필이 없을 때만 사용, 비어있으면 기본 화자)")]
        public string SpeakerName;

        [Tooltip("이 대사의 화자 초상화 (화자 프로필이 없고 화자 이름을 지정한 경우에만 사용)")]
        public Sprite SpeakerPortrait;

        [Tooltip("출력할 대사 내용")]
        [TextArea(2, 5)]
        public string Text;

        [Header("컷씬 연출 (컷씬 중에만 사용)")]
        [Tooltip("이 대사가 나올 때 화자 머리 위에 띄울 감정 말풍선 (화자 프로필 필요)")]
        public EmotionType Bubble;

        [Tooltip("이 대사가 나올 때 재생할 효과음")]
        public AudioClip Sfx;

        [Tooltip("이 대사가 나올 때 컷씬에 보낼 신호 키")]
        public string Signal;

        public bool HasOwnSpeaker => Speaker != null || !string.IsNullOrWhiteSpace(SpeakerName);

        public DialogueLine(string text, string speakerName = null, Sprite speakerPortrait = null)
        {
            Speaker = null;
            Expression = null;
            Text = text;
            SpeakerName = speakerName;
            SpeakerPortrait = speakerPortrait;
            Bubble = EmotionType.None;
            Sfx = null;
            Signal = null;
        }
    }
}
