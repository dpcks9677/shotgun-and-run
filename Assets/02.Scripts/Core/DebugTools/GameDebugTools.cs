#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShotgunAndRun.Core
{
    // 에디터 · 개발 빌드 전용 디버그 도구입니다. GameManager가 자동으로 붙입니다.
    // - 화면 좌상단에 GameManager 상태와 런 정보 표시 (F11로 켜고 끔)
    // - F9: 진행 중인 런을 즉시 탈출 성공으로 종료
    [DisallowMultipleComponent]
    public sealed class GameDebugTools : MonoBehaviour
    {
        private const float Width = 260f;

        private readonly StringBuilder _builder = new StringBuilder();
        private GameManager _gameManager;
        private GUIStyle _style;
        private bool _visible = true;

        private void Awake()
        {
            _gameManager = GetComponent<GameManager>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || _gameManager == null)
            {
                return;
            }

            if (keyboard.f9Key.wasPressedThisFrame)
            {
                _gameManager.EndRun(RunOutcome.Extracted);
            }

            if (keyboard.f11Key.wasPressedThisFrame)
            {
                _visible = !_visible;
            }
        }

        private void OnGUI()
        {
            if (!_visible || _gameManager == null)
            {
                return;
            }

            _style ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 13,
                richText = true
            };

            string text = BuildText();
            float height = _style.CalcHeight(new GUIContent(text), Width);
            GUI.Box(new Rect(8f, 8f, Width, height), text, _style);
        }

        private string BuildText()
        {
            _builder.Clear();
            _builder.AppendLine($"<b>State</b>  {_gameManager.State}");

            RunSession run = _gameManager.CurrentRun;
            if (run != null)
            {
                _builder.AppendLine($"Time  {FormatTime(run.Elapsed)}  (left {FormatTime(run.RemainingTime)})");
                _builder.AppendLine(run.IsExtractionUnlocked
                    ? "Extraction  <color=#6f6>OPEN</color>"
                    : $"Extraction  locked ({FormatTime(run.TimeUntilExtraction)})");

                if (run.ExtractionHoldTime > 0f)
                {
                    _builder.AppendLine($"Holding  {run.ExtractionHoldTime:0.0} / {run.RequiredExtractionHoldTime:0.0}s");
                }

                if (!_gameManager.IsPlayerBound)
                {
                    _builder.AppendLine("<color=#fc6>No player (death not tracked)</color>");
                }

                if (run.IsOverTime)
                {
                    _builder.AppendLine("<color=#f66>OVER TIME</color>");
                }

                _builder.AppendLine($"Kills  {run.NormalKills} (+{run.EliteKills} elite)  Zones  {run.ClearedZones.Count}");
                _builder.AppendLine($"Run XP  {run.RunXp}");
            }

            RunResult result = _gameManager.LastRunResult;
            if (result != null && _gameManager.State == GameState.Result)
            {
                _builder.AppendLine($"Result  {result.Outcome}  +{result.FinalXp} XP");
            }

            ProgressData progress = _gameManager.Progress;
            if (progress != null)
            {
                _builder.AppendLine($"Player  Lv{progress.PlayerLevel}  XP {progress.PlayerXp}");
            }

            _builder.Append("<size=11>F9 extract  F11 hide</size>");
            return _builder.ToString();
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.CeilToInt(seconds);
            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}
#endif
