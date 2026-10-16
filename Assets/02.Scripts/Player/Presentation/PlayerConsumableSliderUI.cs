using Consumable;
using ShotgunAndRun.UI;
using UnityEngine;

namespace ShotgunAndRun.Player
{
    public class PlayerConsumableSliderUI : MonoBehaviour
    {
        [SerializeField] private ConsumableEffectService _controller;
        [SerializeField] private ActionProgressBar _progressBar;

        private void Awake()
        {
            _controller = GetComponentInParent<Player>().GetComponentInChildren<ConsumableEffectService>();

            if (_progressBar == null)
            {
                _progressBar ??= GetComponentInParent<Player>().GetComponentInChildren<ActionProgressBar>();
            }

            if (_controller == null)
                Debug.LogError("ConsumableEffectService 찾을 수 없습니다.");

            if (_progressBar == null)
                Debug.LogError("ActionProgressBar를 찾을 수 없습니다.");
        }

        private void OnEnable()
        {
            if (_controller == null)
            {
                Debug.LogError("ConsumableEffectService 초기화되지 않았습니다.");
                return;
            }

            _controller.StartUseConsumable += HandleUseConsumableStart;
            _controller.UsedConsumable += HandleUseConsumableEnded;
            _controller.CanceledConsumable += HandleConsumableCanceled;
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.StartUseConsumable -= HandleUseConsumableStart;
                _controller.UsedConsumable -= HandleUseConsumableEnded;
                _controller.CanceledConsumable -= HandleConsumableCanceled;
            }

            _progressBar?.Hide(this);
        }

        public void HandleUseConsumableStart(ConsumableType type, float duration)
        {
            if (!(type == ConsumableType.LargeKit || type == ConsumableType.SmallKit))
            {
                return;
            }

            _progressBar?.SetProgress(this, 0f);

            if (_controller == null || _progressBar == null)
            {
                return;
            }

            _progressBar.StartFill(this, duration);
        }

        public void HandleUseConsumableEnded(ConsumableData data)
        {
            _progressBar?.Hide(this);
        }

        public void HandleConsumableCanceled()
        {
            _progressBar?.Cancel(this);
        }
    }
}
