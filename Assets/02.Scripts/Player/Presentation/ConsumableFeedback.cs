using UnityEngine;

namespace Consumable
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ConsumableEffectService))]
    [RequireComponent(typeof(ConsumableAudioFeedback))]
    [RequireComponent(typeof(ConsumableVFXFeedback))]
    public class ConsumableFeedback : MonoBehaviour
    {
        [SerializeField] private ConsumableEffectService _effectService;
        [SerializeField] private ConsumableAudioFeedback _audioFeedback;
        [SerializeField] private ConsumableVFXFeedback _vfxFeedback;

        private void Awake()
        {
            _effectService = GetComponent<ConsumableEffectService>();
            _audioFeedback = GetComponent<ConsumableAudioFeedback>();
            _vfxFeedback = GetComponent<ConsumableVFXFeedback>();
        }

        public void OnEnable()
        {
            _effectService.UsedConsumable += HandleConsumableEffect;
        }

        public void OnDisable()
        {
            _effectService.UsedConsumable -= HandleConsumableEffect;
        }

        public void HandleConsumableEffect(ConsumableData data)
        {
            ConsumableType type = data.ConsumableType;

            switch (type)
            {
                case ConsumableType.SmallKit:
                case ConsumableType.LargeKit:
                    _audioFeedback.PlayHeal();
                    _vfxFeedback.PlayHeal();
                    break;
                case ConsumableType.FragmentGrenade:
                case ConsumableType.FlashBang:
                    _audioFeedback.PlayThrow();
                    break;
                case ConsumableType.Adrenaline:
                    _audioFeedback.PlayAdrenaline();
                    StartCoroutine(_vfxFeedback.PlayAdrenaline());
                    break;
                case ConsumableType.Armor:
                    _audioFeedback.PlayArmor();
                    break;
            }
        }
    }
}
