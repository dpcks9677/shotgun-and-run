namespace ShotgunAndRun.UI.Dialogue
{
    /// <summary>
    /// 인물 머리 위에 띄우는 감정 말풍선의 종류입니다. 대사 데이터(DialogueLine.Bubble)와 컷씬 연출이 함께 사용합니다.
    /// </summary>
    public enum EmotionType
    {
        None,
        Alert,     // !
        Question,  // ?
        Shock,     // !?
        Silence,   // ...
        Sweat,     // 땀
        Joy,       // 기쁨
        Idea,      // 전구 / 깨달음
        Anger,     // 분노
        Heart,     // 하트
        Sad        // 슬픔
    }
}
