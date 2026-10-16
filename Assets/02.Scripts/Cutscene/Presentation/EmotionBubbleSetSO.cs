using System;
using System.Collections.Generic;
using ShotgunAndRun.UI.Dialogue;
using UnityEngine;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 감정 종류별 말풍선 스프라이트 목록입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "EmotionBubbleSet", menuName = "ShotgunAndRun/Cutscene/Emotion Bubble Set")]
    public sealed class EmotionBubbleSetSO : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public EmotionType Emotion;
            public Sprite Sprite;
        }

        [SerializeField] private List<Entry> _entries = new List<Entry>();

        public Sprite Get(EmotionType emotion)
        {
            foreach (var entry in _entries)
            {
                if (entry.Emotion == emotion)
                    return entry.Sprite;
            }

            return null;
        }

        /// <summary>
        /// 스프라이트가 비어 있는 감정 종류를 errors에 추가합니다.
        /// </summary>
        public void CollectMissing(List<string> errors)
        {
            foreach (EmotionType emotion in Enum.GetValues(typeof(EmotionType)))
            {
                if (emotion == EmotionType.None) continue;
                if (Get(emotion) == null)
                {
                    errors.Add($"말풍선 세트 '{name}'에 {emotion} 스프라이트가 없습니다.");
                }
            }
        }
    }
}
