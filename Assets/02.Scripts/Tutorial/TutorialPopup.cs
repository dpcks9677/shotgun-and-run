using UnityEngine;
using UnityEngine.UI;

namespace ShotgunAndRun.Tutorial
{
    /// <summary>
    /// (프로토타입) 튜토리얼이 끝날 때 화면을 검게 덮는 Blackout을 제공합니다. (조작 안내는 TutorialWallGuide가 맡습니다)
    /// </summary>
    public sealed class TutorialPopup : MonoBehaviour
    {
        [SerializeField] private Image _blackout;

        private void Awake()
        {
            if (_blackout != null) _blackout.gameObject.SetActive(false);
        }

        /// <summary>
        /// 화면 전체를 검게 덮거나 걷습니다. 컷씬의 페이드는 컷씬이 끝나면 풀리므로, 끝난 뒤에도 검게 유지할 때 씁니다.
        /// </summary>
        public void SetBlackout(bool on)
        {
            if (_blackout != null) _blackout.gameObject.SetActive(on);
        }
    }
}
