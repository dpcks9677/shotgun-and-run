using System.Collections.Generic;
using ShotgunAndRun.Core;
using ShotgunAndRun.NPC;
using ShotgunAndRun.Quest;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShotgunAndRun.UI
{
    /// <summary>
    /// 쉘터 퀘스트 게시판 창. 열 때 게시판 카드(QuestService.GetBoardQuests)를 만들고, 닫을 때 완료 도장을 비웁니다.
    /// 열려 있는 동안 완료 처리한 카드는 도장을 찍은 채 그 자리에 남고, 그로 인해 새로 해금된 퀘스트는 바로 뒤에 카드로 붙습니다.
    /// 로나의 NpcInteractable(대화에서 "퀘스트 보기")이 Open을, 반경 이탈(OnLeaveRange)이 Close를 부릅니다.
    /// 창이 열려 있는 동안 NpcInteractable의 E 상호작용을 막습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class QuestBoardUI : MonoBehaviour, IPopupUI
    {
        private static readonly Color CountNormalColor = new Color32(0xd8, 0xc9, 0xa0, 0xff);
        private static readonly Color CountFullColor = new Color32(0xe0, 0x70, 0x60, 0xff);

        [Header("UI Panels")]
        [Tooltip("게시판 창의 루트 패널 오브젝트")]
        [SerializeField] private GameObject _boardPanel;

        [Tooltip("카드가 놓일 스크롤 콘텐츠 (HorizontalLayoutGroup)")]
        [SerializeField] private RectTransform _content;

        [SerializeField] private ScrollRect _scrollRect;

        [Tooltip("카드 프리팹 (QuestCard)")]
        [SerializeField] private QuestCardUI _cardPrefab;

        [Tooltip("진행 중 묶음과 수락 가능 묶음 사이 구분선 견본 (비활성)")]
        [SerializeField] private GameObject _dividerTemplate;

        [SerializeField] private TextMeshProUGUI _countText;
        [SerializeField] private QuestCancelPopupUI _cancelPopup;

        [Header("Window Controls")]
        [SerializeField] private Button _closeButton;
        [SerializeField] private bool _closeOnEscape = true;

        private readonly List<QuestCardUI> _cards = new List<QuestCardUI>();
        private readonly List<GameObject> _dividers = new List<GameObject>();
        private QuestService _subscribedQuests;
        private int _requestIndex;
        private bool _hasActiveGroup;
        private bool _dividerPlaced;

        public bool IsOpen => _boardPanel != null && _boardPanel.activeSelf;

        private void Awake()
        {
            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(Close);
            }
        }

        private void OnDisable()
        {
            PopupManager.Pop(this);
        }

        private void OnDestroy()
        {
            PopupManager.Pop(this);

            // 열린 채 씬이 바뀌면 닫을 때처럼 완료 도장을 비운다
            if (IsOpen && GameManager.Instance != null && GameManager.Instance.Quests != null)
            {
                GameManager.Instance.Quests.AcknowledgeStamps();
                GameManager.Instance.SaveProgress();
            }

            NpcInteractable.SetBlocked(this, false);
            Unsubscribe();

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(Close);
            }
        }

        public void Open()
        {
            GameManager manager = GameManager.Instance;
            QuestService quests = manager != null ? manager.Quests : null;
            if (quests == null || _boardPanel == null || IsOpen)
            {
                return;
            }

            BuildCards(quests);
            _boardPanel.SetActive(true);
            NpcInteractable.SetBlocked(this, true);
            if (_closeOnEscape)
            {
                PopupManager.Push(this);
            }

            if (_scrollRect != null)
            {
                _scrollRect.horizontalNormalizedPosition = 0f;
            }

            Unsubscribe();
            _subscribedQuests = quests;
            _subscribedQuests.Changed += RefreshCards;
            RefreshCards();
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            PopupManager.Pop(this);

            if (_cancelPopup != null)
            {
                _cancelPopup.Close();
            }

            Unsubscribe();
            _boardPanel.SetActive(false);
            NpcInteractable.SetBlocked(this, false);
            ClearCards();

            GameManager manager = GameManager.Instance;
            if (manager != null && manager.Quests != null)
            {
                manager.Quests.AcknowledgeStamps();
                manager.SaveProgress();
            }
        }

        private void Unsubscribe()
        {
            if (_subscribedQuests != null)
            {
                _subscribedQuests.Changed -= RefreshCards;
                _subscribedQuests = null;
            }
        }

        private void BuildCards(QuestService quests)
        {
            ClearCards();
            _requestIndex = 0;
            _hasActiveGroup = false;
            _dividerPlaced = false;

            foreach (QuestDefinitionSO quest in quests.GetBoardQuests())
            {
                AddCard(quests, quest);
            }
        }

        private void AddCard(QuestService quests, QuestDefinitionSO quest)
        {
            bool isAvailable = quests.GetState(quest) == QuestState.Available;

            // 진행 중 묶음(스토리 · 수락한 의뢰 · 도장 카드) 뒤에 수락 가능 묶음이 시작될 때만 구분선을 둔다
            if (isAvailable && _hasActiveGroup && !_dividerPlaced)
            {
                GameObject divider = Instantiate(_dividerTemplate, _content);
                divider.SetActive(true);
                _dividers.Add(divider);
                _dividerPlaced = true;
            }

            QuestCardUI card = Instantiate(_cardPrefab, _content);
            card.Bind(quest, _requestIndex);
            card.AcceptClicked += HandleAccept;
            card.TurnInClicked += HandleTurnIn;
            card.CancelClicked += HandleCancel;
            _cards.Add(card);

            if (quest.Slot == QuestSlot.Request)
            {
                _requestIndex++;
            }

            if (!isAvailable)
            {
                _hasActiveGroup = true;
            }
        }

        // 보고 등으로 새로 해금된 퀘스트를 열린 게시판 끝에 붙입니다. (이미 있는 카드는 그대로)
        private void AppendNewCards(QuestService quests)
        {
            foreach (QuestDefinitionSO quest in quests.GetBoardQuests())
            {
                if (!_cards.Exists(card => card.Quest == quest))
                {
                    AddCard(quests, quest);
                }
            }
        }

        private void ClearCards()
        {
            foreach (QuestCardUI card in _cards)
            {
                if (card != null)
                {
                    Destroy(card.gameObject);
                }
            }

            foreach (GameObject divider in _dividers)
            {
                if (divider != null)
                {
                    Destroy(divider);
                }
            }

            _cards.Clear();
            _dividers.Clear();
        }

        // 새로 해금된 카드를 붙이고, 카드 상태와 수주 수를 다시 계산합니다. 이미 놓인 카드는 빼거나 옮기지 않습니다.
        private void RefreshCards()
        {
            QuestService quests = GameManager.Instance != null ? GameManager.Instance.Quests : null;
            if (quests == null)
            {
                return;
            }

            AppendNewCards(quests);

            foreach (QuestCardUI card in _cards)
            {
                if (QuestCardStateResolver.TryResolve(card.Quest.Slot, card.Quest.TurnInByNpc,
                        quests.GetState(card.Quest), quests.CanAccept(card.Quest), out QuestCardState state))
                {
                    card.SetState(state);
                }
            }

            if (_countText != null)
            {
                int accepted = quests.AcceptedCount;
                _countText.text = $"{accepted}/{QuestService.MaxAcceptedCount}";
                _countText.color = accepted >= QuestService.MaxAcceptedCount ? CountFullColor : CountNormalColor;
            }
        }

        private void HandleAccept(QuestDefinitionSO quest)
        {
            GameManager manager = GameManager.Instance;
            if (manager != null && manager.Quests != null && manager.Quests.TryAccept(quest))
            {
                manager.SaveProgress();
            }
        }

        private void HandleTurnIn(QuestDefinitionSO quest)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.TryTurnInQuest(quest);
            }
        }

        private void HandleCancel(QuestDefinitionSO quest)
        {
            if (_cancelPopup != null)
            {
                _cancelPopup.Open(quest);
            }
        }
    }
}
