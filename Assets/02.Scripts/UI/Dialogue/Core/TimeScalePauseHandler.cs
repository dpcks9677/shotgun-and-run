using UnityEngine;

namespace ShotgunAndRun.UI.Dialogue
{
    /// <summary>
    /// 게임 시간(Time.timeScale)의 일시정지 및 복원을 전담하는 핸들러 (SRP, ITimePauseHandler 구현)
    /// </summary>
    public class TimeScalePauseHandler : ITimePauseHandler
    {
        private bool _isPaused;
        private float _savedTimeScale = 1f;

        public bool IsPaused => _isPaused;

        public void Pause()
        {
            if (_isPaused) return;

            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            _isPaused = true;
        }

        public void Resume()
        {
            if (!_isPaused) return;

            Time.timeScale = _savedTimeScale;
            _isPaused = false;
        }
    }
}
