using System.Collections;
using System.Collections.Generic;
using ShotgunAndRun.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShotgunAndRun.UI
{
    /// <summary>
    /// 런(던전 탐색/전투) 종료 시 경과 시간, 킬 수, 획득 XP, 탈출/사망 결과 및 전리품 정산을 표시하는 결과창 UI입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameResultUI : MonoBehaviour
    {
        [Header("Root Panel")]
        [Tooltip("결과창 최상위 패널 (런 중에는 비활성화, 런 종료 시 활성화)")]
        [SerializeField] private GameObject _resultPanel;
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Outcome Header")]
        [Tooltip("결과 타이틀 텍스트 (예: ESCAPED / K.I.A.)")]
        [SerializeField] private TextMeshProUGUI _outcomeTitleText;
        [SerializeField] private string _escapedTitle = "탈출 성공 (ESCAPED)";
        [SerializeField] private string _diedTitle = "작전 실패 (K.I.A.)";
        [SerializeField] private Color _escapedColor = new Color(0.25f, 0.95f, 0.4f, 1f);
        [SerializeField] private Color _diedColor = new Color(0.95f, 0.25f, 0.25f, 1f);

        [Header("Outcome Badge / Icon (선택 사항)")]
        [SerializeField] private Image _outcomeBadgeImage;
        [SerializeField] private Sprite _escapedBadgeSprite;
        [SerializeField] private Sprite _diedBadgeSprite;

        [Header("Comment / Cause of Death")]
        [Tooltip("상황 요약 코멘트 또는 사망 원인을 표시하는 텍스트")]
        [SerializeField] private TextMeshProUGUI _commentText;

        [Header("Statistics")]
        [Tooltip("생존 및 탐색 시간 (MM:SS)")]
        [SerializeField] private TextMeshProUGUI _durationText;
        [Tooltip("처치한 적 수 (총합 / 일반 / 엘리트)")]
        [SerializeField] private TextMeshProUGUI _killsText;
        [Tooltip("획득한 경험치 및 배율 정보")]
        [SerializeField] private TextMeshProUGUI _xpText;
        [Tooltip("클리어한 구역 수 (선택 사항)")]
        [SerializeField] private TextMeshProUGUI _clearedZonesText;

        [Header("EXP Progress Bar")]
        [Tooltip("경험치 차오름 및 레벨업 연출 프로그레스바 (선택 사항, 연결 시 작동)")]
        [SerializeField] private ExpProgressBarUI _expProgressBar;

        [Header("Item Settlement (전리품 정산)")]
        [SerializeField] private GameObject _itemSettlementSection;
        [SerializeField] private TextMeshProUGUI _itemSettlementTitle;
        [SerializeField] private Transform _itemsContainer;
        [SerializeField] private ScrollRect _itemsScrollRect;
        [SerializeField] private float _maxItemScrollViewWidth = 540f;
        [Tooltip("아이템 아이콘과 수량을 표시할 프리팹 (비어있으면 기본 엔트리가 자동 생성됩니다)")]
        [SerializeField] private GameObject _itemEntryPrefab;
        [Tooltip("회수한 아이템이 없을 때 표시할 안내 텍스트")]
        [SerializeField] private TextMeshProUGUI _emptyItemsNoticeText;

        [Header("Action Buttons")]
        [Tooltip("쉘터로 복귀하는 버튼")]
        [SerializeField] private Button _returnToShelterButton;
        [SerializeField] private TextMeshProUGUI _returnButtonText;

        [Header("Options")]
        [Tooltip("결과창이 떴을 때 타임스케일을 0으로 일시정지할지 여부")]
        [SerializeField] private bool _pauseTimeScaleOnOpen = true;

        private readonly List<GameObject> _spawnedItemEntries = new List<GameObject>();
        private Coroutine _bindRoutine;
        private bool _isResultShown;

        private void Awake()
        {
            if (_resultPanel != null)
            {
                _resultPanel.SetActive(false);
            }

            if (_itemsScrollRect == null && _itemsContainer != null)
            {
                _itemsScrollRect = _itemsContainer.GetComponentInParent<ScrollRect>();
            }

            if (_returnToShelterButton != null)
            {
                _returnToShelterButton.onClick.AddListener(OnReturnToShelterClicked);
            }
        }

        private void Start()
        {
            if (_bindRoutine != null)
            {
                StopCoroutine(_bindRoutine);
            }
            _bindRoutine = StartCoroutine(BindGameManagerRoutine());
        }

        private void OnEnable()
        {
            if (GameManager.Instance != null)
            {
                SubscribeEvents();

                // 씬 재진입 또는 이미 결과 상태인 경우 즉시 반영
                if (GameManager.Instance.State == GameState.Result && GameManager.Instance.LastRunResult != null)
                {
                    ShowResult(GameManager.Instance.LastRunResult);
                }
            }
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            if (_bindRoutine != null)
            {
                StopCoroutine(_bindRoutine);
                _bindRoutine = null;
            }

            if (_pauseTimeScaleOnOpen && Mathf.Approximately(Time.timeScale, 0f))
            {
                Time.timeScale = 1f;
            }
        }

        private void OnDestroy()
        {
            if (_returnToShelterButton != null)
            {
                _returnToShelterButton.onClick.RemoveListener(OnReturnToShelterClicked);
            }

            if (_pauseTimeScaleOnOpen && Mathf.Approximately(Time.timeScale, 0f))
            {
                Time.timeScale = 1f;
            }
        }

        private IEnumerator BindGameManagerRoutine()
        {
            while (GameManager.Instance == null)
            {
                yield return null;
            }

            SubscribeEvents();

            if (GameManager.Instance.State == GameState.Result && GameManager.Instance.LastRunResult != null)
            {
                ShowResult(GameManager.Instance.LastRunResult);
            }

            _bindRoutine = null;
        }

        private void SubscribeEvents()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RunEnded -= HandleRunEnded;
                GameManager.Instance.RunEnded += HandleRunEnded;
            }

            if (_expProgressBar != null)
            {
                _expProgressBar.OnLevelUp -= HandleLevelUp;
                _expProgressBar.OnLevelUp += HandleLevelUp;
            }
        }

        private void UnsubscribeEvents()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RunEnded -= HandleRunEnded;
            }

            if (_expProgressBar != null)
            {
                _expProgressBar.OnLevelUp -= HandleLevelUp;
            }
        }

        private void HandleLevelUp(int newLevel)
        {
            if (GameManager.Instance != null && GameManager.Instance.Progress != null)
            {
                GameManager.Instance.Progress.PlayerLevel = newLevel;
            }

            ShotgunAndRun.Player.Player.PlayerInstance?.ApplyPlayerLevel(newLevel);
        }

        private void HandleRunEnded(RunResult result)
        {
            // 던전에서는 RunEndSequence가 탈출 · 사망 컷씬 뒤에 ShowResult를 호출한다
            if (RunEndSequence.Active != null)
            {
                return;
            }

            ShowResult(result);
        }

        public void ShowResult(RunResult result)
        {
            if (result == null || _isResultShown)
            {
                return;
            }

            _isResultShown = true;

            if (_resultPanel != null)
            {
                _resultPanel.transform.SetAsLastSibling();
                _resultPanel.SetActive(true);
            }

            // 마우스 커서 활성화
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (_pauseTimeScaleOnOpen)
            {
                Time.timeScale = 0f;
            }

            SetPlayerActionsEnabled(false);

            // 1. 헤더 (탈출 vs 사망)
            bool isExtracted = result.Outcome == RunOutcome.Extracted;
            if (_outcomeTitleText != null)
            {
                _outcomeTitleText.text = isExtracted ? _escapedTitle : _diedTitle;
                _outcomeTitleText.color = isExtracted ? _escapedColor : _diedColor;
            }

            if (_outcomeBadgeImage != null)
            {
                Sprite badge = isExtracted ? _escapedBadgeSprite : _diedBadgeSprite;
                if (badge != null)
                {
                    _outcomeBadgeImage.sprite = badge;
                    _outcomeBadgeImage.gameObject.SetActive(true);
                }
                else
                {
                    _outcomeBadgeImage.gameObject.SetActive(false);
                }
            }

            // 2. 코멘트 / 사망 원인
            if (_commentText != null)
            {
                if (isExtracted)
                {
                    _commentText.text = "성공적으로 던전에서 탈출하였습니다!";
                    _commentText.color = _escapedColor;
                }
                else if (result.IsAbandoned)
                {
                    _commentText.text = "가혹한 전장을 견디지 못하고 전선을 이탈했습니다.";
                    _commentText.color = _diedColor;
                }
                else
                {
                    string cause = string.IsNullOrWhiteSpace(result.DeathCause)
                        ? "치명상을 입고 전사했습니다."
                        : $"[{result.DeathCause}]의 공격에 의해 전사했습니다.";
                    _commentText.text = cause;
                    _commentText.color = _diedColor;
                }
            }

            // 3. 통계 (시간, 킬 수, XP)
            if (_durationText != null)
            {
                int minutes = Mathf.FloorToInt(result.Duration / 60f);
                int seconds = Mathf.FloorToInt(result.Duration % 60f);
                _durationText.text = $"{minutes:00}:{seconds:00}";
            }

            if (_killsText != null)
            {
                int totalKills = result.NormalKills + result.EliteKills;
                _killsText.text = $"{totalKills}명 (일반 {result.NormalKills} / 엘리트 {result.EliteKills})";
            }

            if (_xpText != null)
            {
                if (result.IsAbandoned)
                {
                    _xpText.text = "+0 XP  (작전 포기 페널티)";
                }
                else
                {
                    float multiplier = isExtracted ? 1.5f : 0.3f;
                    _xpText.text = $"+{result.FinalXp} XP  (기본 {result.BaseXp} × {multiplier:0.0})";
                }
            }

            if (_expProgressBar != null)
            {
                int totalXp = (GameManager.Instance != null && GameManager.Instance.Progress != null)
                    ? GameManager.Instance.Progress.PlayerXp
                    : result.FinalXp;
                int gainedXp = result.FinalXp;
                int startXp = Mathf.Max(0, totalXp - gainedXp);

                _expProgressBar.gameObject.SetActive(true);
                _expProgressBar.AnimateExp(startXp, gainedXp);
            }

            if (_clearedZonesText != null)
            {
                _clearedZonesText.text = $"{result.ClearedZoneCount}개 구역";
            }

            // 4. 전리품 정산 (회수 목록 vs 유실 목록)
            UpdateItemSettlement(result, isExtracted);

            // 5. 페이드 인 연출 (CanvasGroup이 있는 경우)
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
            }
        }

        private void UpdateItemSettlement(RunResult result, bool isExtracted)
        {
            if (_itemSettlementSection == null && _itemsContainer == null)
            {
                return;
            }

            // 이전 엔트리 정리
            for (int i = 0; i < _spawnedItemEntries.Count; i++)
            {
                if (_spawnedItemEntries[i] != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(_spawnedItemEntries[i]);
                    }
                    else
                    {
                        DestroyImmediate(_spawnedItemEntries[i]);
                    }
                }
            }
            _spawnedItemEntries.Clear();

            IReadOnlyList<RunItemRecord> items = isExtracted ? result.SecuredItems : result.LostItems;

            if (_itemSettlementTitle != null)
            {
                _itemSettlementTitle.text = isExtracted
                    ? "회수한 전리품 (보관함 이동 및 퀘스트 정산)"
                    : "유실된 전리품 (소지품 전량 손실)";
                _itemSettlementTitle.color = isExtracted ? _escapedColor : _diedColor;
            }

            bool hasItems = items != null && items.Count > 0;

            if (_emptyItemsNoticeText != null)
            {
                _emptyItemsNoticeText.gameObject.SetActive(!hasItems);
                if (!hasItems)
                {
                    _emptyItemsNoticeText.text = isExtracted
                        ? "획득한 전리품이 없습니다."
                        : "유실된 소지품이 없습니다.";
                }
            }

            if (!hasItems || _itemsContainer == null)
            {
                ApplyDynamicScrollViewLayout(false);
                return;
            }

            // 아이템 목록 표시
            for (int i = 0; i < items.Count; i++)
            {
                RunItemRecord record = items[i];
                if (record.Item == null || record.Amount <= 0)
                {
                    continue;
                }

                GameObject entryObj = CreateItemEntry(record, isExtracted);
                if (entryObj != null)
                {
                    _spawnedItemEntries.Add(entryObj);
                }
            }

            ApplyDynamicScrollViewLayout(_spawnedItemEntries.Count > 0);
        }

        private void ApplyDynamicScrollViewLayout(bool hasItems)
        {
            if (_itemsScrollRect == null && _itemsContainer != null)
            {
                _itemsScrollRect = _itemsContainer.GetComponentInParent<ScrollRect>();
            }

            if (_itemsScrollRect == null)
            {
                return;
            }

            _itemsScrollRect.gameObject.SetActive(hasItems);
            if (!hasItems)
            {
                return;
            }

            // 가로 스크롤 전용 설정 (세로 스크롤 비활성화)
            _itemsScrollRect.vertical = false;

            // ItemsContent 레이아웃 패딩 및 정렬 보정 (배경 슬롯 여백 확보)
            HorizontalLayoutGroup hlg = null;
            if (_itemsContainer != null && _itemsContainer.TryGetComponent(out hlg))
            {
                hlg.padding = new RectOffset(10, 10, 10, 10);
                hlg.spacing = 10f;
                hlg.childAlignment = TextAnchor.MiddleCenter;
            }

            // 부모 ItemSection의 레이아웃이 자식을 가로 중앙 정렬하도록 보정
            Transform parentSection = _itemsScrollRect.transform.parent;
            if (parentSection != null && parentSection.TryGetComponent<VerticalLayoutGroup>(out var vlg))
            {
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.childForceExpandWidth = false;
            }

            // 슬롯 엔트리 크기와 레이아웃 패딩을 기반으로 정확한 선호 너비 계산
            int count = _spawnedItemEntries.Count;
            float slotWidth = 70f;
            if (count > 0 && _spawnedItemEntries[0] != null)
            {
                var entryRect = _spawnedItemEntries[0].GetComponent<RectTransform>();
                if (entryRect != null && entryRect.sizeDelta.x > 0f)
                {
                    slotWidth = entryRect.sizeDelta.x;
                }
            }

            float paddingH = hlg != null ? (hlg.padding.left + hlg.padding.right) : 20f;
            float spacing = hlg != null ? hlg.spacing : 10f;
            float calculatedWidth = paddingH + (count * slotWidth) + (Mathf.Max(0, count - 1) * spacing);
            float targetWidth = Mathf.Clamp(calculatedWidth, 0f, _maxItemScrollViewWidth);

            if (!_itemsScrollRect.TryGetComponent<LayoutElement>(out var layoutElement))
            {
                layoutElement = _itemsScrollRect.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.preferredWidth = targetWidth;

            var scrollRect = _itemsScrollRect.GetComponent<RectTransform>();
            if (scrollRect != null)
            {
                layoutElement.preferredHeight = scrollRect.sizeDelta.y > 0 ? scrollRect.sizeDelta.y : 90f;
                scrollRect.sizeDelta = new Vector2(targetWidth, layoutElement.preferredHeight);
            }

            if (parentSection is RectTransform parentRect)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            }

            _itemsScrollRect.horizontalNormalizedPosition = 0f;
        }

        private GameObject CreateItemEntry(RunItemRecord record, bool isExtracted)
        {
            if (_itemEntryPrefab != null)
            {
                GameObject obj = Instantiate(_itemEntryPrefab, _itemsContainer);
                BindItemEntry(obj, record, isExtracted);
                return obj;
            }

            // 프리팹이 지정되지 않은 경우를 위한 폴백 슬롯 UI 생성 (컴팩트 정사각 슬롯: 배경 + 아이콘 + 우측 하단 수량)
            GameObject fallbackObj = new GameObject(record.Item.ItemName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fallbackObj.transform.SetParent(_itemsContainer, false);

            var rootRect = fallbackObj.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(80f, 80f);

            var bgImg = fallbackObj.GetComponent<Image>();
            bgImg.color = isExtracted ? new Color(0.15f, 0.15f, 0.18f, 0.9f) : new Color(0.2f, 0.1f, 0.1f, 0.7f);

            // 중앙 아이콘
            if (record.Item.Icon != null)
            {
                GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconObj.transform.SetParent(fallbackObj.transform, false);

                var iconRect = iconObj.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = Vector2.zero;
                iconRect.sizeDelta = new Vector2(60f, 60f);

                var img = iconObj.GetComponent<Image>();
                img.sprite = record.Item.Icon;
                img.preserveAspect = true;
                img.color = isExtracted ? Color.white : new Color(0.7f, 0.7f, 0.7f, 0.5f);
            }

            // 우측 하단 수량 텍스트
            GameObject textObj = new GameObject("AmountText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(fallbackObj.transform, false);

            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(4f, 4f);
            textRect.offsetMax = new Vector2(-6f, -4f);

            var text = textObj.GetComponent<TextMeshProUGUI>();
            text.fontSize = 16f;
            text.alignment = TextAlignmentOptions.BottomRight;
            text.text = $"x{record.Amount}";
            text.color = isExtracted ? Color.white : new Color(0.85f, 0.5f, 0.5f, 0.9f);

            return fallbackObj;
        }

        private void BindItemEntry(GameObject entryObj, RunItemRecord record, bool isExtracted)
        {
            // 1. 전용 ResultItemSlotUI 컴포넌트가 부착되어 있는 경우 우선 사용
            if (entryObj.TryGetComponent<ResultItemSlotUI>(out var slotUI))
            {
                slotUI.SetItem(record.Item, record.Amount, isExtracted);
                return;
            }

            // 2. 컴포넌트가 없는 경우 자식 오브젝트를 탐색하여 안전하게 바인딩
            // 배경 이미지가 루트에 있을 때 덮어쓰지 않도록 "Icon" 자식을 우선 탐색
            Transform iconTransform = entryObj.transform.Find("Icon");
            Image icon = iconTransform != null
                ? iconTransform.GetComponent<Image>()
                : entryObj.GetComponentInChildren<Image>();

            if (icon != null && record.Item.Icon != null)
            {
                icon.sprite = record.Item.Icon;
                icon.preserveAspect = true;
                icon.color = isExtracted ? Color.white : new Color(0.7f, 0.7f, 0.7f, 0.5f);
            }

            Transform amountTransform = entryObj.transform.Find("AmountText")
                ?? entryObj.transform.Find("AmountCount");
            TextMeshProUGUI label = amountTransform != null
                ? amountTransform.GetComponent<TextMeshProUGUI>()
                : entryObj.GetComponentInChildren<TextMeshProUGUI>();

            if (label != null)
            {
                label.text = $"x{record.Amount}";
                label.color = isExtracted ? Color.white : new Color(0.85f, 0.5f, 0.5f, 0.9f);
            }
        }

        private void OnReturnToShelterClicked()
        {
            if (_expProgressBar != null)
            {
                _expProgressBar.SkipAnimation();
            }

            if (_pauseTimeScaleOnOpen)
            {
                Time.timeScale = 1f;
            }

            SetPlayerActionsEnabled(true);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.GoToShelter();
            }
        }

        private void SetPlayerActionsEnabled(bool enabled)
        {
            ShotgunAndRun.Player.Player.PlayerInstance
                ?.GetComponent<ShotgunAndRun.Player.PlayerInputReader>()
                ?.SetActionsEnabled(enabled);
        }
    }
}
