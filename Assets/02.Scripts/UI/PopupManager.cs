using System.Collections.Generic;
using UnityEngine;

namespace ShotgunAndRun.UI
{
    /// <summary>
    /// 열려 있는 인게임 팝업 UI를 LIFO(후입선출) 스택 형태로 관리하여
    /// ESC 키 입력 시 최상단 팝업부터 순차적으로 닫히도록 중재하는 정적 매니저입니다.
    /// </summary>
    public static class PopupManager
    {
        private static readonly List<IPopupUI> _activePopups = new List<IPopupUI>();
        private static int _lastClosedFrame = -1;

        /// <summary>
        /// 이번 프레임에 어떤 팝업이든 닫힌 적이 있는지 여부입니다.
        /// 동일 프레임에 일시정지 메뉴가 바로 열리는 현상을 방지하는 데 사용됩니다.
        /// </summary>
        public static bool WasAnyPopupClosedThisFrame => _lastClosedFrame == Time.frameCount;

        /// <summary>
        /// 현재 열려 있는 유효한 팝업이 하나라도 존재하는지 여부입니다.
        /// </summary>
        public static bool HasActivePopup
        {
            get
            {
                CleanUpDeadReferences();
                for (int i = _activePopups.Count - 1; i >= 0; i--)
                {
                    if (_activePopups[i].IsOpen)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// 팝업이 열릴 때 스택 최상단에 등록합니다.
        /// </summary>
        public static void Push(IPopupUI popup)
        {
            if (popup == null)
            {
                return;
            }

            CleanUpDeadReferences();
            _activePopups.Remove(popup);
            _activePopups.Add(popup);
        }

        /// <summary>
        /// 팝업이 닫힐 때 스택에서 제거합니다.
        /// </summary>
        public static void Pop(IPopupUI popup)
        {
            if (popup == null)
            {
                return;
            }

            if (_activePopups.Remove(popup))
            {
                _lastClosedFrame = Time.frameCount;
            }

            CleanUpDeadReferences();
        }

        /// <summary>
        /// 팝업이 자체적으로 닫혔음을 알리고 해당 프레임의 ESC 입력을 소비하도록 등록합니다.
        /// </summary>
        public static void NotifyClosed(IPopupUI popup)
        {
            Pop(popup);
            _lastClosedFrame = Time.frameCount;
        }

        /// <summary>
        /// 현재 열려 있는 팝업 중 가장 최근에 열린 최상단 팝업 1개만 닫습니다.
        /// </summary>
        /// <returns>팝업이 닫혔다면 true, 열려 있는 팝업이 없었다면 false</returns>
        public static bool TryCloseTopPopup()
        {
            CleanUpDeadReferences();

            for (int i = _activePopups.Count - 1; i >= 0; i--)
            {
                IPopupUI popup = _activePopups[i];
                if (popup != null && popup.IsOpen)
                {
                    _activePopups.RemoveAt(i);
                    _lastClosedFrame = Time.frameCount;
                    popup.Close();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 씬 전환 시 모든 팝업 스택 참조를 안전하게 비웁니다.
        /// </summary>
        public static void Clear()
        {
            _activePopups.Clear();
            _lastClosedFrame = -1;
        }

        private static void CleanUpDeadReferences()
        {
            _activePopups.RemoveAll(p => p == null || (p is Object obj && obj == null));
        }
    }
}
