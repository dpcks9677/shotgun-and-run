using ShotgunAndRun.UI;
using UnityEngine;

namespace InventorySystem
{
    /// <summary>
    /// 상자가 열리는 동안(길게 누르기) 상자 머리 위의 행동 진행 막대(ActionProgressBar)를 갱신하고 표시하는 UI 컴포넌트
    /// </summary>
    public class ChestOpenSliderUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LootChest _chest;

        [SerializeField] private ActionProgressBar _progressBar;

        private void Awake()
        {
            _chest ??= GetComponentInParent<LootChest>();
            if (_chest == null)
            {
                _chest = GetComponent<LootChest>();
            }

            _progressBar ??= GetComponentInChildren<ActionProgressBar>(true);
        }

        private void OnEnable()
        {
            if (_chest == null) return;

            _chest.OnOpenStarted += HandleOpenStarted;
            _chest.OnOpenProgressChanged += HandleProgressChanged;
            _chest.OnOpenCancelled += HandleOpenCancelled;
            _chest.OnChestOpened += HandleChestOpened;
        }

        private void OnDisable()
        {
            if (_chest != null)
            {
                _chest.OnOpenStarted -= HandleOpenStarted;
                _chest.OnOpenProgressChanged -= HandleProgressChanged;
                _chest.OnOpenCancelled -= HandleOpenCancelled;
                _chest.OnChestOpened -= HandleChestOpened;
            }

            HideProgressBar();
        }

        private void HandleOpenStarted(float duration)
        {
            if (_chest != null && _chest.IsOpened) return;

            _progressBar?.SetProgress(this, 0f);
        }

        private void HandleProgressChanged(float progress)
        {
            if (_chest != null && _chest.IsOpened) return;

            _progressBar?.SetProgress(this, progress);
        }

        private void HandleOpenCancelled()
        {
            _progressBar?.Cancel(this);
        }

        private void HandleChestOpened(LootChest chest)
        {
            HideProgressBar();
        }

        private void HideProgressBar()
        {
            _progressBar?.Hide(this);
        }
    }
}
