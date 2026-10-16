using UnityEngine;

namespace Consumable
{
    public class ConsumableAudioFeedback : MonoBehaviour
    {
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _heal;
        [SerializeField] private AudioClip _adrenaline;
        [SerializeField] private AudioClip _armor;
        [SerializeField] private AudioClip _throwing;

        public void Awake()
        {
            _audioSource = GetComponent<AudioSource>();

            if (_audioSource == null)
            {
                Debug.LogError("No audio source found");
            }
        }

        public void PlayHeal()
        {
            _audioSource.PlayOneShot(_heal);
        }

        public void PlayAdrenaline()
        {
            _audioSource.PlayOneShot(_adrenaline);
        }

        public void PlayArmor()
        {
            _audioSource.PlayOneShot(_armor);
        }

        public void PlayThrow()
        {
            _audioSource.PlayOneShot(_throwing);
        }
    }
}
