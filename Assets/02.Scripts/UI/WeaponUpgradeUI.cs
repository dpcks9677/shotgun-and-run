using System;
using System.Collections.Generic;
using InventorySystem;
using ShotgunAndRun.Core;
using ShotgunAndRun.Cutscene;
using ShotgunAndRun.NPC;
using ShotgunAndRun.Quest;
using ShotgunAndRun.Weapon;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShotgunAndRun.UI
{
    /// <summary>
    /// 무기(샷건)의 5개 부품을 강화하는 전용 쉘터 UI입니다.
    /// - 상단: 해당 부품의 고정 재료 슬롯 (보유 시 알파 1.0, 미보유 시 알파 0.25)
    /// - 중앙: 부품 대형 Sprite, 현재/다음 레벨, 스탯 향상치 미리보기, 강화 실행 버튼
    /// - 하단: 5개 부품 선택 탭 (총열, 약실, 탄띠, 개머리판, 탄약)
    /// </summary>
    [DisallowMultipleComponent]
    public class WeaponUpgradeUI : MonoBehaviour, IPopupUI
    {
        public static WeaponUpgradeUI Instance { get; private set; }
        [Serializable]
        public struct PartTabButton
        {
            public WeaponPartsType PartType;
            public Button Button;
            public Image ActiveIndicator; // 선택 시 켜지는 하이라이트 이미지 (선택사항)
        }

        [Header("Master Data Catalog")]
        [Tooltip("무기 강화 레시피와 비용 및 파츠 메타 데이터가 담긴 ScriptableObject")]
        [SerializeField]
        private WeaponUpgradeCatalogSO _upgradeCatalog;

        [Header("UI Panels & Window Controls")]
        [Tooltip("강화 UI의 루트 패널 오브젝트")]
        [SerializeField]
        private GameObject _windowPanel;

        [Tooltip("닫기 버튼")]
        [SerializeField]
        private Button _closeButton;

        [Tooltip("ESC 키로 닫을 수 있는지 여부")]
        [SerializeField]
        private bool _closeOnEscape = true;

        [Header("Top: Fixed Material Slots (5 Slots)")]
        [Tooltip("고정 재료 슬롯 5개 [Main, Sub1, Sub2, Sub3, Core]")]
        [SerializeField]
        private UpgradeMaterialSlotUI[] _materialSlots = new UpgradeMaterialSlotUI[5];

        [Tooltip("최대 레벨 도달 시 상단에 표시할 완료 안내 오브젝트 (선택사항)")]
        [SerializeField]
        private GameObject _maxLevelBanner;

        [Header("Center: Selected Part Display")]
        [Tooltip("선택된 부품 대형 스프라이트 이미지")]
        [SerializeField]
        private Image _partBigImage;

        [Tooltip("선택된 부품 이름 텍스트 (예: Barrel, 개머리판 등)")]
        [SerializeField]
        private TextMeshProUGUI _partNameText;

        [Tooltip("선택된 부품 설명 텍스트 (선택사항)")]
        [SerializeField]
        private TextMeshProUGUI _partDescText;

        [Tooltip("레벨 표시 텍스트 (예: Lv. 2  ➔  Lv. 3)")]
        [SerializeField]
        private TextMeshProUGUI _partLevelText;

        [Tooltip("스탯 변화 미리보기 텍스트 (PartsDataSO의 단계별 설명 출력)")]
        [SerializeField]
        private TextMeshProUGUI _statEffectText;

        [Tooltip("특수 해금 효과 안내 텍스트 (선택사항)")]
        [SerializeField]
        private TextMeshProUGUI _specialEffectText;

        [Tooltip("강화 실행 버튼")]
        [SerializeField]
        private Button _upgradeButton;

        [Tooltip("강화 버튼 내부 텍스트 (강화하기 / 재료 부족 / 최대 강화 완료)")]
        [SerializeField]
        private TextMeshProUGUI _upgradeButtonText;

        [Tooltip("강화 가능 시 적용할 버튼 색상 세트")]
        [SerializeField]
        private ColorBlock _upgradeButtonActiveColors = CreateDefaultActiveButtonColors();

        [Tooltip("강화 가능 시 버튼 텍스트 색상")]
        [SerializeField]
        private Color _activeTextColor = Color.white;

        [Tooltip("강화 불가 시 버튼 텍스트 색상")]
        [SerializeField]
        private Color _disabledTextColor = new Color(0.2f, 0.2f, 0.2f, 0.6f);

        [Header("Forge Effect")]
        [Tooltip("강화 버튼을 누르면 그룬이 모루를 내려치는 연출. 마지막 타격에서 강화가 확정된다. 비우면 즉시 확정")]
        [SerializeField]
        private GrunForgeSequence _forgeSequence;

        [Tooltip("강화 연출을 감싸는 컷씬(HUD · NPC 하이라이트 끄기, 카메라 확대, 끝나면 복구). 컷씬의 GrunForge 신호에 BeginForge를 연결한다. 비우면 컷씬 없이 연출만 재생")]
        [SerializeField]
        private CutsceneAsset _reinforceCutscene;

        [Header("Bottom: Part Select Tabs")]
        [Tooltip("하단 5개 부품 선택 버튼 매핑")]
        [SerializeField]
        private PartTabButton[] _tabButtons;

        [Header("Unlock")]
        [Tooltip("이 영구 플래그가 있어야 강화 창이 열립니다. 비우면 항상 열림.")]
        [SerializeField]
        private string _unlockFlag = QuestFlags.Q1DebriefDone;

        private WeaponPartsType _currentPart = WeaponPartsType.Barrel;
        private WeaponPartsType _pendingPart;   // 연출을 시작한 시점에 누른 부품 (연출 중 탭을 바꿔도 이 부품이 강화된다)
        private WeaponUpgradeService _upgradeService;
        private readonly Dictionary<WeaponPartsType, GameObject> _tabBorders = new();
        private ColorBlock _upgradeButtonDefaultColors;
        private bool _hasCachedDefaultColors;

        public bool IsOpen => _windowPanel != null && _windowPanel.activeSelf;

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

            if (_upgradeButton != null)
            {
                _upgradeButtonDefaultColors = _upgradeButton.colors;
                _hasCachedDefaultColors = true;
                _upgradeButton.onClick.AddListener(OnUpgradeButtonClicked);
            }

            SetupTabHighlightBorders();

            // 하단 탭 버튼 클릭 리스너 연결
            if (_tabButtons != null)
            {
                for (int i = 0; i < _tabButtons.Length; i++)
                {
                    WeaponPartsType type = _tabButtons[i].PartType;
                    if (_tabButtons[i].Button != null)
                    {
                        _tabButtons[i].Button.onClick.AddListener(() => SelectPart(type));
                    }
                }
            }
        }

        private void Start()
        {
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
            NpcInteractable.SetBlocked(this, false);

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(Close);
            }

            if (_upgradeButton != null)
            {
                _upgradeButton.onClick.RemoveListener(OnUpgradeButtonClicked);
            }
        }

        // 플래그 미지정이거나 GameManager/진행도가 없으면(에디터 테스트 씬) 열림으로 취급합니다.
        private bool IsUnlocked
        {
            get
            {
                if (string.IsNullOrEmpty(_unlockFlag))
                {
                    return true;
                }

                var quests = GameManager.Instance != null ? GameManager.Instance.Progress?.Quests : null;
                return quests == null || quests.HasFlag(_unlockFlag);
            }
        }

        public void Open()
        {
            if (!IsUnlocked)
            {
                Debug.Log($"[WeaponUpgradeUI] {_unlockFlag} 플래그가 없어 강화 창을 열 수 없습니다.");
                return;
            }

            if (_windowPanel != null)
            {
                _windowPanel.SetActive(true);
                // 창이 열려 있는 동안 E로 NPC 대화가 다시 시작되지 않게 막는다
                NpcInteractable.SetBlocked(this, true);
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

            if (_forgeSequence != null)
            {
                _forgeSequence.Cancel();
            }

            // 연출 중에 창이 닫히면 컷씬도 끝내 HUD · 카메라 · 조작을 되돌린다
            CutsceneDirector director = CutsceneDirector.Current;
            if (_reinforceCutscene != null && director != null && director.Playing == _reinforceCutscene)
            {
                director.Abort();
            }

            if (_windowPanel != null)
            {
                _windowPanel.SetActive(false);
            }

            NpcInteractable.SetBlocked(this, false);

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
        /// 특정 부품을 선택하고 UI 뷰를 해당 부품 정보로 갱신합니다.
        /// </summary>
        public void SelectPart(WeaponPartsType partType)
        {
            _currentPart = partType;
            RefreshUI();
        }

        /// <summary>
        /// 현재 선택된 부품의 레벨, 상단 재료 슬롯, 중앙 스프라이트 및 스탯 효과를 갱신합니다.
        /// </summary>
        public void RefreshUI()
        {
            ProgressData progress = GameManager.Instance != null ? GameManager.Instance.Progress : null;
            WeaponUpgradeState upgradeState = progress != null ? progress.WeaponParts : null;

            int currentLevel = upgradeState != null ? Mathf.Clamp(upgradeState.GetLevel(_currentPart), 1, 5) : 1;
            bool isMaxLevel = currentLevel >= 5;

            // 1. 하단 탭 하이라이트 갱신
            UpdateTabHighlights();

            // 2. PartsDataSO에서 중앙 부품 메타 정보(이름, 스프라이트) 갱신
            PartsDataSO partData = null;
            if (_upgradeCatalog != null)
            {
                _upgradeCatalog.TryGetPartsData(_currentPart, out partData);
            }

            if (_partBigImage != null)
            {
                Sprite icon = partData != null ? partData.Icon : null;
                _partBigImage.sprite = icon;
                _partBigImage.enabled = icon != null;
            }

            if (_partNameText != null)
            {
                _partNameText.text = partData != null && !string.IsNullOrEmpty(partData.PartsName)
                    ? partData.PartsName
                    : _currentPart.ToString();
            }

            if (_partDescText != null)
            {
                _partDescText.text = string.Empty;
            }

            // 3. 중앙 레벨 텍스트 갱신
            if (_partLevelText != null)
            {
                if (isMaxLevel)
                {
                    _partLevelText.text = $"<color=#FFD700>Lv.{currentLevel} (최대 강화)</color>";
                }
                else
                {
                    _partLevelText.text =
                        $"Lv.{currentLevel}  <color=#4CAF50>-></color>  <color=#55FF55>Lv.{currentLevel + 1}</color>";
                }
            }

            // 4. 스탯 향상치 미리보기 텍스트 갱신 (PartsDataSO 기반)
            UpdateStatEffectPreview(partData, currentLevel, isMaxLevel);

            // 5. 상단 재료 슬롯 갱신 & 강화 가능 여부 판정
            bool canAfford = true;

            if (_upgradeCatalog == null)
            {
                ClearAllMaterialSlots();
                SetUpgradeButtonState(false, "카탈로그 없음");
                return;
            }

            if (isMaxLevel)
            {
                ClearAllMaterialSlots();
                if (_maxLevelBanner != null) _maxLevelBanner.SetActive(true);
                SetUpgradeButtonState(false, "최대 강화 완료");
                return;
            }

            if (_maxLevelBanner != null)
            {
                _maxLevelBanner.SetActive(false);
            }

            // 부품 레시피 및 다음 단계 필요 재료 가져오기
            if (!_upgradeCatalog.TryGetPartRecipe(_currentPart, out WeaponPartsRecipe recipe) ||
                !_upgradeCatalog.TryGetUpgradeStepCost(currentLevel, out UpgradeStepCost stepCost))
            {
                ClearAllMaterialSlots();
                SetUpgradeButtonState(false, "강화 정보 없음");
                return;
            }

            ItemData coreMaterial = _upgradeCatalog.CoreMaterial;

            // 고정 슬롯 5개 매핑: [0: Main, 1: Sub1, 2: Sub2, 3: Sub3, 4: Core]
            (ItemData item, int requiredAmount)[] slotTargets = new (ItemData, int)[]
            {
                (recipe.Main, stepCost.MainAmount),
                (recipe.Sub1, stepCost.Sub1Amount),
                (recipe.Sub2, stepCost.Sub2Amount),
                (recipe.Sub3, stepCost.Sub3Amount),
                (coreMaterial, stepCost.CoreAmount)
            };

            for (int i = 0; i < 5; i++)
            {
                if (i >= _materialSlots.Length || _materialSlots[i] == null)
                {
                    continue;
                }

                (ItemData item, int required) = slotTargets[i];
                if (item != null && required > 0)
                {
                    int storedCount = GameManager.Instance != null ? GameManager.Instance.GetItemCount(item) : 0;
                    _materialSlots[i].Bind(item, required, storedCount);

                    if (storedCount < required)
                    {
                        canAfford = false;
                    }
                }
                else
                {
                    _materialSlots[i].Clear();
                }
            }

            // 6. 강화 버튼 활성화 설정
            SetUpgradeButtonState(canAfford, canAfford ? "강화하기" : "재료 부족");
        }

        /// <summary>
        /// 강화 버튼을 클릭했을 때 호출됩니다.
        /// </summary>
        private void OnUpgradeButtonClicked()
        {
            if (!IsUnlocked)
            {
                return;
            }

            if (GameManager.Instance == null || GameManager.Instance.Progress == null || _upgradeCatalog == null)
            {
                Debug.LogWarning("[WeaponUpgradeUI] GameManager 또는 Catalog를 찾을 수 없습니다.");
                return;
            }

            if (_forgeSequence == null)
            {
                ApplyUpgrade(_currentPart);
                return;
            }

            // 연출 중에 탭을 바꿔도 누른 부품이 강화되도록 클릭 시점의 부품을 넘긴다
            if (_forgeSequence.IsPlaying)
            {
                return;
            }

            _pendingPart = _currentPart;
            SetUpgradeButtonState(false, "강화 중...");

            CutsceneDirector director = CutsceneDirector.Current;
            if (_reinforceCutscene != null && director != null && director.Play(_reinforceCutscene))
            {
                return;
            }

            BeginForge();
        }

        /// <summary>
        /// 망치질 연출을 시작하고 마지막 타격에서 강화를 확정한다. 강화 컷씬의 GrunForge 신호(CutsceneSignal)에 연결한다.
        /// </summary>
        public void BeginForge()
        {
            WeaponPartsType part = _pendingPart;
            _forgeSequence.Play(() => ApplyUpgrade(part));
        }

        private void ApplyUpgrade(WeaponPartsType part)
        {
            _upgradeService ??= new WeaponUpgradeService(
                GameManager.Instance.UpgradeState,
                _upgradeCatalog,
                GameManager.Instance);

            bool success = _upgradeService.TryUpgrade(part, isShelter: true);
            if (success)
            {
                // 강화 성공 시 세이브 데이터에 영구 반영
                GameManager.Instance.CommitUpgrade();
                ShotgunAndRun.Player.Player.PlayerInstance?.SetWeaponUpgradeState(GameManager.Instance.UpgradeState);
                Debug.Log(
                    $"[WeaponUpgradeUI] {part} 강화 성공! 새 레벨: {GameManager.Instance.UpgradeState.GetLevel(part)}");

                RefreshUI();
                ShelterStorageUI.RequestRefresh();
            }
            else
            {
                Debug.LogWarning($"[WeaponUpgradeUI] {part} 강화 실패.");
                RefreshUI();
            }
        }

        /// <summary>
        /// PartsDataSO의 StepUpgradeDescription을 기반으로 스탯 강화 효과 안내를 갱신합니다.
        /// </summary>
        private void UpdateStatEffectPreview(PartsDataSO partData, int currentLevel, bool isMaxLevel)
        {
            if (_statEffectText == null)
            {
                return;
            }

            if (isMaxLevel)
            {
                _statEffectText.text = "<color=#FFD700>최대 레벨에 도달했습니다.</color>";
            }
            else
            {
                int nextIndex = currentLevel - 1; // Lv1 -> index 0 (Lv2 효과)
                if (partData != null && partData.StepUpgradeDescription != null && nextIndex >= 0 &&
                    nextIndex < partData.StepUpgradeDescription.Length)
                {
                    _statEffectText.text = partData.StepUpgradeDescription[nextIndex];
                }
                else
                {
                    _statEffectText.text = "강화 효과 정보가 없습니다.";
                }
            }

            if (_specialEffectText != null)
            {
                _specialEffectText.gameObject.SetActive(false);
            }
        }

        private void SetUpgradeButtonState(bool interactable, string label)
        {
            if (_upgradeButton != null)
            {
                if (!_hasCachedDefaultColors)
                {
                    _upgradeButtonDefaultColors = _upgradeButton.colors;
                    _hasCachedDefaultColors = true;
                }

                if (_upgradeButtonActiveColors.colorMultiplier <= 0f)
                {
                    _upgradeButtonActiveColors = CreateDefaultActiveButtonColors();
                }

                _upgradeButton.colors = interactable ? _upgradeButtonActiveColors : _upgradeButtonDefaultColors;
                _upgradeButton.interactable = interactable;
            }

            if (_upgradeButtonText != null)
            {
                _upgradeButtonText.text = label;
                _upgradeButtonText.color = interactable ? _activeTextColor : _disabledTextColor;
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

        private void SetupTabHighlightBorders()
        {
            if (_tabButtons == null) return;

            Color borderColor = new Color(1f, 0.9f, 0.25f, 1f); // 인벤토리 슬롯과 동일한 골드 옐로우 테두리
            float thickness = 2.5f;

            for (int i = 0; i < _tabButtons.Length; i++)
            {
                var tab = _tabButtons[i];
                if (tab.Button == null) continue;

                // 인스펙터에 ActiveIndicator가 이미 지정되어 있다면 그것을 우선 사용
                if (tab.ActiveIndicator != null)
                {
                    _tabBorders[tab.PartType] = tab.ActiveIndicator.gameObject;
                    continue;
                }

                // 없다면 인벤토리 SlotUI와 동일한 4변 테두리를 런타임에 자동 생성
                GameObject borderRoot = new GameObject("Tab_Border_Highlight", typeof(RectTransform));
                borderRoot.transform.SetParent(tab.Button.transform, false);

                RectTransform rootRect = borderRoot.GetComponent<RectTransform>();
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.pivot = new Vector2(0.5f, 0.5f);
                rootRect.anchoredPosition = Vector2.zero;
                rootRect.sizeDelta = Vector2.zero;

                CreateBorderLine(borderRoot.transform, "Border_Top", new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(0.5f, 1f), new Vector2(0f, thickness), borderColor);
                CreateBorderLine(borderRoot.transform, "Border_Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f),
                    new Vector2(0.5f, 0f), new Vector2(0f, thickness), borderColor);
                CreateBorderLine(borderRoot.transform, "Border_Left", new Vector2(0f, 0f), new Vector2(0f, 1f),
                    new Vector2(0f, 0.5f), new Vector2(thickness, 0f), borderColor);
                CreateBorderLine(borderRoot.transform, "Border_Right", new Vector2(1f, 0f), new Vector2(1f, 1f),
                    new Vector2(1f, 0.5f), new Vector2(thickness, 0f), borderColor);

                borderRoot.SetActive(false);
                _tabBorders[tab.PartType] = borderRoot;
            }
        }

        private static void CreateBorderLine(Transform parent, string lineName, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 sizeDelta, Color color)
        {
            GameObject lineObj = new GameObject(lineName, typeof(RectTransform), typeof(Image));
            lineObj.transform.SetParent(parent, false);

            RectTransform rect = lineObj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = sizeDelta;

            Image img = lineObj.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        private void UpdateTabHighlights()
        {
            if (_tabButtons == null)
            {
                return;
            }

            for (int i = 0; i < _tabButtons.Length; i++)
            {
                WeaponPartsType partType = _tabButtons[i].PartType;
                bool isSelected = partType == _currentPart;

                if (_tabBorders.TryGetValue(partType, out GameObject border) && border != null)
                {
                    border.SetActive(isSelected);
                }
                else if (_tabButtons[i].ActiveIndicator != null)
                {
                    _tabButtons[i].ActiveIndicator.gameObject.SetActive(isSelected);
                }
            }
        }
    }
}
