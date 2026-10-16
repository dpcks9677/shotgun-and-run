using System;
using System.Collections;
using ShotgunAndRun.Player;
using UnityEngine;

namespace Consumable
{
    public class ConsumableEffectService : MonoBehaviour
    {
        [SerializeField] private ConsumableCatalog _catalog;
        [SerializeField] private PlayerHealth _playerHealth;
        [SerializeField] private PlayerMovement _playerMovement;
        [SerializeField] private PlayerDash _playerDash;
        [SerializeField] private ThrowGrenade _throwGrenade;

        private Coroutine _activeConsumableCoroutine;
        private bool _isUsingHealKit;
        private bool _isConsumableUsable = true;

        public bool IsUsingHealKit => _isUsingHealKit;

        public event Action<ConsumableType, float> StartUseConsumable;
        public event Action<ConsumableData> UsedConsumable;
        public event Action CanceledConsumable;

        public void Awake()
        {
            _isConsumableUsable = true;
            _playerHealth = GetComponentInParent<PlayerHealth>();
            _playerMovement = GetComponentInParent<PlayerMovement>();
            _playerDash = GetComponentInParent<PlayerDash>();
            _throwGrenade = GetComponent<ThrowGrenade>();

            _playerDash.DashStarted += CancelActiveConsumable;
        }

        private void OnDestroy()
        {
            _playerDash.DashStarted -= CancelActiveConsumable;
        }

        public void UseConsumable(ConsumableData selectedConsumable)
        {
            if (_isUsingHealKit)
            {
                // 사용 중 재입력은 취소가 아니라 무시합니다.
                // 취소는 마우스 해제, 이동, 대시 이벤트에서만 처리합니다.
                return;
            }

            if (!_isConsumableUsable)
            {
                return;
            }

            ConsumableData data = selectedConsumable;

            ConsumableType type = data.ConsumableType;

            switch (type)
            {
                case ConsumableType.SmallKit:
                case ConsumableType.LargeKit:
                    StartHealKit(data);
                    break;
                case ConsumableType.FragmentGrenade:
                case ConsumableType.FlashBang:
                    _throwGrenade.TryThrow(type);
                    StartUseConsumable?.Invoke(type, data.ConsumableEffectData.duration);
                    UsedConsumable?.Invoke(data);
                    break;
                case ConsumableType.Adrenaline:
                    StartCoroutine(ApplyAdrenaline(data));
                    break;
                case ConsumableType.Armor:
                    ApplyArmor(data);
                    break;
            }
        }

        private IEnumerator ApplyAdrenaline(ConsumableData data)
        {
            ConsumableType type = data.ConsumableType;
            float duration = data.ConsumableEffectData.duration;
            float value = data.ConsumableEffectData.value;

            if (_playerMovement.Stats.FindMoveSpeedMultiplier("Adrenaline"))
            {
                Debug.Log("아직 아드레날린이 적용 중입니다.");
                yield break;
            }


            StartUseConsumable?.Invoke(type, duration);
            UsedConsumable?.Invoke(data);

            _playerMovement.Stats.SetMoveSpeedMultiplier("Adrenaline", value);

            Debug.Log("아드레날린 적용!");

            yield return new WaitForSeconds(duration);

            _playerMovement.Stats.RemoveMoveSpeedMultiplier("Adrenaline");

            Debug.Log("아드레날린 종료!");

            Debug.Log($"{type.ToString()} 사용!");
            yield return null;
        }

        private void StartHealKit(ConsumableData data)
        {
            ConsumableType type = data.ConsumableType;
            float duration = data.ConsumableEffectData.duration;
            float value = data.ConsumableEffectData.value;

            if (_activeConsumableCoroutine != null)
            {
                return;
            }

            if (_playerHealth.CurrentHealth == _playerHealth.MaxHealth)
            {
                Debug.LogError("현재 체력이 최대이므로 회복 아이템을 사용할 수 없습니다.");
                return;
            }

            _isUsingHealKit = true;
            StartUseConsumable?.Invoke(type, duration);

            _activeConsumableCoroutine =
                StartCoroutine(ApplyHealKit(data));
        }

        private IEnumerator ApplyHealKit(ConsumableData data)
        {
            ConsumableType type = data.ConsumableType;
            float duration = data.ConsumableEffectData.duration;
            float value = data.ConsumableEffectData.value;

            yield return new WaitForSeconds(duration);

            if (!_isUsingHealKit)
            {
                yield break;
            }

            _playerHealth.Heal((int)value);

            _activeConsumableCoroutine = null;
            _isUsingHealKit = false;

            Debug.Log($"{type.ToString()} 사용!");
            UsedConsumable?.Invoke(data);
        }

        private void ApplyArmor(ConsumableData data)
        {
            ConsumableType type = data.ConsumableType;
            float duration = data.ConsumableEffectData.duration;
            float value = data.ConsumableEffectData.value;

            if (_playerHealth.HasArmor)
            {
                Debug.Log("이미 아머 소모품을 사용하여 적용이 된 상태입니다.");
                return;
            }

            StartUseConsumable?.Invoke(type, duration);

            _playerMovement.Stats.SetMoveSpeedMultiplier("Armor", value);

            Debug.Log($"{type.ToString()} 사용!");
            UsedConsumable?.Invoke(data);
        }

        public void CancelActiveConsumable()
        {
            if (!_isUsingHealKit)
            {
                return;
            }

            if (_activeConsumableCoroutine != null)
            {
                StopCoroutine(_activeConsumableCoroutine);
                _activeConsumableCoroutine = null;
            }

            _isUsingHealKit = false;
            CanceledConsumable?.Invoke();
        }

        private void CancelActiveConsumable(Vector2 obj)
        {
            CancelActiveConsumable();
        }

        public void SetConsumableUsable(bool isCounsumableUsable)
        {
            _isConsumableUsable = isCounsumableUsable;
        }
    }
}
