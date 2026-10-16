using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using ShotgunAndRun.Core;
using ShotgunAndRun.Cutscene;

namespace ShotgunAndRun.UI
{
    [DisallowMultipleComponent]
    public sealed class PauseMenuUI : MonoBehaviour
    {
        private const int CanvasSortOrder = 1100;
        private const string AbandonLabel = "현재 게임 포기";
        private const string SettingsLabel = "설정";
        private const string TitleLabel = "타이틀로";
        private const string ConfirmYesLabel = "예";
        private const string ConfirmNoLabel = "아니오";
        private const string ShelterTitleConfirmation = "정말 타이틀 화면으로 이동하시겠습니까?";
        private const string DungeonAbandonConfirmation = "현재까지 진행된 게임 데이터를 포기하고\n쉘터로 이동하시겠습니까?";
        private const string DungeonTitleConfirmation = "현재 진행 내용이 저장되지 않습니다.\n타이틀 화면으로 이동하시겠습니까?";

        private enum PendingAction
        {
            None,
            AbandonCurrentGame,
            GoToTitle
        }

        [SerializeField] private SettingsPopupUI _settingsPopupPrefab;
        [SerializeField] private AudioClip _buttonHoverSfx;
        [SerializeField] private AudioClip _buttonClickSfx;
        [SerializeField, Range(0f, 1f)] private float _buttonHoverVolume = 0.15f;
        [SerializeField, Range(0f, 1f)] private float _buttonClickVolume = 0.25f;

        private GameManager _gameManager;
        private GameObject _menuRoot;
        private GameObject _mainPanel;
        private GameObject _confirmationPanel;
        private Button _abandonButton;
        private Button _settingsButton;
        private Button _titleButton;
        private SettingsPopupUI _settingsPopup;
        private TextMeshProUGUI _confirmationText;
        private Button _confirmationYesButton;
        private Button _confirmationNoButton;
        private PendingAction _pendingAction;
        private bool _isPaused;
        private bool _ownsTimeScalePause;
        private float _timeScaleBeforePause = 1f;

        private void Awake()
        {
            BuildMenu();
        }

        private void Start()
        {
            _gameManager = GameManager.Instance;
            if (_gameManager != null)
            {
                _gameManager.StateChanged += HandleStateChanged;
            }

            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void Update()
        {
            if (_ownsTimeScalePause && !_isPaused)
            {
                RestoreTimeScale();
                return;
            }

            if (_ownsTimeScalePause && Time.timeScale != 0f)
            {
                Time.timeScale = 0f;
            }

            if (!Input.GetKeyDown(KeyCode.Escape))
            {
                return;
            }

            if (_settingsPopup != null && _settingsPopup.IsOpen)
            {
                _settingsPopup.Close();
                return;
            }

            if (_confirmationPanel != null && _confirmationPanel.activeSelf)
            {
                HandleConfirmationNo();
                return;
            }

            if (_isPaused)
            {
                Close();
            }
            else
            {
                // 컷씬 재생 중에는 ESC 키를 눌러도 설정/일시정지 메뉴가 열리지 않도록 차단
                if (CutsceneDirector.IsPlaying)
                {
                    return;
                }

                if (PopupManager.WasAnyPopupClosedThisFrame || PopupManager.TryCloseTopPopup())
                {
                    return;
                }

                Open();
            }
        }

        private void OnDestroy()
        {
            if (_gameManager != null)
            {
                _gameManager.StateChanged -= HandleStateChanged;
            }

            if (_settingsPopup != null)
            {
                _settingsPopup.Closed -= HandleSettingsClosed;
            }

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            RestoreTimeScale();
        }

        private void OnDisable()
        {
            RestoreTimeScale();
        }

        public void Open()
        {
            if (_isPaused || !CanOpen())
            {
                return;
            }

            _timeScaleBeforePause = Time.timeScale;
            _ownsTimeScalePause = true;
            _isPaused = true;
            Time.timeScale = 0f;
            _menuRoot.SetActive(true);
            RefreshButtonVisibility();
            ShowMainPanel();
            SelectInitialButton();
        }

        public void Close()
        {
            RestoreTimeScale();
            _pendingAction = PendingAction.None;
            if (_settingsPopup != null && _settingsPopup.IsOpen)
            {
                _settingsPopup.Close();
            }
            _mainPanel.SetActive(true);
            _confirmationPanel.SetActive(false);
            _menuRoot.SetActive(false);
        }

        private void HandleSettingsClicked()
        {
            if (_settingsPopup == null)
            {
                return;
            }

            _mainPanel.SetActive(false);
            _confirmationPanel.SetActive(false);
            _settingsPopup.Open();
        }

        private void HandleSettingsClosed()
        {
            if (_isPaused)
            {
                ShowMainPanel();
                if (_settingsButton != null && _settingsButton.gameObject.activeInHierarchy)
                {
                    _settingsButton.Select();
                }
            }
        }

        private void HandleAbandonClicked()
        {
            ShowConfirmation(PendingAction.AbandonCurrentGame, DungeonAbandonConfirmation);
        }

        private void HandleTitleClicked()
        {
            string message = IsShelterState()
                ? ShelterTitleConfirmation
                : DungeonTitleConfirmation;
            ShowConfirmation(PendingAction.GoToTitle, message);
        }

        private void HandleConfirmationYes()
        {
            switch (_pendingAction)
            {
                case PendingAction.AbandonCurrentGame:
                    if (_gameManager != null)
                    {
                        Close();
                        Time.timeScale = 1f;
                        _gameManager.AbandonRun();
                    }
                    break;
                case PendingAction.GoToTitle:
                    if (_gameManager != null)
                    {
                        LeaveToScene(_gameManager.GoToTitle);
                    }
                    break;
            }
        }

        private void HandleConfirmationNo()
        {
            ShowMainPanel();
            SelectInitialButton();
        }

        private void ShowConfirmation(PendingAction pendingAction, string message)
        {
            _pendingAction = pendingAction;
            _confirmationText.text = message;
            _mainPanel.SetActive(false);
            _confirmationPanel.SetActive(true);
            _confirmationNoButton.Select();
        }

        private void ShowMainPanel()
        {
            _pendingAction = PendingAction.None;
            if (_settingsPopup != null && _settingsPopup.IsOpen)
            {
                _settingsPopup.Close();
            }
            _mainPanel.SetActive(true);
            _confirmationPanel.SetActive(false);
        }

        private void SelectInitialButton()
        {
            Button firstButton = _abandonButton.gameObject.activeSelf
                ? _abandonButton
                : _settingsButton != null && _settingsButton.gameObject.activeSelf
                    ? _settingsButton
                    : _titleButton;
            firstButton.Select();
        }

        private void LeaveToScene(System.Action loadScene)
        {
            if (loadScene == null)
            {
                return;
            }

            Close();
            Time.timeScale = 1f;
            loadScene.Invoke();
        }

        private bool CanOpen()
        {
            if (CutsceneDirector.IsPlaying)
            {
                return false;
            }

            if (_gameManager == null || _gameManager.IsSceneTransitioning)
            {
                return _gameManager == null;
            }

            return _gameManager.State != GameState.Title &&
                   _gameManager.State != GameState.Result;
        }

        private bool IsShelterState()
        {
            return _gameManager != null && _gameManager.State == GameState.Shelter;
        }

        private void RefreshButtonVisibility()
        {
            _abandonButton.gameObject.SetActive(!IsShelterState());
        }

        private void RestoreTimeScale()
        {
            if (!_ownsTimeScalePause)
            {
                _isPaused = false;
                return;
            }

            Time.timeScale = _timeScaleBeforePause;
            _ownsTimeScalePause = false;
            _isPaused = false;
        }

        private void HandleStateChanged(GameState previous, GameState next)
        {
            if (next == GameState.Title || next == GameState.Result)
            {
                Close();
                return;
            }

            RefreshButtonVisibility();
            if (_isPaused)
            {
                ShowMainPanel();
                SelectInitialButton();
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single)
            {
                Close();
                PopupManager.Clear();
            }
        }

        private void BuildMenu()
        {
            GameObject canvasObject = new GameObject(
                "PauseMenuCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _menuRoot = canvasObject;
            _menuRoot.SetActive(false);

            GameObject overlay = CreateRectObject("PauseMenuOverlay", canvasObject.transform);
            RectTransform overlayRect = overlay.GetComponent<RectTransform>();
            StretchToParent(overlayRect);
            Image overlayImage = overlay.AddComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.45f);

            GameObject panel = CreateRectObject("PauseMenuPanel", canvasObject.transform);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(460f, 380f);
            _mainPanel = panel;

            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.08f, 0.08f, 0.1f, 0.98f);

            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 35, 35);
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            _abandonButton = CreateButton(panel.transform, "AbandonButton", AbandonLabel);
            _settingsButton = CreateButton(panel.transform, "SettingsButton", SettingsLabel);
            _titleButton = CreateButton(panel.transform, "TitleButton", TitleLabel);
            _abandonButton.onClick.AddListener(HandleAbandonClicked);
            _settingsButton.onClick.AddListener(HandleSettingsClicked);
            _titleButton.onClick.AddListener(HandleTitleClicked);

            BuildConfirmationPanel(canvasObject.transform);
            BuildSettingsPopup();
        }

        private void BuildSettingsPopup()
        {
            GameObject canvasObject = new GameObject(
                "SettingsPopupCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortOrder + 10;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(320f, 180f);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 16f;

            SettingsPopupUI prefab = _settingsPopupPrefab;
            if (prefab == null)
            {
                prefab = Resources.Load<SettingsPopupUI>("UI/SettingsPopup");
                if (prefab == null)
                {
                    GameObject resObj = Resources.Load<GameObject>("UI/SettingsPopup");
                    if (resObj != null)
                    {
                        prefab = resObj.GetComponent<SettingsPopupUI>();
                    }
                }
            }

            if (prefab != null)
            {
                _settingsPopup = Instantiate(prefab, canvasObject.transform, false);
                _settingsPopup.SetButtonAudio(
                    _buttonHoverSfx,
                    _buttonClickSfx,
                    _buttonHoverVolume,
                    _buttonClickVolume);
                _settingsPopup.gameObject.SetActive(false);
                _settingsPopup.Closed += HandleSettingsClosed;
            }
        }

        private void BuildConfirmationPanel(Transform canvasTransform)
        {
            _confirmationPanel = CreateRectObject("PauseConfirmationPanel", canvasTransform);
            RectTransform panelRect = _confirmationPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(560f, 280f);

            Image panelImage = _confirmationPanel.AddComponent<Image>();
            panelImage.color = new Color(0.08f, 0.08f, 0.1f, 0.98f);

            VerticalLayoutGroup layout = _confirmationPanel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(35, 35, 35, 35);
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            _confirmationText = CreateText(
                _confirmationPanel.transform,
                "ConfirmationText",
                string.Empty,
                26f,
                TextAlignmentOptions.Center);
            LayoutElement textLayout = _confirmationText.gameObject.AddComponent<LayoutElement>();
            textLayout.minHeight = 90f;
            textLayout.preferredHeight = 90f;

            GameObject buttonRow = CreateRectObject("ConfirmationButtons", _confirmationPanel.transform);
            LayoutElement rowLayout = buttonRow.AddComponent<LayoutElement>();
            rowLayout.minHeight = 64f;
            rowLayout.preferredHeight = 64f;

            HorizontalLayoutGroup rowGroup = buttonRow.AddComponent<HorizontalLayoutGroup>();
            rowGroup.spacing = 18f;
            rowGroup.childAlignment = TextAnchor.MiddleCenter;
            rowGroup.childControlWidth = true;
            rowGroup.childControlHeight = true;
            rowGroup.childForceExpandWidth = true;
            rowGroup.childForceExpandHeight = true;

            _confirmationYesButton = CreateButton(buttonRow.transform, "YesButton", ConfirmYesLabel);
            _confirmationNoButton = CreateButton(buttonRow.transform, "NoButton", ConfirmNoLabel);
            _confirmationYesButton.onClick.AddListener(HandleConfirmationYes);
            _confirmationNoButton.onClick.AddListener(HandleConfirmationNo);
            _confirmationPanel.SetActive(false);
        }

        private static GameObject CreateRectObject(string objectName, Transform parent)
        {
            GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private Button CreateButton(Transform parent, string objectName, string labelText)
        {
            GameObject buttonObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.18f, 0.18f, 0.22f, 1f);

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.colors = new ColorBlock
            {
                normalColor = new Color(0.18f, 0.18f, 0.22f, 1f),
                highlightedColor = new Color(0.3f, 0.3f, 0.36f, 1f),
                pressedColor = new Color(0.12f, 0.12f, 0.15f, 1f),
                selectedColor = new Color(0.3f, 0.3f, 0.36f, 1f),
                disabledColor = new Color(0.18f, 0.18f, 0.22f, 0.5f),
                colorMultiplier = 1f,
                fadeDuration = 0.1f
            };

            LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
            layoutElement.minHeight = 64f;
            layoutElement.preferredHeight = 64f;

            CreateText(buttonObject.transform, "Label", labelText, 28f, TextAlignmentOptions.Center);

            ButtonHoverFeedback hoverFeedback = buttonObject.AddComponent<ButtonHoverFeedback>();
            hoverFeedback.SetAudio(
                _buttonHoverSfx,
                _buttonClickSfx,
                _buttonHoverVolume,
                _buttonClickVolume);

            return button;
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string objectName,
            string text,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            GameObject labelObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(parent, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            StretchToParent(labelRect);

            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Mulmaru SDF") ??
                         Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            label.text = text;
            label.fontSize = fontSize;
            label.color = Color.white;
            label.alignment = alignment;
            label.enableWordWrapping = true;
            label.raycastTarget = false;
            return label;
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
        }
    }
}
