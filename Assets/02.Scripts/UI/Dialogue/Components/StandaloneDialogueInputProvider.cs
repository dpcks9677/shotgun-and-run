using UnityEngine;

namespace ShotgunAndRun.UI.Dialogue
{
    /// <summary>
    /// 마우스 클릭 및 키보드 단축키를 감지하는 독립 입력 공급 컴포넌트 (SRP, IDialogueInputProvider 구현)
    /// </summary>
    public class StandaloneDialogueInputProvider : MonoBehaviour, IDialogueInputProvider
    {
        [Tooltip("대사 넘기기, 즉시 완성 및 선택지 확정에 사용할 단축키들")]
        [SerializeField] private KeyCode[] _advanceKeys = new KeyCode[] { KeyCode.Space, KeyCode.Return, KeyCode.E };

        [Tooltip("마우스 좌클릭으로도 넘길 수 있는지 여부")]
        [SerializeField] private bool _allowMouseLeftClick = true;

        [Tooltip("선택지를 위로 이동하는 키들")]
        [SerializeField] private KeyCode[] _previousChoiceKeys = new KeyCode[] { KeyCode.UpArrow, KeyCode.W };

        [Tooltip("선택지를 아래로 이동하는 키들")]
        [SerializeField] private KeyCode[] _nextChoiceKeys = new KeyCode[] { KeyCode.DownArrow, KeyCode.S };

        public bool IsAdvanceTriggered()
        {
            if (_allowMouseLeftClick && Input.GetMouseButtonDown(0))
                return true;

            return IsAnyKeyDown(_advanceKeys);
        }

        public bool IsSubmitTriggered()
        {
            return IsAnyKeyDown(_advanceKeys);
        }

        public int GetChoiceNavigation()
        {
            if (IsAnyKeyDown(_previousChoiceKeys))
                return -1;

            if (IsAnyKeyDown(_nextChoiceKeys))
                return 1;

            return 0;
        }

        private static bool IsAnyKeyDown(KeyCode[] keys)
        {
            if (keys == null)
                return false;

            for (int i = 0; i < keys.Length; i++)
            {
                if (Input.GetKeyDown(keys[i]))
                    return true;
            }

            return false;
        }
    }
}
