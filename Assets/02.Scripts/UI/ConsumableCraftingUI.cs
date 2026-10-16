using System.Collections.Generic;
using Consumable;
using InventorySystem;
using ShotgunAndRun.Core;
using ShotgunAndRun.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShotgunAndRun.UI
{
    /// <summary>
    /// 쉘터에서 소모품을 제작하는 전용 UI 컴포넌트입니다.
    /// - 좌측/목록: 제작 가능한 소모품 레시피 슬롯 (CraftingRecipeSlotUI)
    /// - 중앙: 선택된 소모품의 상세 스펙 (아이콘, 이름, 설명, 효과 수치, 최대 소지량)
    /// - 하단: 필요 재료 슬롯 (UpgradeMaterialSlotUI) 및 제작 버튼
    /// - 제작된 소모품은 플레이어의 소모품 전용 퀵슬롯(IConsumableInventory)에 바로 지급됩니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ConsumableCraftingUI : MonoBehaviour, IPopupUI
    {
        public static ConsumableCraftingUI Instance { get; private set; }
        [Header("Catalogs & Master Data")]
        [Tooltip("소모품 제작 레시피 ScriptableObject")]
        [SerializeField] private ConsumableCraftingCosts _craftingCostsCatalog;

        [Tooltip("소모품 마스터 카탈로그 ScriptableObject")]
        [SerializeField] private ConsumableCatalog _consumableCatalog;

        [Header("UI Panels & Window Controls")]
        [Tooltip("제작 UI 루트 패널")]
        [SerializeField] private GameObject _windowPanel;

        [Tooltip("닫기 버튼")]
        [SerializeField] private Button _closeButton;

        [Tooltip("ESC 키로 닫기 가능 여부")]
        [SerializeField] private bool _closeOnEscape = true;

        [Header("Recipe Slots (List / Grid)")]
        [Tooltip("레시피 슬롯들이 생성/배치될 컨테이너 (GridLayoutGroup 권장)")]
        [SerializeField] private Transform _recipeSlotContainer;

        [Tooltip("동적 생성할 레시피 슬롯 프리팹 (지정 시 컨테이너에 자동 생성)")]
        [SerializeField] private CraftingRecipeSlotUI _recipeSlotPrefab;

        [Tooltip("씬에 미리 배치된 고정 레시피 슬롯들 (프리팹 동적 생성 대신 사용 가능)")]
        [SerializeField] private CraftingRecipeSlotUI[] _preplacedRecipeSlots;

        [Header("Center: Selected Consumable Detail")]
        [Tooltip("선택된 소모품 대형 아이콘 이미지")]
        [SerializeField] private Image _consumableBigImage;

        [Tooltip("선택된 소모품 이름 텍스트")]
        [SerializeField] private TextMeshProUGUI _consumableNameText;

        [Tooltip("선택된 소모품 카테고리/분류 텍스트 (회복 / 투척 / 버프)")]
        [SerializeField] private TextMeshProUGUI _categoryText;

        [Tooltip("선택된 소모품 설명 텍스트")]
        [SerializeField] private TextMeshProUGUI _descriptionText;

        [Tooltip("선택된 소모품 효과 수치 안내 텍스트")]
        [SerializeField] private TextMeshProUGUI _effectText;

        [Tooltip("최대 소지 개수 텍스트")]
        [SerializeField] private TextMeshProUGUI _maxStackText;

        [Header("Bottom: Material Cost Slots & Craft Button")]
        [Tooltip("필요 제작 재료 슬롯 (최대 4개 슬롯 연결)")]
        [SerializeField] private UpgradeMaterialSlotUI[] _materialSlots = new UpgradeMaterialSlotUI[4];

        [Tooltip("제작 실행 버튼")]
        [SerializeField] private Button _craftButton;

        [Tooltip("제작 버튼 내부 텍스트 (제작하기 / 재료 부족 / 슬롯 가득 참)")]
        [SerializeField] private TextMeshProUGUI _craftButtonText;

        [Tooltip("제작 가능 시 적용할 버튼 색상 세트")]
        [SerializeField] private ColorBlock _craftButtonActiveColors = CreateDefaultActiveButtonColors();

        [Tooltip("제작 가능 시 버튼 텍스트 색상")]
        [SerializeField] private Color _activeTextColor = Color.white;

        [Tooltip("제작 불가 시 버튼 텍스트 색상")]
        [SerializeField] private Color _disabledTextColor = new Color(0.2f, 0.2f, 0.2f, 0.6f);

        [Tooltip("상태/안내 메시지 텍스트 (선택사항)")]
        [SerializeField] private TextMeshProUGUI _statusMessageText;

        private readonly List<CraftingRecipeSlotUI> _activeRecipeSlots = new List<CraftingRecipeSlotUI>();
        private ConsumableData _selectedData;
        private ConsumableCraftingService _craftingService;
        private bool _isInitialized;
        private ColorBlock _craftButtonDefaultColors;
        private bool _hasCachedDefaultColors;

        public bool IsOpen => _windowPanel != null && _windowPanel.activeSelf;
        public ConsumableData SelectedData => _selectedData;

        private static ColorBlock CreateDefaultActiveButtonColors()
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = new Color(0.24f, 0.70f, 0.35f, 1f);
            colors.highlightedColor = new Color(0.33f, 0.85f, 0.45f, 1f);
            colors.pressedColor = new Color(0.18f, 0.55f, 0.26f, 1f);
            colors.selectedColor = new Color(0.24f, 0.70f, 0.35f, 1f);
            colors.disabledColor = new Color(0.78f, 0.78f, 0.78f, 0.5f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;
            return colors;
        }

        private void Awake()
        {
            Instance = this;

            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(Close);
            }

            if (_craftButton != null)
            {
                _craftButtonDefaultColors = _craftButton.colors;
                _hasCachedDefaultColors = true;
                _craftButton.onClick.AddListener(OnCraftButtonClicked);
            }
        }

        private void Start()
        {
            InitializeSlots();

            if (IsOpen)
            {
                RefreshUI();
            }
        }

        private void OnDisable()
        {
            PopupManager.Pop(this);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            PopupManager.Pop(this);

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(Close);
            }

            if (_craftButton != null)
            {
                _craftButton.onClick.RemoveListener(OnCraftButtonClicked);
            }
        }

#if UNITY_EDITOR
        private void Reset()
        {
            _craftingCostsCatalog ??= UnityEditor.AssetDatabase.LoadAssetAtPath<ConsumableCraftingCosts>("Assets/07.Datas/ConsumableCraftingCosts.asset");
            _consumableCatalog ??= UnityEditor.AssetDatabase.LoadAssetAtPath<ConsumableCatalog>("Assets/07.Datas/ConsumableCatalog.asset");
        }
#endif

        public void Open()
        {
            InitializeSlots();

            if (_windowPanel != null)
            {
                _windowPanel.SetActive(true);
                if (_closeOnEscape)
                {
                    PopupManager.Push(this);
                }
            }

            RefreshUI();
        }

        public void Close()
        {
            PopupManager.Pop(this);

            if (_windowPanel != null)
            {
                _windowPanel.SetActive(false);
            }

            if (ShelterStorageUI.Instance != null && ShelterStorageUI.Instance.IsOpen)
            {
                ShelterStorageUI.Instance.Close();
            }
        }

        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        /// <summary>
        /// 레시피 목록 슬롯들을 1회 초기화합니다.
        /// </summary>
        public void InitializeSlots()
        {
            if (_isInitialized)
            {
                return;
            }

            _activeRecipeSlots.Clear();

            // 1. 컨테이너 + 프리팹 방식인 경우 동적 생성
            if (_recipeSlotContainer != null && _recipeSlotPrefab != null && _craftingCostsCatalog != null)
            {
                for (int i = _recipeSlotContainer.childCount - 1; i >= 0; i--)
                {
                    Destroy(_recipeSlotContainer.GetChild(i).gameObject);
                }

                IReadOnlyList<ConsumableCraftingCost> recipes = _craftingCostsCatalog.GetAllRecipes();
                for (int i = 0; i < recipes.Count; i++)
                {
                    ConsumableData data = recipes[i].ConsumableData;
                    if (data == null)
                    {
                        continue;
                    }

                    CraftingRecipeSlotUI slot = Instantiate(_recipeSlotPrefab, _recipeSlotContainer);
                    _activeRecipeSlots.Add(slot);
                }
            }
            // 2. 씬에 미리 배치된 슬롯 배열이 있는 경우
            else if (_preplacedRecipeSlots != null && _preplacedRecipeSlots.Length > 0)
            {
                for (int i = 0; i < _preplacedRecipeSlots.Length; i++)
                {
                    if (_preplacedRecipeSlots[i] != null)
                    {
                        _activeRecipeSlots.Add(_preplacedRecipeSlots[i]);
                    }
                }
            }

            // 첫 번째 소모품을 기본 선택으로 지정
            if (_selectedData == null && _craftingCostsCatalog != null)
            {
                IReadOnlyList<ConsumableCraftingCost> recipes = _craftingCostsCatalog.GetAllRecipes();
                if (recipes.Count > 0)
                {
                    _selectedData = recipes[0].ConsumableData;
                }
            }

            _isInitialized = true;
        }

        public void SelectConsumable(ConsumableData data)
        {
            if (data == null)
            {
                return;
            }

            _selectedData = data;
            RefreshUI();
        }

        /// <summary>
        /// 소모품 목록, 중앙 상세 스펙, 하단 재료 슬롯 및 제작 버튼 상태를 최신 데이터로 갱신합니다.
        /// </summary>
        public void RefreshUI()
        {
            EnsureService();

            if (_craftingCostsCatalog == null)
            {
                SetCraftButtonState(false, "레시피 카탈로그 없음");
                return;
            }

            IReadOnlyList<ConsumableCraftingCost> recipes = _craftingCostsCatalog.GetAllRecipes();

            // 1. 레시피 슬롯 바인딩 및 갱신
            for (int i = 0; i < _activeRecipeSlots.Count; i++)
            {
                CraftingRecipeSlotUI slot = _activeRecipeSlots[i];
                if (slot == null)
                {
                    continue;
                }

                ConsumableData data = i < recipes.Count ? recipes[i].ConsumableData : slot.Data;
                if (data == null)
                {
                    continue;
                }

                bool isSelected = data == _selectedData;
                bool canCraftThis = _craftingService != null && _craftingService.CanCraft(data, out _);

                slot.Bind(data, isSelected, canCraftThis, SelectConsumable);
            }

            // 2. 중앙 상세 정보 갱신
            UpdateCenterDetailView(_selectedData);

            // 3. 하단 재료 슬롯 및 제작 버튼 갱신
            UpdateMaterialSlotsAndButton(_selectedData);
        }

        private void UpdateCenterDetailView(ConsumableData data)
        {
            if (data == null)
            {
                if (_consumableBigImage != null) _consumableBigImage.enabled = false;
                if (_consumableNameText != null) _consumableNameText.text = string.Empty;
                if (_categoryText != null) _categoryText.text = string.Empty;
                if (_descriptionText != null) _descriptionText.text = string.Empty;
                if (_effectText != null) _effectText.text = string.Empty;
                if (_maxStackText != null) _maxStackText.text = string.Empty;
                return;
            }

            if (_consumableBigImage != null)
            {
                _consumableBigImage.sprite = data.Icon;
                _consumableBigImage.enabled = data.Icon != null;
            }

            if (_consumableNameText != null)
            {
                _consumableNameText.text = data.ConsumableName;
            }

            if (_categoryText != null)
            {
                _categoryText.text = GetCategoryDisplayName(data.ConsumableType);
            }

            if (_descriptionText != null)
            {
                _descriptionText.text = data.Description;
            }

            if (_effectText != null)
            {
                _effectText.text = GetEffectDescription(data);
            }

            if (_maxStackText != null)
            {
                _maxStackText.text = $"최대 소지: {data.MaxStack}개";
            }
        }

        private void UpdateMaterialSlotsAndButton(ConsumableData data)
        {
            if (data == null || !_craftingCostsCatalog.TryGetRecipe(data, out ConsumableCraftingCost recipe))
            {
                ClearAllMaterialSlots();
                SetCraftButtonState(false, "제작 정보 없음");
                return;
            }

            MaterialCost[] costs = recipe.MaterialCosts;
            int costCount = costs != null ? costs.Length : 0;

            for (int i = 0; i < _materialSlots.Length; i++)
            {
                if (_materialSlots[i] == null)
                {
                    continue;
                }

                if (i < costCount && costs[i].ItemData != null && costs[i].Amount > 0)
                {
                    ItemData item = costs[i].ItemData;
                    int required = costs[i].Amount;
                    int storedCount = GameManager.Instance != null ? GameManager.Instance.GetItemCount(item) : 0;

                    _materialSlots[i].Bind(item, required, storedCount);
                }
                else
                {
                    _materialSlots[i].Clear();
                }
            }

            // 제작 가능 여부 및 버튼 텍스트 설정
            CraftFailureReason reason = CraftFailureReason.None;
            bool canCraft = _craftingService != null && _craftingService.CanCraft(data, out reason);

            if (canCraft)
            {
                SetCraftButtonState(true, "제작하기");
            }
            else
            {
                string buttonLabel = reason switch
                {
                    CraftFailureReason.InsufficientMaterials => "재료 부족",
                    CraftFailureReason.InventoryFull => "슬롯 가득 참",
                    CraftFailureReason.RecipeNotFound => "레시피 없음",
                    _ => "제작 불가"
                };
                SetCraftButtonState(false, buttonLabel);
            }
        }

        private void OnCraftButtonClicked()
        {
            if (_selectedData == null)
            {
                return;
            }

            EnsureService();

            if (_craftingService == null)
            {
                Debug.LogWarning("[ConsumableCraftingUI] CraftingService가 준비되지 않았습니다.");
                return;
            }

            bool success = _craftingService.TryCraft(_selectedData, out CraftFailureReason reason);
            if (success)
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.SaveProgress();
                }

                Debug.Log($"[ConsumableCraftingUI] {_selectedData.ConsumableName} 제작 성공!");
                RefreshUI();
                ShelterStorageUI.RequestRefresh();
            }
            else
            {
                Debug.LogWarning($"[ConsumableCraftingUI] {_selectedData.ConsumableName} 제작 실패: {reason}");
                RefreshUI();
            }
        }

        private void EnsureService()
        {
            if (_craftingService != null)
            {
                return;
            }

            if (_craftingCostsCatalog == null)
            {
                return;
            }

            IItemInventory storage = GameManager.Instance;
            if (storage == null)
            {
                return;
            }

            IConsumableInventory consumableInv = global::ShotgunAndRun.Player.Player.PlayerInstance != null ? global::ShotgunAndRun.Player.Player.PlayerInstance.ConsumableInventory : null;
            consumableInv ??= FindAnyObjectByType<PlayerConsumableInventory>();

            _craftingService = new ConsumableCraftingService(_craftingCostsCatalog, storage, consumableInv);
        }

        private void SetCraftButtonState(bool interactable, string label)
        {
            if (_craftButton != null)
            {
                if (!_hasCachedDefaultColors)
                {
                    _craftButtonDefaultColors = _craftButton.colors;
                    _hasCachedDefaultColors = true;
                }

                if (_craftButtonActiveColors.colorMultiplier <= 0f)
                {
                    _craftButtonActiveColors = CreateDefaultActiveButtonColors();
                }

                _craftButton.colors = interactable ? _craftButtonActiveColors : _craftButtonDefaultColors;
                _craftButton.interactable = interactable;
            }

            if (_craftButtonText != null)
            {
                _craftButtonText.text = label;
                _craftButtonText.color = interactable ? _activeTextColor : _disabledTextColor;
            }
        }

        private void ClearAllMaterialSlots()
        {
            for (int i = 0; i < _materialSlots.Length; i++)
            {
                if (_materialSlots[i] != null)
                {
                    _materialSlots[i].Clear();
                }
            }
        }

        private string GetCategoryDisplayName(ConsumableType type)
        {
            return type switch
            {
                ConsumableType.SmallKit or ConsumableType.LargeKit => "회복 아이템",
                ConsumableType.FragmentGrenade or ConsumableType.FlashBang => "투척 무기",
                ConsumableType.Adrenaline or ConsumableType.Armor => "특수 장비/버프",
                _ => "소모품"
            };
        }

        private string GetEffectDescription(ConsumableData data)
        {
            if (data == null || data.ConsumableEffectData == null)
            {
                return string.Empty;
            }

            ConsumableEffectData effect = data.ConsumableEffectData;
            return data.ConsumableType switch
            {
                ConsumableType.SmallKit => $"체력 {(int)effect.value}칸 즉시 회복 (시전 1.0초)",
                ConsumableType.LargeKit => $"체력 {(int)effect.value}칸 즉시 회복 (시전 2.0초)",
                ConsumableType.FragmentGrenade => "폭발 반경 2.5u 내 적에게 큰 피해를 입힙니다.",
                ConsumableType.FlashBang => "폭발 반경 4.0u 내 적의 시야와 행동을 마비시킵니다.",
                ConsumableType.Adrenaline => $"이동속도 {effect.value}배 증가 ({effect.duration}초간 지속)",
                ConsumableType.Armor => "다음 피격 1회를 100% 흡수합니다. (이속 10% 감소)",
                _ => string.Empty
            };
        }
    }
}
