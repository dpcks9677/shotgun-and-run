using System.Collections;
using System.Collections.Generic;
using ShotgunAndRun.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PlayerCharacter = ShotgunAndRun.Player.Player;

namespace ShotgunAndRun.UI
{
    /// <summary>
    /// 플레이어의 체력(하트) 수량 및 피격/회복 연출을 화면 HUD에 표시하는 UI 컴포넌트
    /// HorizontalLayoutGroup과의 충돌 없이 타격감을 살리기 위해 회전 진동(Rotation Shake)과 색상 플래시로 연출합니다.
    /// </summary>
    public class PlayerHealthUI : MonoBehaviour
    {
        [Header("Target Health")]
        [Tooltip("비워두면 씬에서 PlayerHealth를 자동으로 찾아 연결합니다.")]
        [SerializeField]
        private PlayerHealth _playerHealth;

        [Header("UI Layout Elements")]
        [Tooltip("하트 아이콘들이 배치될 부모 컨테이너 (Horizontal Layout Group 추천)")]
        [SerializeField]
        private Transform _heartContainer;

        [Tooltip("하트 1칸을 나타낼 Image 프리팹 (또는 씬 내 프로토타입 Image)")]
        [SerializeField]
        private Image _heartPrefab;

        [Tooltip("선택 사항: '5 / 5' 형태로 텍스트를 띄우고 싶을 때 연결하는 TMP 텍스트")]
        [SerializeField]
        private TextMeshProUGUI _healthText;

        [Header("Heart Sprites")]
        [Tooltip("가득 찬 하트 스프라이트 (비워두면 프리팹 기본 스프라이트 사용)")]
        [SerializeField]
        private Sprite _fullHeartSprite;

        [Tooltip("체력이 깎였을 때 표시할 빈 하트 스프라이트")]
        [SerializeField]
        private Sprite _emptyHeartSprite;

        [Tooltip("아머(방패) 활성화 시 마지막 하트 슬롯에 표시할 방패 스프라이트")]
        [SerializeField]
        private Sprite _armorHeartSprite;

        [Header("Armor Animation (아머 획득/파괴 연출)")]
        [Tooltip("아머 획득 시 팝(Pop) 애니메이션 연출 사용 여부")]
        [SerializeField]
        private bool _enableArmorGainPop = true;

        [Tooltip("아머 파괴 시 피격 흔들림(Damage Shake) 연출 사용 여부")]
        [SerializeField]
        private bool _enableArmorBreakShake = true;

        [Header("Damage Animation (피격 회전 흔들림 연출)")]
        [Tooltip("피격 시 방금 깎인 하트가 붉게 번쩍이며 바들바들 떨리는 연출 사용 여부")]
        [SerializeField]
        private bool _enableDamageFeedback = true;

        [Tooltip("피격 시 순간적으로 깜빡일 색상 (붉은색)")]
        [SerializeField]
        private Color _damageFlashColor = new Color(1f, 0.2f, 0.2f, 1f);

        [Tooltip("피격 떨림 최대 각도 (도 단위, 예: 16도)")]
        [SerializeField, Range(5f, 30f)]
        private float _damageShakeAngle = 16f;

        [Tooltip("피격 떨림 진동수 (속도)")]
        [SerializeField, Range(20f, 60f)]
        private float _damageShakeSpeed = 45f;

        [Tooltip("피격 연출 지속 시간 (초)")]
        [SerializeField, Range(0.1f, 0.5f)]
        private float _damageEffectDuration = 0.25f;

        [Header("Low Health Warning (빈사 상태 경고)")]
        [Tooltip("체력이 위험 수치 이하일 때 마지막 하트가 두근두근 뛰는 펄스 연출 사용 여부")]
        [SerializeField]
        private bool _enableLowHealthPulse = true;

        [Tooltip("빈사 상태로 판단할 체력 기준값 (기본: 하트 1개 이하)")]
        [SerializeField, Min(1)]
        private int _lowHealthThreshold = 1;

        [Tooltip("두근거리는 속도")]
        [SerializeField, Range(2f, 10f)]
        private float _pulseSpeed = 5f;

        [Tooltip("두근거릴 때 커지는 배율")]
        [SerializeField, Range(1.05f, 1.4f)]
        private float _pulseScale = 1.2f;

        [Header("Heal Animation (회복 연출)")]
        [Tooltip("체력 회복 시 하트가 톡 튀어나오는 팝 애니메이션 사용 여부")]
        [SerializeField]
        private bool _enableHealPop = true;

        [Tooltip("회복 시 순간적으로 커지는 배율")]
        [SerializeField, Range(1.1f, 1.8f)]
        private float _healPopScale = 1.35f;

        [Tooltip("회복 팝 애니메이션 지속 시간 (초)")]
        [SerializeField, Range(0.05f, 0.3f)]
        private float _healPopDuration = 0.15f;

        private readonly List<Image> _spawnedHearts = new();
        private Coroutine _bindRoutine;
        private Coroutine _pulseRoutine;
        private int _previousHealth = -1;
        private bool _isSubscribed;

        private void Awake()
        {
            EnsureSprites();
        }

        private void EnsureSprites()
        {
            if (_fullHeartSprite == null && _heartPrefab != null)
            {
                _fullHeartSprite = _heartPrefab.sprite;
            }
        }

        private void Start()
        {
            if (_bindRoutine != null)
            {
                StopCoroutine(_bindRoutine);
            }

            _bindRoutine = StartCoroutine(BindHealthRoutine());
        }

        private void OnEnable()
        {
            if (_playerHealth != null && !_isSubscribed)
            {
                SubscribeHealthEvents();
            }
        }

        private void OnDisable()
        {
            if (_bindRoutine != null)
            {
                StopCoroutine(_bindRoutine);
                _bindRoutine = null;
            }

            StopLowHealthPulse();
            UnsubscribeHealthEvents();
        }

        /// <summary>
        /// Player 및 PlayerHealth 초기화(Awake)가 완료될 때까지 안전하게 대기 후 바인딩
        /// </summary>
        private IEnumerator BindHealthRoutine()
        {
            while (_playerHealth == null)
            {
                if (PlayerCharacter.PlayerInstance != null)
                {
                    _playerHealth = PlayerCharacter.PlayerInstance.Health;
                }

                if (_playerHealth == null)
                {
                    _playerHealth = FindFirstObjectByType<PlayerHealth>();
                }

                if (_playerHealth == null)
                {
                    yield return null;
                }
            }

            while (_playerHealth.MaxHealth <= 0)
            {
                yield return null;
            }

            SubscribeHealthEvents();
            RebuildHeartGrid(_playerHealth.MaxHealth);
            _previousHealth = _playerHealth.CurrentHealth;
            UpdateHealthDisplay(_playerHealth.CurrentHealth, _playerHealth.MaxHealth, isDamage: false, isHeal: false);

            _bindRoutine = null;
        }

        private void SubscribeHealthEvents()
        {
            if (_isSubscribed || _playerHealth == null) return;

            _playerHealth.HealthChanged += HandleHealthChanged;
            _playerHealth.Died += HandleDied;
            _playerHealth.ArmorActivated += HandleArmorActivated;
            _playerHealth.ArmorBroken += HandleArmorDeactivated;

            _isSubscribed = true;
        }

        private void UnsubscribeHealthEvents()
        {
            if (!_isSubscribed || _playerHealth == null) return;

            _playerHealth.HealthChanged -= HandleHealthChanged;
            _playerHealth.Died -= HandleDied;
            _playerHealth.ArmorActivated -= HandleArmorActivated;
            _playerHealth.ArmorBroken -= HandleArmorDeactivated;

            _isSubscribed = false;
        }

        /// <summary>
        /// 최대 체력 수치만큼 하트 슬롯들을 생성/배치합니다.
        /// </summary>
        private void RebuildHeartGrid(int maxHealth)
        {
            if (_heartContainer == null || _heartPrefab == null || maxHealth <= 0) return;

            EnsureSprites();

            foreach (Transform child in _heartContainer)
            {
                if (child.gameObject == _heartPrefab.gameObject)
                {
                    child.gameObject.SetActive(false);
                    continue;
                }

                Destroy(child.gameObject);
            }

            _spawnedHearts.Clear();

            for (int i = 0; i < maxHealth; i++)
            {
                Image heart = Instantiate(_heartPrefab, _heartContainer);
                heart.gameObject.SetActive(true);
                heart.transform.localScale = Vector3.one;
                heart.transform.localRotation = Quaternion.identity;
                if (_fullHeartSprite != null)
                {
                    heart.sprite = _fullHeartSprite;
                }

                _spawnedHearts.Add(heart);
            }
        }

        private void HandleHealthChanged(int currentHealth, int maxHealth)
        {
            if (_spawnedHearts.Count != maxHealth)
            {
                RebuildHeartGrid(maxHealth);
            }

            bool isDamage = _previousHealth > currentHealth;
            bool isHeal = _previousHealth >= 0 && _previousHealth < currentHealth;
            int damagedFrom = currentHealth;
            int damagedTo = _previousHealth;

            _previousHealth = currentHealth;

            UpdateHealthDisplay(currentHealth, maxHealth, isDamage, isHeal, damagedFrom, damagedTo);

            // 빈사 상태 펄스 연출 제어
            if (_enableLowHealthPulse && currentHealth <= _lowHealthThreshold && currentHealth > 0)
            {
                StartLowHealthPulse();
            }
            else
            {
                StopLowHealthPulse();
            }
        }

        private void HandleDied()
        {
            StopLowHealthPulse();

            for (int i = 0; i < _spawnedHearts.Count; i++)
            {
                SetHeartState(_spawnedHearts[i], isLoaded: false);
            }

            if (_healthText != null)
            {
                _healthText.text = $"0 / {_playerHealth.MaxHealth}";
            }
        }

        private void HandleArmorActivated()
        {
            if (_playerHealth == null) return;

            int currentHealth = _playerHealth.CurrentHealth;
            int maxHealth = _playerHealth.MaxHealth;
            UpdateHealthDisplay(currentHealth, maxHealth, isDamage: false, isHeal: false);

            int armorIndex = currentHealth - 1;
            if (_enableArmorGainPop && armorIndex >= 0 && armorIndex < _spawnedHearts.Count)
            {
                Image armorHeart = _spawnedHearts[armorIndex];
                if (armorHeart != null)
                {
                    StartCoroutine(HealPopRoutine(armorHeart.transform));
                }
            }
        }

        private void HandleArmorDeactivated()
        {
            if (_playerHealth == null) return;

            int currentHealth = _playerHealth.CurrentHealth;
            int armorIndex = currentHealth - 1;

            if (_enableArmorBreakShake && armorIndex >= 0 && armorIndex < _spawnedHearts.Count)
            {
                Image armorHeart = _spawnedHearts[armorIndex];
                if (armorHeart != null)
                {
                    StartCoroutine(ArmorBreakRoutine(armorHeart));
                    return;
                }
            }

            UpdateHealthDisplay(currentHealth, _playerHealth.MaxHealth, isDamage: false, isHeal: false);
        }

        private void UpdateHealthDisplay(int currentHealth, int maxHealth, bool isDamage, bool isHeal,
            int damagedFrom = 0, int damagedTo = 0)
        {
            if (_healthText != null)
            {
                _healthText.text = $"{currentHealth} / {maxHealth}";
            }

            bool hasArmor = _playerHealth != null && _playerHealth.HasArmor;
            int armorIndex = (hasArmor && currentHealth > 0) ? currentHealth - 1 : -1;

            for (int i = 0; i < _spawnedHearts.Count; i++)
            {
                var heart = _spawnedHearts[i];
                if (heart == null) continue;

                bool isAlive = i < currentHealth;
                bool isArmorSlot = isAlive && i == armorIndex;

                if (isAlive)
                {
                    SetHeartState(heart, isLoaded: true, isArmor: isArmorSlot);

                    // 회복 시 방금 채워진 하트에 팝 연출
                    if (isHeal && _enableHealPop && i == currentHealth - 1)
                    {
                        StartCoroutine(HealPopRoutine(heart.transform));
                    }
                }
                else
                {
                    // 방금 깎인 하트인 경우 회전 흔들림 + 플래시 연출 후 딤드/비활성화 처리
                    if (isDamage && _enableDamageFeedback && i >= damagedFrom && i < damagedTo)
                    {
                        StartCoroutine(DamageShakeRoutine(heart));
                    }
                    else
                    {
                        SetHeartState(heart, isLoaded: false);
                    }
                }
            }
        }

        private void SetHeartState(Image heart, bool isLoaded, bool isArmor = false)
        {
            if (heart == null) return;

            heart.transform.localScale = Vector3.one;
            heart.transform.localRotation = Quaternion.identity;
            heart.gameObject.SetActive(true);
            heart.color = Color.white;

            if (isLoaded)
            {
                if (isArmor && _armorHeartSprite != null)
                {
                    heart.sprite = _armorHeartSprite;
                }
                else if (_fullHeartSprite != null)
                {
                    heart.sprite = _fullHeartSprite;
                }
            }
            else
            {
                if (_emptyHeartSprite != null)
                {
                    heart.sprite = _emptyHeartSprite;
                }
            }
        }

        #region Animation Routines (타격감 & 레이아웃 안전 연출)

        /// <summary>
        /// 피격 시 하트가 붉게 번쩍이며 제자리에서 좌우로 바들바들 떨리다가(Rotation Shake) 빈 하트 스프라이트로 전환됩니다.
        /// 위치(Position)를 전혀 건드리지 않으므로 HorizontalLayoutGroup과 완벽하게 호환됩니다.
        /// </summary>
        private IEnumerator DamageShakeRoutine(Image heart)
        {
            if (heart == null) yield break;

            heart.gameObject.SetActive(true);
            heart.color = _damageFlashColor;
            if (_fullHeartSprite != null)
            {
                heart.sprite = _fullHeartSprite;
            }

            float elapsed = 0f;

            while (elapsed < _damageEffectDuration)
            {
                if (heart == null) yield break;

                elapsed += Time.unscaledDeltaTime;

                // 시간이 지날수록 떨림 강도가 자연스럽게 감쇠(Damping)
                float decay = 1f - Mathf.Clamp01(elapsed / _damageEffectDuration);
                float angle = Mathf.Sin(elapsed * _damageShakeSpeed) * _damageShakeAngle * decay;

                heart.transform.localRotation = Quaternion.Euler(0f, 0f, angle);

                yield return null;
            }

            if (heart != null)
            {
                heart.transform.localRotation = Quaternion.identity;
                SetHeartState(heart, isLoaded: false);
            }
        }

        /// <summary>
        /// 아머로 피격을 막아냈을 때, 방패 하트가 붉게 번쩍이며 떨리다가(Rotation Shake) 일반 하트 스프라이트로 복구됩니다.
        /// </summary>
        private IEnumerator ArmorBreakRoutine(Image heart)
        {
            if (heart == null) yield break;

            heart.gameObject.SetActive(true);
            heart.color = _damageFlashColor;
            if (_armorHeartSprite != null)
            {
                heart.sprite = _armorHeartSprite;
            }

            float elapsed = 0f;

            while (elapsed < _damageEffectDuration)
            {
                if (heart == null) yield break;

                elapsed += Time.unscaledDeltaTime;

                // 시간이 지날수록 떨림 강도가 자연스럽게 감쇠(Damping)
                float decay = 1f - Mathf.Clamp01(elapsed / _damageEffectDuration);
                float angle = Mathf.Sin(elapsed * _damageShakeSpeed) * _damageShakeAngle * decay;

                heart.transform.localRotation = Quaternion.Euler(0f, 0f, angle);

                yield return null;
            }

            if (heart != null)
            {
                heart.transform.localRotation = Quaternion.identity;
                if (_playerHealth != null && _playerHealth.IsDead)
                {
                    SetHeartState(heart, isLoaded: false);
                }
                else
                {
                    SetHeartState(heart, isLoaded: true, isArmor: false);
                }
            }
        }

        private IEnumerator HealPopRoutine(Transform target)
        {
            if (target == null) yield break;

            Vector3 normalScale = Vector3.one;
            Vector3 peakScale = Vector3.one * _healPopScale;

            float halfDuration = _healPopDuration * 0.5f;
            float elapsed = 0f;

            while (elapsed < halfDuration)
            {
                if (target == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                target.localScale = Vector3.Lerp(normalScale, peakScale, t);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                if (target == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                target.localScale = Vector3.Lerp(peakScale, normalScale, t);
                yield return null;
            }

            if (target != null)
            {
                target.localScale = normalScale;
            }
        }

        private void StartLowHealthPulse()
        {
            if (_pulseRoutine != null) return;
            _pulseRoutine = StartCoroutine(LowHealthPulseRoutine());
        }

        private void StopLowHealthPulse()
        {
            if (_pulseRoutine != null)
            {
                StopCoroutine(_pulseRoutine);
                _pulseRoutine = null;
            }

            if (_spawnedHearts.Count > 0 && _spawnedHearts[0] != null)
            {
                _spawnedHearts[0].transform.localScale = Vector3.one;
                _spawnedHearts[0].transform.localRotation = Quaternion.identity;
            }
        }

        private IEnumerator LowHealthPulseRoutine()
        {
            while (true)
            {
                if (_spawnedHearts.Count == 0 || _spawnedHearts[0] == null)
                {
                    yield return null;
                    continue;
                }

                Image lastHeart = _spawnedHearts[0];
                float sin = (Mathf.Sin(Time.unscaledTime * _pulseSpeed) + 1f) * 0.5f;
                float currentScale = Mathf.Lerp(1f, _pulseScale, sin);

                lastHeart.transform.localScale = Vector3.one * currentScale;

                yield return null;
            }
        }

        #endregion
    }
}
