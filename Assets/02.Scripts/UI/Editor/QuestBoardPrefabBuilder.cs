using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShotgunAndRun.UI.Editor
{
    /// <summary>
    /// 퀘스트 게시판 프리팹(QuestBoard, QuestCard)을 스프라이트 시트에서 만듭니다. 다시 실행하면 같은 경로에 덮어씁니다.
    /// 레이아웃 값은 모두 원본 픽셀이며, 캔버스 단위로 옮길 때 3배(Unit)로 키웁니다.
    /// 배치 모드: -executeMethod ShotgunAndRun.UI.Editor.QuestBoardPrefabBuilder.Build
    /// </summary>
    public static class QuestBoardPrefabBuilder
    {
        private const string SheetPath = "Assets/04.Sprites/UI/QuestBoard/quest-board.png";
        private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/Mulmaru SDF.asset";
        private const string BoardPrefabPath = "Assets/03.Prefabs/UI/QuestBoard.prefab";
        private const string CardPrefabPath = "Assets/03.Prefabs/UI/QuestCard.prefab";

        private const float Unit = 3f;
        private const float ReferencePixelsPerUnit = 100f;
        private const float SpritePixelsPerUnit = 16f;
        private const int UiLayer = 5;

        private const float BoardWidth = 540f;
        private const float BoardHeight = 250f;
        private const float CardWidth = 96f;
        private const float CardHeight = 176f;

        private static TMP_FontAsset _font;
        private static Sprite[] _sprites;

        private static readonly Color Gold = Hex("d8c9a0");
        private static readonly Color Ink = Hex("3a2a22");
        private static readonly Color InkSoft = Hex("5c4a3a");
        private static readonly Color Gray = Hex("857c6c");

        [MenuItem("ShotgunAndRun/UI/Build Quest Board Prefabs")]
        public static void Build()
        {
            _sprites = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().ToArray();
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (_sprites.Length == 0 || _font == null)
            {
                Debug.LogError($"[QuestBoardPrefabBuilder] 스프라이트 시트({SheetPath}) 또는 글꼴({FontPath})을 찾을 수 없습니다.");
                return;
            }

            GameObject cardRoot = BuildCard();
            PrefabUtility.SaveAsPrefabAsset(cardRoot, CardPrefabPath);
            Object.DestroyImmediate(cardRoot);

            QuestCardUI cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath).GetComponent<QuestCardUI>();
            GameObject boardRoot = BuildBoard(cardPrefab);
            PrefabUtility.SaveAsPrefabAsset(boardRoot, BoardPrefabPath);
            Object.DestroyImmediate(boardRoot);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[QuestBoardPrefabBuilder] 생성 완료: {BoardPrefabPath}, {CardPrefabPath}");
        }

        // ---------------------------------------------------------------- 카드

        private static GameObject BuildCard()
        {
            RectTransform root = NewRect("QuestCard", null);
            root.sizeDelta = new Vector2(CardWidth * Unit, CardHeight * Unit);
            LayoutElement layout = root.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = layout.preferredWidth = CardWidth * Unit;
            layout.minHeight = layout.preferredHeight = CardHeight * Unit;
            QuestCardUI card = root.gameObject.AddComponent<QuestCardUI>();

            Image glow = AddImage(root, "Glow", "qb_card_glow", -3, -3, 105, 185);
            glow.gameObject.SetActive(false);
            Image cardImage = AddImage(root, "Card", "qb_card_gold", 0, 0, 99, 179);

            // 리본 (스토리 전용)
            RectTransform ribbon = NewRect("Ribbon", root);
            Place(ribbon, 0, 0, CardWidth, CardHeight);
            AddImage(ribbon, "RibbonImage", "qb_ribbon", 2, 9, 30, 12);
            AddText(ribbon, "RibbonText", "필수", 18, Gold, 15, 15, 30, 12, TextAlignmentOptions.Center);

            TextMeshProUGUI title = AddText(root, "Title", "제목", 30, Ink, 48, 29, 84, 14, TextAlignmentOptions.Center);
            title.enableAutoSizing = true;
            title.fontSizeMax = 30;
            title.fontSizeMin = 18;
            AddText(root, "Giver", "의뢰인", 18, InkSoft, 48, 39.5f, 84, 10, TextAlignmentOptions.Center);

            TextMeshProUGUI description = AddText(root, "Description", "설명", 22, Ink, 8, 54, 80, 47,
                TextAlignmentOptions.TopLeft, topLeft: true);
            description.textWrappingMode = TextWrappingModes.Normal;
            description.overflowMode = TextOverflowModes.Truncate;
            description.enableAutoSizing = true;
            description.fontSizeMax = 22;
            description.fontSizeMin = 14;

            AddLeftText(root, "RewardLabel", "보상", 18, InkSoft, 8, 116, 20, 10);
            TextMeshProUGUI xp = AddLeftText(root, "RewardXp", "0 XP", 22, Ink, 27, 115, 50, 10);

            RectTransform rewardRoot = NewRect("RewardIcons", root);
            Place(rewardRoot, 7, 125, 0, 0);
            Image iconTemplate = AddImage(rewardRoot, "IconTemplate", null, 0, 0, 16, 16);
            iconTemplate.gameObject.SetActive(false);
            TextMeshProUGUI amountTemplate = AddLeftText(rewardRoot, "AmountTemplate", "x2", 18, InkSoft, 0, 0, 15, 10);
            amountTemplate.gameObject.SetActive(false);

            // 아래 영역
            Button acceptButton = AddButton(root, "AcceptButton", 12, 150, "qb_btn_normal", "qb_btn_hover",
                "qb_btn_pressed", "qb_btn_disabled", "수락", Gold, out TextMeshProUGUI acceptLabel);
            Button turnInButton = AddButton(root, "TurnInButton", 12, 150, "qb_btn_turnin", "qb_btn_turnin",
                "qb_btn_turnin", "qb_btn_turnin", "퀘스트 완료", Hex("2a2010"), out _);

            TextMeshProUGUI cancelText = AddText(root, "CancelText", "퀘스트 취소", 18, Gray, 48, 159, 60, 10,
                TextAlignmentOptions.Center);
            cancelText.raycastTarget = true;
            Button cancelButton = cancelText.gameObject.AddComponent<Button>();
            cancelButton.transition = Selectable.Transition.None;
            cancelButton.targetGraphic = cancelText;
            QuestButtonFeedback cancelFeedback = cancelText.gameObject.AddComponent<QuestButtonFeedback>();
            SetFeedback(cancelFeedback, cancelText, 0f, true);

            TextMeshProUGUI storyActive = AddText(root, "StoryActiveText", "진행 중", 18, Gray, 48, 159, 60, 10,
                TextAlignmentOptions.Center);

            RectTransform report = NewRect("Report", root);
            Place(report, 0, 0, CardWidth, CardHeight);
            TextMeshProUGUI reportText = AddText(report, "ReportText", "보고", 22, Hex("7a4a1c"), 48, 155, 90, 10,
                TextAlignmentOptions.Center);
            AddText(report, "ReportHint", "쉘터에서 말을 걸어 주세요", 16, Gray, 48, 163, 90, 8, TextAlignmentOptions.Center);

            Image stamp = AddImage(root, "Stamp", "qb_stamp", 24, 124, 48, 48);
            stamp.gameObject.SetActive(false);

            SetRefs(card,
                ("_cardImage", cardImage),
                ("_redCard", Find("qb_card_red")),
                ("_goldCard", Find("qb_card_gold")),
                ("_blueCard", Find("qb_card_blue")),
                ("_glow", glow.gameObject),
                ("_ribbon", ribbon.gameObject),
                ("_titleText", title),
                ("_giverText", root.Find("Giver").GetComponent<TextMeshProUGUI>()),
                ("_descriptionText", description),
                ("_xpText", xp),
                ("_rewardRoot", rewardRoot),
                ("_rewardIconTemplate", iconTemplate),
                ("_rewardAmountTemplate", amountTemplate),
                ("_acceptButton", acceptButton),
                ("_acceptLabel", acceptLabel),
                ("_turnInButton", turnInButton),
                ("_cancelButton", cancelButton),
                ("_storyActiveText", storyActive.gameObject),
                ("_reportRoot", report.gameObject),
                ("_reportText", reportText),
                ("_stamp", stamp.gameObject));

            return root.gameObject;
        }

        // ---------------------------------------------------------------- 게시판

        private static GameObject BuildBoard(QuestCardUI cardPrefab)
        {
            RectTransform root = NewRect("QuestBoard", null);
            Canvas canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            CanvasScaler scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
            scaler.referencePixelsPerUnit = ReferencePixelsPerUnit;
            root.gameObject.AddComponent<GraphicRaycaster>();
            QuestBoardUI board = root.gameObject.AddComponent<QuestBoardUI>();

            // 패널 (화면 가운데, 처음엔 비활성)
            RectTransform panel = NewRect("Panel", root);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(BoardWidth * Unit, BoardHeight * Unit);

            // 투명 판: 틀 · 팻말 위 클릭이 뒤로 새어 사격 입력이 되지 않게 레이캐스트를 받는다
            Image clickBlocker = panel.gameObject.AddComponent<Image>();
            clickBlocker.color = new Color(0f, 0f, 0f, 0f);
            clickBlocker.raycastTarget = true;

            Image frame = AddImage(panel, "Frame", "qb_frame", 0, 0, BoardWidth, BoardHeight);
            frame.type = Image.Type.Tiled;
            frame.fillCenter = false;
            frame.pixelsPerUnitMultiplier = BorderMultiplier();
            frame.raycastTarget = false;

            // 코르크 + 스크롤 영역
            RectTransform cork = NewRect("Cork", panel);
            Place(cork, 13, 25, BoardWidth - 26, BoardHeight - 38);
            Image corkImage = cork.gameObject.AddComponent<Image>();
            corkImage.sprite = Find("qb_cork");
            corkImage.type = Image.Type.Tiled;
            corkImage.pixelsPerUnitMultiplier = BorderMultiplier();

            RectTransform viewport = NewRect("Viewport", cork);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            HorizontalLayoutGroup group = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.padding = new RectOffset((int)(9 * Unit), (int)(9 * Unit), (int)(13 * Unit), 0);
            group.spacing = 12 * Unit;
            group.childAlignment = TextAnchor.UpperLeft;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            // 구분선 견본 (레이아웃 폭 10, 높이는 카드와 같게. 이미지는 위로 11 올려 코르크 위 2부터 204)
            RectTransform divider = NewRect("DividerTemplate", content);
            LayoutElement dividerLayout = divider.gameObject.AddComponent<LayoutElement>();
            dividerLayout.minWidth = dividerLayout.preferredWidth = 10 * Unit;
            dividerLayout.minHeight = dividerLayout.preferredHeight = CardHeight * Unit;
            Image dividerImage = AddImage(divider, "Image", "qb_divider", 0, -11, 10, 204);
            dividerImage.raycastTarget = false;
            divider.gameObject.SetActive(false);

            // 가로 스크롤바
            RectTransform track = NewRect("Scrollbar", panel);
            Place(track, 30, 228, 480, 6);
            Image trackImage = track.gameObject.AddComponent<Image>();
            trackImage.sprite = Find("qb_scroll_track");
            trackImage.type = Image.Type.Sliced;
            trackImage.pixelsPerUnitMultiplier = BorderMultiplier();
            RectTransform slidingArea = NewRect("SlidingArea", track);
            Stretch(slidingArea);
            slidingArea.offsetMin = new Vector2(0f, -1f * Unit);
            slidingArea.offsetMax = new Vector2(0f, 1f * Unit);
            RectTransform handle = NewRect("Handle", slidingArea);
            Stretch(handle);
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.sprite = Find("qb_scroll_thumb");
            handleImage.type = Image.Type.Sliced;
            handleImage.pixelsPerUnitMultiplier = BorderMultiplier();
            Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.LeftToRight;
            scrollbar.transition = Selectable.Transition.None;
            scrollbar.targetGraphic = handleImage;
            scrollbar.handleRect = handle;

            ScrollRect scrollRect = cork.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = true;
            scrollRect.vertical = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f;
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontalScrollbar = scrollbar;
            scrollRect.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            // 머리 팻말
            RectTransform header = AddImage(panel, "Header", "qb_header", (BoardWidth - 150) / 2f, -4, 150, 26).rectTransform;
            header.GetComponent<Image>().raycastTarget = false;
            AddText(panel, "HeaderText", "의뢰 게시판", 30, Gold, BoardWidth / 2f, 9, 150, 18, TextAlignmentOptions.Center);

            // 수주 팻말
            AddImage(panel, "Counter", "qb_counter", BoardWidth - 120, 3, 70, 18).raycastTarget = false;
            AddText(panel, "CounterLabel", "수주", 20, Hex("b8a880"), BoardWidth - 85, 12, 24, 14, TextAlignmentOptions.Center);
            TextMeshProUGUI count = AddText(panel, "CounterValue", "0/3", 22, Gold, BoardWidth - 66, 12, 30, 14,
                TextAlignmentOptions.Center);

            // 닫기
            Button close = AddSimpleSwapButton(panel, "CloseButton", BoardWidth - 26, 4, 16, 16, "qb_close",
                "qb_close_pressed");

            // 취소 확인 팝업 (전체 화면 어둡게 + 팝업)
            RectTransform popupHost = NewRect("CancelPopup", root);
            Stretch(popupHost);
            QuestCancelPopupUI popup = popupHost.gameObject.AddComponent<QuestCancelPopupUI>();

            RectTransform dim = NewRect("Dim", popupHost);
            Stretch(dim);
            Image dimImage = dim.gameObject.AddComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, 150f / 255f);

            RectTransform popupBody = NewRect("Popup", dim);
            popupBody.anchorMin = popupBody.anchorMax = popupBody.pivot = new Vector2(0.5f, 0.5f);
            popupBody.anchoredPosition = Vector2.zero;
            popupBody.sizeDelta = new Vector2(170 * Unit, 84 * Unit);
            Image popupImage = popupBody.gameObject.AddComponent<Image>();
            popupImage.sprite = Find("qb_popup");
            AddText(popupBody, "Title", "의뢰를 취소할까요?", 30, Ink, 85, 20, 160, 14, TextAlignmentOptions.Center);
            TextMeshProUGUI message = AddText(popupBody, "Message", "[제목]은 게시판으로 돌아가고,", 20, InkSoft, 85, 38, 160, 9,
                TextAlignmentOptions.Center);
            AddText(popupBody, "Message2", "언제든 다시 받을 수 있어요.", 20, InkSoft, 85, 47, 160, 9, TextAlignmentOptions.Center);
            Button confirm = AddButton(popupBody, "ConfirmButton", 14, 60, "qb_btn_normal", "qb_btn_hover",
                "qb_btn_pressed", "qb_btn_disabled", "취소하기", Gold, out _);
            Button back = AddButton(popupBody, "BackButton", 90, 60, "qb_btn_normal", "qb_btn_hover",
                "qb_btn_pressed", "qb_btn_disabled", "돌아가기", Gold, out _);
            dim.gameObject.SetActive(false);

            SetRefs(popup,
                ("_root", dim.gameObject),
                ("_messageText", message),
                ("_confirmButton", confirm),
                ("_backButton", back));

            SetRefs(board,
                ("_boardPanel", panel.gameObject),
                ("_content", content),
                ("_scrollRect", scrollRect),
                ("_cardPrefab", cardPrefab),
                ("_dividerTemplate", divider.gameObject),
                ("_countText", count),
                ("_cancelPopup", popup),
                ("_closeButton", close));

            panel.gameObject.SetActive(false);
            return root.gameObject;
        }

        // ---------------------------------------------------------------- 도우미

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color color);
            return color;
        }

        // Sliced/Tiled 이미지의 테두리 · 타일이 원본 × 3이 되게 하는 배율
        private static float BorderMultiplier()
        {
            return ReferencePixelsPerUnit / (SpritePixelsPerUnit * Unit);
        }

        private static Sprite Find(string spriteName)
        {
            Sprite sprite = _sprites.FirstOrDefault(s => s.name == spriteName);
            if (sprite == null)
            {
                Debug.LogError($"[QuestBoardPrefabBuilder] 스프라이트 '{spriteName}'을 시트에서 찾을 수 없습니다.");
            }

            return sprite;
        }

        private static RectTransform NewRect(string objectName, Transform parent)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform));
            go.layer = UiLayer;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // 부모 왼쪽 위 기준 (x, y) 원본 픽셀에 왼쪽 위 모서리를 맞춥니다.
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x * Unit, -y * Unit);
            rect.sizeDelta = new Vector2(width * Unit, height * Unit);
        }

        // 부모 왼쪽 위 기준 (cx, cy) 원본 픽셀에 중심을 맞춥니다.
        private static void PlaceCenter(RectTransform rect, float cx, float cy, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(cx * Unit, -cy * Unit);
            rect.sizeDelta = new Vector2(width * Unit, height * Unit);
        }

        // 왼쪽 가운데 기준 (x, cy)
        private static void PlaceLeftMiddle(RectTransform rect, float x, float cy, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(x * Unit, -cy * Unit);
            rect.sizeDelta = new Vector2(width * Unit, height * Unit);
        }

        private static Image AddImage(RectTransform parent, string objectName, string spriteName, float x, float y,
            float width, float height)
        {
            RectTransform rect = NewRect(objectName, parent);
            Place(rect, x, y, width, height);
            Image image = rect.gameObject.AddComponent<Image>();
            if (spriteName != null)
            {
                image.sprite = Find(spriteName);
            }

            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI CreateText(RectTransform parent, string objectName, string text, float size,
            Color color, TextAlignmentOptions alignment)
        {
            RectTransform rect = NewRect(objectName, parent);
            TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = _font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static TextMeshProUGUI AddText(RectTransform parent, string objectName, string text, float size,
            Color color, float cx, float cy, float width, float height, TextAlignmentOptions alignment,
            bool topLeft = false)
        {
            TextMeshProUGUI tmp = CreateText(parent, objectName, text, size, color, alignment);
            if (topLeft)
            {
                Place(tmp.rectTransform, cx, cy, width, height);
            }
            else
            {
                PlaceCenter(tmp.rectTransform, cx, cy, width, height);
            }

            return tmp;
        }

        private static TextMeshProUGUI AddLeftText(RectTransform parent, string objectName, string text, float size,
            Color color, float x, float cy, float width, float height)
        {
            TextMeshProUGUI tmp = CreateText(parent, objectName, text, size, color, TextAlignmentOptions.MidlineLeft);
            PlaceLeftMiddle(tmp.rectTransform, x, cy, width, height);
            return tmp;
        }

        private static Button AddButton(RectTransform parent, string objectName, float x, float y, string normal,
            string hover, string pressed, string disabled, string label, Color labelColor, out TextMeshProUGUI labelText)
        {
            RectTransform rect = NewRect(objectName, parent);
            Place(rect, x, y, 72, 18);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = Find(normal);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = Find(hover),
                pressedSprite = Find(pressed),
                selectedSprite = Find(normal),
                disabledSprite = Find(disabled)
            };
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            labelText = CreateText(rect, "Label", label, 22, labelColor, TextAlignmentOptions.Center);
            Stretch(labelText.rectTransform);

            QuestButtonFeedback feedback = rect.gameObject.AddComponent<QuestButtonFeedback>();
            SetFeedback(feedback, labelText, 3f, false);
            return button;
        }

        private static Button AddSimpleSwapButton(RectTransform parent, string objectName, float x, float y,
            float width, float height, string normal, string pressed)
        {
            Image image = AddImage(parent, objectName, normal, x, y, width, height);
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = Find(normal),
                pressedSprite = Find(pressed),
                selectedSprite = Find(normal),
                disabledSprite = Find(normal)
            };
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        private static void SetFeedback(QuestButtonFeedback feedback, TextMeshProUGUI label, float pressOffset,
            bool underline)
        {
            SerializedObject so = new SerializedObject(feedback);
            so.FindProperty("_label").objectReferenceValue = label;
            so.FindProperty("_pressOffset").floatValue = pressOffset;
            so.FindProperty("_underlineOnHover").boolValue = underline;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRefs(Object target, params (string field, Object value)[] refs)
        {
            SerializedObject so = new SerializedObject(target);
            foreach ((string field, Object value) in refs)
            {
                SerializedProperty property = so.FindProperty(field);
                if (property == null)
                {
                    Debug.LogError($"[QuestBoardPrefabBuilder] {target.GetType().Name}에 '{field}' 필드가 없습니다.");
                    continue;
                }

                property.objectReferenceValue = value;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
