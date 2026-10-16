using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Consumable;
using ShotgunAndRun.Weapon;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace InventorySystem
{
    /// <summary>
    /// 인벤토리 및 루트 상자 등에서 아이템 호버 시 상세 정보를 표시하는 팝업 툴팁 UI
    /// - 프리팹(Resources/UI/ItemTooltip) 기반의 안정적인 인스펙터 워크플로우 지원
    /// - 화면 경계 감지 스마트 피벗(Smart Pivot) 및 클램핑(Boundary Clamping) 지원
    /// - 미세 호버 딜레이 및 마우스 커서 추적 지원
    /// - 재료 아이템의 무기 강화 파츠 및 소모품 제작 주요 사용처 안내 지원
    /// - Zero-GC 재사용 버퍼 및 런타임 빌드 안전성 보장
    /// </summary>
    public class ItemTooltipUI : MonoBehaviour
    {
        private static ItemTooltipUI _instance;
        private static bool _isApplicationQuitting;

        public static ItemTooltipUI Instance
        {
            get
            {
                if (_isApplicationQuitting) return null;

                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<ItemTooltipUI>();
                    if (_instance == null)
                    {
                        var prefab = Resources.Load<GameObject>("UI/ItemTooltip");
                        if (prefab != null)
                        {
                            var instantiated = Instantiate(prefab);
                            _instance = instantiated.GetComponent<ItemTooltipUI>();
                            if (_instance != null)
                            {
                                if (Application.isPlaying)
                                {
                                    DontDestroyOnLoad(instantiated);
                                }

                                return _instance;
                            }
                        }

                        Debug.LogError("[ItemTooltipUI] Resources/UI/ItemTooltip 프리팹을 찾을 수 없습니다.");
                    }
                }

                return _instance;
            }
        }

        [Header("Hierarchy & Canvas")]
        [SerializeField] private Canvas _rootCanvas;
        [SerializeField] private CanvasScaler _canvasScaler;
        [SerializeField] private GameObject _tooltipRoot;
        [SerializeField] private RectTransform _tooltipRectTransform;
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("UI Elements")]
        [SerializeField] private Image _iconImage;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private GameObject _dividerObject;

        [Header("Usage Section UI Elements")]
        [SerializeField] private GameObject _usageSectionRoot;
        [SerializeField] private GameObject _usageDivider;
        [SerializeField] private TextMeshProUGUI _usageTitleText;
        [SerializeField] private TextMeshProUGUI _usageContentText;

        [Header("Catalogs & Master Data")]
        [Tooltip("무기 강화 레시피 카탈로그 (비워둘 시 자동 탐색)")]
        [SerializeField] private WeaponUpgradeCatalogSO _upgradeCatalog;
        [Tooltip("소모품 제작 레시피 카탈로그 (비워둘 시 자동 탐색)")]
        [SerializeField] private ConsumableCraftingCosts _craftingCatalog;

        [Header("Settings")]
        [Tooltip("마우스 호버 후 툴팁이 나타나기까지의 대기 시간(초)")]
        [SerializeField, Range(0f, 0.5f)] private float _hoverDelay = 0.12f;
        [Tooltip("마우스 커서로부터의 기본 여백(px)")]
        [SerializeField] private Vector2 _cursorOffset = new Vector2(18f, 18f);
        [Tooltip("화면 가장자리 여백(px)")]
        [SerializeField] private float _screenPadding = 14f;
        [Tooltip("툴팁이 표시된 동안 마우스 이동을 실시간 추적할지 여부")]
        [SerializeField] private bool _followMouse = true;

        public bool IsVisible => _tooltipRoot != null && _tooltipRoot.activeSelf && _canvasGroup != null && _canvasGroup.alpha > 0.01f;
        public ItemData CurrentItem => _currentItem;

        private ItemData _currentItem;
        private Coroutine _showRoutine;
        private bool _isShowing;

        // GC 할당 방지를 위한 재사용 캐시 버퍼
        private bool _isCatalogsLoaded;
        private readonly List<string> _cachedWeaponParts = new(8);
        private readonly List<string> _cachedConsumables = new(8);
        private readonly StringBuilder _usageStringBuilder = new(128);

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                transform.SetParent(null);
                if (Application.isPlaying)
                {
                    DontDestroyOnLoad(gameObject);
                }
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            EnsureTooltipHierarchy();
            EnsureCatalogs();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            _isApplicationQuitting = true;
        }

        private void Update()
        {
            if (_isShowing && _followMouse && IsVisible)
            {
                UpdatePosition(GetMouseScreenPosition());
            }
        }

        private void EnsureTooltipHierarchy()
        {
            if (_rootCanvas == null) _rootCanvas = GetComponent<Canvas>();
            if (_tooltipRoot != null)
            {
                if (_tooltipRectTransform == null) _tooltipRectTransform = _tooltipRoot.GetComponent<RectTransform>();
                if (_canvasGroup == null) _canvasGroup = _tooltipRoot.GetComponent<CanvasGroup>();
            }

            HideImmediate();
        }

        #region Public Show / Hide API

        /// <summary>
        /// 슬롯 호버 시 아이템 정보를 툴팁에 표시합니다.
        /// </summary>
        public void Show(ItemData itemData, Vector2 screenPosition)
        {
            if (itemData == null)
            {
                Hide();
                return;
            }

            // 아이템 드래그 중인 상태라면 툴팁을 띄우지 않음
            if (ItemDragHandler.Instance != null && ItemDragHandler.Instance.IsDragging)
            {
                Hide();
                return;
            }

            EnsureTooltipHierarchy();
            EnsureRootCanvasOrder();

            _currentItem = itemData;
            _isShowing = true;

            UpdateContent(itemData);
            UpdatePosition(screenPosition);

            if (_showRoutine != null)
            {
                StopCoroutine(_showRoutine);
            }

            if (_hoverDelay > 0f)
            {
                _showRoutine = StartCoroutine(ShowDelayedRoutine());
            }
            else
            {
                ShowImmediate();
            }
        }

        /// <summary>
        /// 슬롯 호버 시 소모품 정보를 툴팁에 표시합니다.
        /// </summary>
        public void Show(ConsumableData consumableData, Vector2 screenPosition)
        {
            if (consumableData == null)
            {
                Hide();
                return;
            }

            if (ItemDragHandler.Instance != null && ItemDragHandler.Instance.IsDragging)
            {
                Hide();
                return;
            }

            EnsureTooltipHierarchy();
            EnsureRootCanvasOrder();

            _currentItem = null;
            _isShowing = true;

            UpdateContentConsumable(consumableData);
            UpdatePosition(screenPosition);

            if (_showRoutine != null)
            {
                StopCoroutine(_showRoutine);
            }

            if (_hoverDelay > 0f)
            {
                _showRoutine = StartCoroutine(ShowDelayedRoutine());
            }
            else
            {
                ShowImmediate();
            }
        }

        /// <summary>
        /// 툴팁을 숨깁니다.
        /// </summary>
        public void Hide()
        {
            _isShowing = false;
            _currentItem = null;

            if (_showRoutine != null)
            {
                StopCoroutine(_showRoutine);
                _showRoutine = null;
            }

            HideImmediate();
        }

        private IEnumerator ShowDelayedRoutine()
        {
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
            if (_tooltipRoot != null) _tooltipRoot.SetActive(true);

            float timer = 0f;
            while (timer < _hoverDelay)
            {
                timer += Time.unscaledDeltaTime;
                if (_followMouse)
                {
                    UpdatePosition(GetMouseScreenPosition());
                }
                yield return null;
            }

            ShowImmediate();
            _showRoutine = null;
        }

        private void ShowImmediate()
        {
            if (_tooltipRoot != null)
            {
                _tooltipRoot.SetActive(true);
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
            }

            UpdatePosition(GetMouseScreenPosition());
        }

        private void HideImmediate()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
            }

            if (_tooltipRoot != null)
            {
                _tooltipRoot.SetActive(false);
            }
        }

        #endregion

        #region Content & Positioning Algorithm

        private void UpdateContent(ItemData item)
        {
            if (_iconImage != null)
            {
                if (item.Icon != null)
                {
                    _iconImage.gameObject.SetActive(true);
                    _iconImage.sprite = item.Icon;
                }
                else
                {
                    _iconImage.gameObject.SetActive(false);
                }
            }

            if (_nameText != null)
            {
                _nameText.text = !string.IsNullOrEmpty(item.ItemName) ? item.ItemName : "알 수 없는 아이템";
            }

            if (_descriptionText != null)
            {
                bool hasDesc = !string.IsNullOrWhiteSpace(item.Description);
                _descriptionText.gameObject.SetActive(hasDesc);
                if (hasDesc)
                {
                    _descriptionText.text = item.Description;
                }
            }

            if (_dividerObject != null)
            {
                bool hasDesc = !string.IsNullOrWhiteSpace(item.Description);
                _dividerObject.SetActive(hasDesc);
            }

            // 주요 사용처 (무기 강화 및 소모품 제작) 역추적 및 바인딩
            string usageText = BuildUsageDescription(item);
            bool hasUsage = !string.IsNullOrEmpty(usageText);

            if (_usageSectionRoot != null)
            {
                _usageSectionRoot.SetActive(hasUsage);
            }

            if (_usageContentText != null && hasUsage)
            {
                _usageContentText.text = usageText;
            }

            // 레이아웃 즉시 강제 갱신하여 툴팁의 정확한 rect 크기를 산출
            if (_tooltipRectTransform != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_tooltipRectTransform);
            }
        }

        private void UpdateContentConsumable(ConsumableData consumable)
        {
            if (_iconImage != null)
            {
                if (consumable.Icon != null)
                {
                    _iconImage.gameObject.SetActive(true);
                    _iconImage.sprite = consumable.Icon;
                }
                else
                {
                    _iconImage.gameObject.SetActive(false);
                }
            }

            if (_nameText != null)
            {
                _nameText.text = !string.IsNullOrEmpty(consumable.ConsumableName) ? consumable.ConsumableName : "소모품";
            }

            if (_descriptionText != null)
            {
                bool hasDesc = !string.IsNullOrWhiteSpace(consumable.Description);
                _descriptionText.gameObject.SetActive(hasDesc);
                if (hasDesc)
                {
                    _descriptionText.text = consumable.Description;
                }
            }

            if (_dividerObject != null)
            {
                bool hasDesc = !string.IsNullOrWhiteSpace(consumable.Description);
                _dividerObject.SetActive(hasDesc);
            }

            if (_usageSectionRoot != null)
            {
                _usageSectionRoot.SetActive(false);
            }

            if (_tooltipRectTransform != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_tooltipRectTransform);
            }
        }

        #region Usage Parsing Helpers

        private void EnsureCatalogs()
        {
            if (_isCatalogsLoaded) return;
            _isCatalogsLoaded = true;

            if (_upgradeCatalog == null)
            {
                _upgradeCatalog = Resources.Load<WeaponUpgradeCatalogSO>("Catalogs/WeaponUpgradeCatalog");
                if (_upgradeCatalog == null)
                {
                    _upgradeCatalog = Resources.Load<WeaponUpgradeCatalogSO>("WeaponUpgradeCatalog");
                }
                if (_upgradeCatalog == null)
                {
                    var catalogs = Resources.FindObjectsOfTypeAll<WeaponUpgradeCatalogSO>();
                    if (catalogs != null && catalogs.Length > 0) _upgradeCatalog = catalogs[0];
                }
            }

            if (_craftingCatalog == null)
            {
                _craftingCatalog = Resources.Load<ConsumableCraftingCosts>("Catalogs/ConsumableCraftingCosts");
                if (_craftingCatalog == null)
                {
                    _craftingCatalog = Resources.Load<ConsumableCraftingCosts>("ConsumableCraftingCosts");
                }
                if (_craftingCatalog == null)
                {
                    var catalogs = Resources.FindObjectsOfTypeAll<ConsumableCraftingCosts>();
                    if (catalogs != null && catalogs.Length > 0) _craftingCatalog = catalogs[0];
                }
            }
        }

        public void SetCatalogs(WeaponUpgradeCatalogSO upgradeCatalog, ConsumableCraftingCosts craftingCatalog)
        {
            _upgradeCatalog = upgradeCatalog;
            _craftingCatalog = craftingCatalog;
            _isCatalogsLoaded = true;
        }

        private string BuildUsageDescription(ItemData item)
        {
            if (item == null) return null;

            EnsureCatalogs();

            _cachedWeaponParts.Clear();
            _cachedConsumables.Clear();
            _usageStringBuilder.Clear();

            // 1. 무기 강화 파츠 역추적
            if (_upgradeCatalog != null)
            {
                if (_upgradeCatalog.CoreMaterial == item)
                {
                    _cachedWeaponParts.Add("모든 파츠 (코어 재료)");
                }

                foreach (WeaponPartsType partType in (WeaponPartsType[])Enum.GetValues(typeof(WeaponPartsType)))
                {
                    if (_upgradeCatalog.TryGetPartRecipe(partType, out var recipe) && recipe != null)
                    {
                        if (recipe.Main == item || recipe.Sub1 == item || recipe.Sub2 == item || recipe.Sub3 == item)
                        {
                            string partName = _upgradeCatalog.TryGetPartsData(partType, out var partsData) && !string.IsNullOrEmpty(partsData?.PartsName)
                                ? partsData.PartsName
                                : GetDefaultPartName(partType);

                            if (!_cachedWeaponParts.Contains(partName))
                            {
                                _cachedWeaponParts.Add(partName);
                            }
                        }
                    }
                }
            }

            // 2. 소모품 제작 역추적
            if (_craftingCatalog != null)
            {
                var recipes = _craftingCatalog.GetAllRecipes();
                if (recipes != null)
                {
                    int recipeCount = recipes.Count;
                    for (int i = 0; i < recipeCount; i++)
                    {
                        var recipe = recipes[i];
                        if (recipe.MaterialCosts != null)
                        {
                            int matCount = recipe.MaterialCosts.Length;
                            for (int m = 0; m < matCount; m++)
                            {
                                if (recipe.MaterialCosts[m].ItemData == item)
                                {
                                    string cName = GetConsumableDisplayName(recipe.ConsumableData);
                                    if (!string.IsNullOrEmpty(cName) && !_cachedConsumables.Contains(cName))
                                    {
                                        _cachedConsumables.Add(cName);
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            if (_cachedWeaponParts.Count == 0 && _cachedConsumables.Count == 0)
            {
                return null;
            }

            if (_cachedWeaponParts.Count > 0)
            {
                _usageStringBuilder.Append("• 무기 강화 : ");
                for (int i = 0; i < _cachedWeaponParts.Count; i++)
                {
                    if (i > 0) _usageStringBuilder.Append(", ");
                    _usageStringBuilder.Append(_cachedWeaponParts[i]);
                }
            }

            if (_cachedConsumables.Count > 0)
            {
                if (_usageStringBuilder.Length > 0) _usageStringBuilder.AppendLine();
                _usageStringBuilder.Append("• 소모품 제작 : ");
                for (int i = 0; i < _cachedConsumables.Count; i++)
                {
                    if (i > 0) _usageStringBuilder.Append(", ");
                    _usageStringBuilder.Append(_cachedConsumables[i]);
                }
            }

            return _usageStringBuilder.ToString();
        }

        private static string GetDefaultPartName(WeaponPartsType partType)
        {
            return partType switch
            {
                WeaponPartsType.Barrel => "총열 / 총구",
                WeaponPartsType.Chamber => "약실",
                WeaponPartsType.Belt => "멜빵",
                WeaponPartsType.Stock => "개머리판",
                WeaponPartsType.Ammo => "탄창",
                _ => partType.ToString()
            };
        }

        private static string GetConsumableDisplayName(ConsumableData data)
        {
            if (data == null) return null;

            string rawName = data.ConsumableName;
            return rawName switch
            {
                "Armor" => "아머 (방탄복)",
                "FlashBang" => "섬광탄",
                "FragmentGrenade" => "파편 수류탄",
                "LargeKit" => "구급상자",
                "SmallKit" => "응급 처치 키트",
                "Adrenaline" => "아드레날린",
                _ => !string.IsNullOrEmpty(rawName) ? rawName : "소모품"
            };
        }

        #endregion

        /// <summary>
        /// 마우스 위치 및 화면 경계를 감지하여 스마트 피벗 전환과 Safe-Area 클램핑을 적용합니다.
        /// </summary>
        public void UpdatePosition(Vector2 screenPosition)
        {
            if (_tooltipRectTransform == null || _rootCanvas == null) return;

            var canvasRect = _rootCanvas.transform as RectTransform;
            if (canvasRect == null) return;

            Camera cam = _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _rootCanvas.worldCamera;

            // 1. 스크린 좌표를 캔버스 로컬 좌표로 변환
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, cam, out Vector2 localMousePos))
            {
                return;
            }

            // 툴팁의 현재 계산된 실제 크기
            float width = _tooltipRectTransform.rect.width;
            float height = _tooltipRectTransform.rect.height;
            if (width <= 0f || height <= 0f)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_tooltipRectTransform);
                width = _tooltipRectTransform.rect.width;
                height = _tooltipRectTransform.rect.height;
            }

            // 캔버스 바운딩 영역 (피벗 독립적)
            float canvasMinX = canvasRect.rect.xMin + _screenPadding;
            float canvasMaxX = canvasRect.rect.xMax - _screenPadding;
            float canvasMinY = canvasRect.rect.yMin + _screenPadding;
            float canvasMaxY = canvasRect.rect.yMax - _screenPadding;

            // 2. 스마트 피벗 및 오프셋 결정
            Vector2 pivot = Vector2.zero;
            Vector2 offset = Vector2.zero;

            // X축: 오른쪽 공간 검사
            if (localMousePos.x + _cursorOffset.x + width > canvasMaxX)
            {
                // 오른쪽 공간 부족 -> 마우스 좌측으로 전개 (Pivot.X = 1)
                pivot.x = 1f;
                offset.x = -_cursorOffset.x;
            }
            else
            {
                // 오른쪽 공간 충분 -> 마우스 우측으로 전개 (Pivot.X = 0)
                pivot.x = 0f;
                offset.x = _cursorOffset.x;
            }

            // Y축: 아래쪽 공간 검사
            if (localMousePos.y - _cursorOffset.y - height < canvasMinY)
            {
                // 아래쪽 공간 부족 -> 마우스 위쪽으로 전개 (Pivot.Y = 0)
                pivot.y = 0f;
                offset.y = _cursorOffset.y;
            }
            else
            {
                // 아래쪽 공간 충분 -> 마우스 아래쪽으로 전개 (Pivot.Y = 1)
                pivot.y = 1f;
                offset.y = -_cursorOffset.y;
            }

            _tooltipRectTransform.pivot = pivot;

            // 3. 목표 위치 산출 및 2차 Safe-Area 클램핑
            Vector2 targetPos = localMousePos + offset;

            float tooltipLeft = targetPos.x - pivot.x * width;
            float tooltipRight = targetPos.x + (1f - pivot.x) * width;
            float tooltipBottom = targetPos.y - pivot.y * height;
            float tooltipTop = targetPos.y + (1f - pivot.y) * height;

            if (tooltipLeft < canvasMinX) targetPos.x += (canvasMinX - tooltipLeft);
            if (tooltipRight > canvasMaxX) targetPos.x -= (tooltipRight - canvasMaxX);
            if (tooltipBottom < canvasMinY) targetPos.y += (canvasMinY - tooltipBottom);
            if (tooltipTop > canvasMaxY) targetPos.y -= (tooltipTop - canvasMaxY);

            _tooltipRectTransform.anchoredPosition = targetPos;
        }

        private Vector2 GetMouseScreenPosition()
        {
            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }

            return Input.mousePosition;
        }

        private void EnsureRootCanvasOrder()
        {
            if (_rootCanvas != null)
            {
                _rootCanvas.overrideSorting = true;
                _rootCanvas.sortingOrder = 9999;
            }

            if (_tooltipRoot != null && _rootCanvas != null && _tooltipRoot.transform.parent != _rootCanvas.transform)
            {
                _tooltipRoot.transform.SetParent(_rootCanvas.transform, false);
            }

            if (_tooltipRoot != null)
            {
                _tooltipRoot.transform.SetAsLastSibling();
            }
        }

        #endregion
    }
}
