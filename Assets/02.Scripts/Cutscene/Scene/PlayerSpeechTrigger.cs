using UnityEngine;
using PlayerComponent = ShotgunAndRun.Player.Player;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 플레이어 머리 위에 대사 말풍선을 띄웁니다. UnityEvent(예: RunEntranceZone의 잠김 안내)에서 Show()를 호출해 사용합니다.
    /// </summary>
    public sealed class PlayerSpeechTrigger : MonoBehaviour
    {
        [Tooltip("말풍선에 표시할 대사")]
        [TextArea]
        [SerializeField] private string _text;

        [Tooltip("말풍선 표시 시간(초)")]
        [Min(0.5f)]
        [SerializeField] private float _seconds = 2.5f;

        /// <summary>플레이어 머리 위에 대사 말풍선을 띄웁니다.</summary>
        public void Show()
        {
            var player = PlayerComponent.PlayerInstance;
            if (player == null) return;

            var director = CutsceneDirector.Current;
            if (director == null)
            {
                Debug.LogWarning("PlayerSpeechTrigger: CutsceneDirector is not available.", this);
                return;
            }

            director.ShowSpeech(player.transform, _text, _seconds);
        }
    }
}
