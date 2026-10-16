using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShotgunAndRun.UI.Dialogue
{
    /// <summary>
    /// 인물 한 명의 정보입니다. 대사의 화자(이름 · 초상화)와 컷씬 등장인물 식별에 함께 사용합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "CHR_NewCharacter", menuName = "ShotgunAndRun/Dialogue/Character Profile")]
    public class CharacterProfileSO : ScriptableObject
    {
        [Serializable]
        public struct ExpressionPortrait
        {
            [Tooltip("표정 키 (예: Default, Serious, Surprised)")]
            public string Key;
            public Sprite Portrait;
        }

        [Tooltip("대화창에 표시할 이름")]
        [SerializeField] private string _displayName;

        [Tooltip("표정을 지정하지 않은 대사에 쓰는 초상화 (없으면 초상화 영역이 숨겨집니다)")]
        [SerializeField] private Sprite _defaultPortrait;

        [Tooltip("표정별 초상화. 대사의 Expression 값으로 고릅니다.")]
        [SerializeField] private List<ExpressionPortrait> _expressions = new List<ExpressionPortrait>();

        public string DisplayName => _displayName;

        /// <summary>
        /// 표정 키에 맞는 초상화를 반환합니다. 키가 비어 있으면 기본 초상화, 목록에 없는 키면 경고 후 기본 초상화입니다.
        /// </summary>
        public Sprite GetPortrait(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return _defaultPortrait;

            foreach (var entry in _expressions)
            {
                if (entry.Key == expression)
                    return entry.Portrait;
            }

            Debug.LogWarning($"[CharacterProfileSO] '{name}'에 '{expression}' 표정이 없어 기본 초상화를 사용합니다.", this);
            return _defaultPortrait;
        }
    }
}
