using System;
using System.Collections.Generic;
using ShotgunAndRun.Core;
using ShotgunAndRun.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InventorySystem
{
    /// <summary>
    /// 상자 드롭 테이블의 개별 아이템 항목 설정
    /// </summary>
    [System.Serializable]
    public struct LootDropEntry
    {
        [Tooltip("드롭할 아이템 데이터")]
        public ItemData itemData;

        [Tooltip("최소 드롭 수량")]
        [Min(1)] public int minQuantity;

        [Tooltip("최대 드롭 수량")]
        [Min(1)] public int maxQuantity;

        [Tooltip("드롭 확률 (0.0 ~ 1.0, 1.0이면 100% 드롭)")]
        [Range(0f, 1f)] public float dropChance;
    }

    /// <summary>
    /// 상자 겉모습 변형 하나 (닫힘 · 열림 스프라이트 한 쌍)
    /// </summary>
    [System.Serializable]
    public struct ChestLook
    {
        public Sprite closedSprite;
        public Sprite openedSprite;
    }

    /// <summary>
    /// 필드에 배치되어 일정 시간(2~3초) 상호작용 후 열리는 루팅 상자 컴포넌트
    /// </summary>
    [RequireComponent(typeof(Inventory))]
    public class LootChest : MonoBehaviour
    {
        [Header("Interaction Settings")]
        [Tooltip("상자를 여는 데 걸리는 시간 (초)")]
        [SerializeField, Min(0.1f)] private float _interactionDuration = 2f;

        [Tooltip("플레이어 상호작용 가능 반경 (Trigger Collider가 없을 때 사용)")]
        [SerializeField, Min(0.5f)] private float _interactionRadius = 1.8f;

        [Tooltip("E키를 누르고 있어야 하는지 (true), 한 번만 눌러도 열리는지 (false)")]
        [SerializeField] private bool _requireHoldKey = true;

        [Header("Chest Inventory")]
        [SerializeField] private Inventory _chestInventory;

        [Header("Loot Drop Settings")]
        [Tooltip("상자가 처음 열릴 때 아이템을 자동 생성할지 여부")]
        [SerializeField] private bool _autoGenerateLoot = true;

        [Tooltip("중앙 카탈로그에서 무작위 아이템을 추첨할 때 참조 (선택 사항)")]
        [SerializeField] private ItemCatalogSO _itemCatalog;

        [Tooltip("중앙 카탈로그에서 무작위로 추첨할 아이템 종류 수 (0이면 사용 안 함)")]
        [SerializeField, Min(0)] private int _randomCatalogItemCount = 2;

        [Tooltip("카탈로그 무작위 추첨 시 최소 수량")]
        [SerializeField, Min(1)] private int _catalogItemMinQty = 1;

        [Tooltip("카탈로그 무작위 추첨 시 최대 수량")]
        [SerializeField, Min(1)] private int _catalogItemMaxQty = 3;

        [Tooltip("상자에 확정/확률적으로 드롭할 커스텀 아이템 목록")]
        [SerializeField] private List<LootDropEntry> _customLootTable = new();

        [Header("Visual / Sprite Settings")]
        [Tooltip("상자 이미지를 표시할 SpriteRenderer (비워두면 자동 탐색)")]
        [SerializeField] private SpriteRenderer _spriteRenderer;

        [Tooltip("상자가 닫혀 있을 때의 기본 스프라이트")]
        [SerializeField] private Sprite _closedSprite;

        [Tooltip("상자가 열렸을 때 표시할 스프라이트")]
        [SerializeField] private Sprite _openedSprite;

        [Tooltip("스폰할 때 LootSpawner가 하나를 골라 적용하는 겉모습 변형 목록")]
        [SerializeField] private ChestLook[] _looks = Array.Empty<ChestLook>();

        [SerializeField] private PlayerWeapon _playerWeapon;
        [SerializeField] private AudioClip _rootingSfxClip;
        [SerializeField] private AudioSource _audioSource;
        
        private Transform _playerTransform;
        private Collider2D _triggerCollider;
        private bool _isPlayerInRange;
        private bool _isInteracting;
        private bool _isOpened;
        private bool _hasGeneratedLoot;
        private float _currentProgressTimer;

        public bool IsOpened => _isOpened;
        public bool IsInteracting => _isInteracting;
        public bool IsPlayerInRange => _isPlayerInRange;
        public float OpenDuration => _interactionDuration;
        public float CurrentProgress => Mathf.Clamp01(_currentProgressTimer / _interactionDuration);
        public Inventory ChestInventory => _chestInventory;
        public SpriteRenderer SpriteRenderer => _spriteRenderer;
        public int LookCount => _looks.Length;

        /// <summary>
        /// 플레이어가 상자 상호작용 가능 반경에 진입(true)하거나 이탈(false)할 때 발생하는 이벤트
        /// </summary>
        public event Action<bool> OnPlayerRangeChanged;

        /// <summary>
        /// 상호작용 시작 시 호출되는 이벤트 (열리는 총 시간: 초)
        /// </summary>
        public event Action<float> OnOpenStarted;

        /// <summary>
        /// 상호작용 진행도 변경 이벤트 (0.0f ~ 1.0f)
        /// </summary>
        public event Action<float> OnOpenProgressChanged;

        /// <summary>
        /// 상호작용 취소 이벤트 (범위 이탈, 공격 피격 등)
        /// </summary>
        public event Action OnOpenCancelled;

        /// <summary>
        /// 상자가 완전히 열렸을 때 이벤트
        /// </summary>
        public event Action<LootChest> OnChestOpened;

        private void Awake()
        {
            if (_chestInventory == null)
            {
                _chestInventory = GetComponent<Inventory>();
            }

            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>();
            }

            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
                if (_spriteRenderer == null)
                {
                    _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
                }
            }

            var colliders = GetComponents<Collider2D>();
            foreach (var col in colliders)
            {
                if (col.isTrigger)
                {
                    _triggerCollider = col;
                    break;
                }
            }

            UpdateVisual();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
                if (_spriteRenderer == null)
                {
                    _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
                }
            }

            if (!Application.isPlaying && _spriteRenderer != null)
            {
                if (_isOpened && _openedSprite != null)
                {
                    _spriteRenderer.sprite = _openedSprite;
                }
                else if (!_isOpened && _closedSprite != null)
                {
                    _spriteRenderer.sprite = _closedSprite;
                }
            }
        }
#endif

        private void OnDisable()
        {
            StopRootingSound();

            if (_isInteracting)
            {
                CancelInteraction();
            }

            if (_isPlayerInRange)
            {
                _isPlayerInRange = false;
                OnPlayerExitRange();
                OnPlayerRangeChanged?.Invoke(false);
            }
        }

        private void Update()
        {
            CheckPlayerDistance();

            if (_isOpened)
            {
                HandleOpenedChestInput();
                return;
            }

            HandleInteractionInput();
            UpdateInteractionProgress();
        }

        private void HandleOpenedChestInput()
        {
            if (!_isPlayerInRange) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.eKey.wasPressedThisFrame)
            {
                if (LootChestUI.Instance != null && !LootChestUI.Instance.IsContentsOpen)
                {
                    ReopenChest();
                }
            }
        }

        private void ReopenChest()
        {
            Debug.Log("[LootChest] 이미 열린 상자를 즉시 다시 엽니다.");
            LootChestUI.Instance?.OpenChest(this);
        }


        private void CheckPlayerDistance()
        {
            // 상자에 별도의 Trigger Collider2D가 있다면 물리 트리거 이벤트(OnTriggerEnter/Exit)를 우선 사용합니다.
            if (_triggerCollider != null) return;

            if (_playerTransform == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    _playerTransform = player.transform;
                }
                else
                {
                    var move = FindFirstObjectByType<PlayerMovement>();
                    if (move != null) _playerTransform = move.transform;
                }
            }

            if (_playerTransform != null)
            {
                float distance = Vector2.Distance(transform.position, _playerTransform.position);
                bool inRange = distance <= _interactionRadius;

                if (inRange != _isPlayerInRange)
                {
                    _isPlayerInRange = inRange;
                    if (!_isPlayerInRange)
                    {
                        // 범위를 벗어남 -> 상호작용 취소
                        OnPlayerExitRange();
                    }
                    OnPlayerRangeChanged?.Invoke(_isPlayerInRange);
                }
            }
        }

        private void HandleInteractionInput()
        {
            if (!_isPlayerInRange || _isOpened) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            bool isKeyPressed = _requireHoldKey ? keyboard.eKey.isPressed : keyboard.eKey.wasPressedThisFrame;

            if (isKeyPressed)
            {
                if (!_isInteracting)
                {
                    StartInteraction();
                }
            }
            else if (_requireHoldKey && _isInteracting)
            {
                // 키를 뗐을 때 취소
                CancelInteraction();
            }
        }

        private void StartInteraction()
        {
            _isInteracting = true;
            _currentProgressTimer = 0f;
            
            SetPlayerWeaponLocked(true);
            PlayRootingSound();
            
            OnOpenStarted?.Invoke(_interactionDuration);
            OnOpenProgressChanged?.Invoke(0f);
            Debug.Log("[LootChest] 상자 열기를 시작합니다...");
        }

        private void UpdateInteractionProgress()
        {
            if (!_isInteracting) return;

            _currentProgressTimer += Time.deltaTime;
            float progress = CurrentProgress;
            OnOpenProgressChanged?.Invoke(progress);

            if (_currentProgressTimer >= _interactionDuration)
            {
                CompleteOpen();
            }
        }

        private void CompleteOpen()
        {
            _isInteracting = false;
            _isOpened = true;
            
            SetPlayerWeaponLocked(false);
            StopRootingSound();
            
            _currentProgressTimer = _interactionDuration;
            OnOpenProgressChanged?.Invoke(1f);

            UpdateVisual();

            // 상자 개봉 시 아이템 자동 생성 (최초 1회만 생성)
            if (_autoGenerateLoot && !_hasGeneratedLoot)
            {
                _hasGeneratedLoot = true;
                GenerateLoot();
            }

            Debug.Log("[LootChest] 상자가 열렸습니다!");
            OnChestOpened?.Invoke(this);
            LootChestUI.Instance?.OpenChest(this);
        }

        /// <summary>
        /// 처음 열 때의 자동 드롭을 끕니다.
        /// LootSpawner처럼 내용을 미리 채운 상자에 무작위 아이템이 더 들어가지 않게 할 때 사용합니다.
        /// </summary>
        public void DisableAutoLoot()
        {
            _autoGenerateLoot = false;
        }

        /// <summary>
        /// 설정된 커스텀 드롭 테이블 및 ItemCatalogSO에서 아이템을 추첨하여 상자에 채웁니다.
        /// </summary>
        public void GenerateLoot()
        {
            if (_chestInventory == null) return;

            // 1. 커스텀 지정 드롭 테이블 항목 처리
            if (_customLootTable != null)
            {
                foreach (var entry in _customLootTable)
                {
                    if (entry.itemData == null) continue;

                    if (UnityEngine.Random.value <= entry.dropChance)
                    {
                        int min = Mathf.Max(1, entry.minQuantity);
                        int max = Mathf.Max(min, entry.maxQuantity);
                        int qty = UnityEngine.Random.Range(min, max + 1);

                        _chestInventory.AddItem(entry.itemData, qty);
                    }
                }
            }

            // 2. ItemCatalogSO에서 무작위 재료 아이템 추첨
            if (_itemCatalog != null && _itemCatalog.Items != null && _itemCatalog.Items.Count > 0 && _randomCatalogItemCount > 0)
            {
                var availablePool = new List<ItemData>(_itemCatalog.Items);
                int pickCount = Mathf.Min(_randomCatalogItemCount, availablePool.Count);

                for (int i = 0; i < pickCount; i++)
                {
                    if (availablePool.Count == 0) break;

                    int randomIndex = UnityEngine.Random.Range(0, availablePool.Count);
                    ItemData pickedItem = availablePool[randomIndex];
                    availablePool.RemoveAt(randomIndex); // 중복 종류 방지

                    int min = Mathf.Max(1, _catalogItemMinQty);
                    int max = Mathf.Max(min, _catalogItemMaxQty);
                    int qty = UnityEngine.Random.Range(min, max + 1);

                    _chestInventory.AddItem(pickedItem, qty);
                }
            }
        }

        /// <summary>
        /// 겉모습 변형 목록(_looks)에서 index번째 닫힘 · 열림 스프라이트를 적용합니다.
        /// </summary>
        public void ApplyLook(int index)
        {
            if (index < 0 || index >= _looks.Length) return;

            _closedSprite = _looks[index].closedSprite;
            _openedSprite = _looks[index].openedSprite;
            UpdateVisual();
        }

        /// <summary>
        /// 상자의 열림 여부에 맞춰 스프라이트를 교체합니다.
        /// 한 번 열린 상자는 _isOpened가 계속 true이므로 영구적으로 열린 스프라이트를 유지합니다.
        /// </summary>
        public void UpdateVisual()
        {
            if (_spriteRenderer == null) return;

            if (_isOpened)
            {
                if (_openedSprite != null)
                {
                    _spriteRenderer.sprite = _openedSprite;
                }
            }
            else
            {
                if (_closedSprite != null)
                {
                    _spriteRenderer.sprite = _closedSprite;
                }
            }
        }

        /// <summary>
        /// 피격당하거나 플레이어가 범위를 벗어났을 때 외부(전투 시스템 등)에서 호출 가능한 취소 메서드
        /// </summary>
        public void CancelInteraction()
        {
            if (!_isInteracting) return;

            _isInteracting = false;
            _currentProgressTimer = 0f;
            
            SetPlayerWeaponLocked(false);
            StopRootingSound();
            
            OnOpenProgressChanged?.Invoke(0f);
            OnOpenCancelled?.Invoke();

            Debug.Log("[LootChest] 상자 상호작용이 취소되었습니다.");
        }

        private void PlayRootingSound()
        {
            if (_rootingSfxClip == null || _audioSource == null) return;

            if (_audioSource.outputAudioMixerGroup == null && AudioManager.Instance != null)
            {
                _audioSource.outputAudioMixerGroup = AudioManager.Instance.UiGroup;
            }

            _audioSource.clip = _rootingSfxClip;
            _audioSource.loop = true;
            _audioSource.Play();
        }

        private void StopRootingSound()
        {
            if (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.Stop();
            }
        }

        private void OnPlayerExitRange()
        {
            if (_isInteracting)
            {
                CancelInteraction();
            }

            if (_isOpened && LootChestUI.Instance != null && LootChestUI.Instance.IsContentsOpen)
            {
                LootChestUI.Instance.CloseChest();
            }
        }
        
        private void SetPlayerWeaponLocked(bool locked)
        {
            if (_playerWeapon == null)
            {
                if (_playerTransform != null)
                {
                    _playerWeapon =
                        _playerTransform.GetComponentInParent<PlayerWeapon>() ??
                        _playerTransform.GetComponentInChildren<PlayerWeapon>(true);
                }

                if (_playerWeapon == null &&
                    Player.PlayerInstance != null)
                {
                    _playerWeapon =
                        Player.PlayerInstance.GetComponent<PlayerWeapon>();
                }
            }

            _playerWeapon?.SetChestInteractionLocked(locked);
        }

        #region Trigger Collider2D Support (옵션)

        private void OnTriggerEnter2D(Collider2D collision)
        {
            // Trigger Collider가 없으면 거리 기반(CheckPlayerDistance)으로 동작하므로 무시합니다.
            if (_triggerCollider == null) return;

            if (collision.CompareTag("Player") || collision.GetComponent<PlayerMovement>() != null)
            {
                _playerTransform = collision.transform;
                if (!_isPlayerInRange)
                {
                    _isPlayerInRange = true;
                    OnPlayerRangeChanged?.Invoke(true);
                }
            }
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            // Trigger Collider가 없으면 거리 기반(CheckPlayerDistance)으로 동작하므로 무시합니다.
            if (_triggerCollider == null) return;

            if (collision.CompareTag("Player") || collision.GetComponent<PlayerMovement>() != null)
            {
                if (_isPlayerInRange)
                {
                    _isPlayerInRange = false;
                    OnPlayerExitRange();
                    OnPlayerRangeChanged?.Invoke(false);
                }
            }
        }

        #endregion

        #region Gizmos (에디터 범위 시각화)

        private void OnDrawGizmosSelected()
        {
            // Trigger Collider가 없을 때만 interactionRadius 구체 범위를 표시합니다.
            if (_triggerCollider == null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, _interactionRadius);
            }
        }

        #endregion
    }
}
