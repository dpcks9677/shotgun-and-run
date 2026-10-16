using System;
using System.Collections.Generic;
using ShotgunAndRun.Quest;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShotgunAndRun.UI
{
    /// <summary>
    /// 퀘스트 게시판의 카드 한 장. 퀘스트 내용은 Bind로 한 번 채우고, 상태(버튼 · 도장 · 광택)는 SetState로 바꿉니다.
    /// 레이아웃 값은 원본 픽셀 × 3(캔버스 단위)입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class QuestCardUI : MonoBehaviour
    {
        private const float Unit = 3f;

        [Header("Card")]
        [SerializeField] private Image _cardImage;
        [SerializeField] private Sprite _redCard;
        [SerializeField] private Sprite _goldCard;
        [SerializeField] private Sprite _blueCard;
        [SerializeField] private GameObject _glow;
        [SerializeField] private GameObject _ribbon;

        [Header("Content")]
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _giverText;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private TextMeshProUGUI _xpText;

        [Header("Rewards")]
        [SerializeField] private RectTransform _rewardRoot;
        [Tooltip("보상 아이콘 견본 (비활성). 보상 수만큼 복제합니다")]
        [SerializeField] private Image _rewardIconTemplate;
        [Tooltip("보상 수량 견본 (비활성). 수량이 2 이상일 때 복제합니다")]
        [SerializeField] private TextMeshProUGUI _rewardAmountTemplate;

        [Header("Bottom Area")]
        [SerializeField] private Button _acceptButton;
        [SerializeField] private TextMeshProUGUI _acceptLabel;
        [SerializeField] private Button _turnInButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private GameObject _storyActiveText;
        [SerializeField] private GameObject _reportRoot;
        [SerializeField] private TextMeshProUGUI _reportText;
        [SerializeField] private GameObject _stamp;

        [Header("Label Colors")]
        [SerializeField] private Color _acceptColor = new Color32(0xd8, 0xc9, 0xa0, 0xff);
        [SerializeField] private Color _fullColor = new Color32(0x9a, 0x93, 0x84, 0xff);

        private readonly List<GameObject> _rewardObjects = new List<GameObject>();
        private QuestDefinitionSO _quest;

        public QuestDefinitionSO Quest => _quest;

        public event Action<QuestDefinitionSO> AcceptClicked;
        public event Action<QuestDefinitionSO> TurnInClicked;
        public event Action<QuestDefinitionSO> CancelClicked;

        private void Awake()
        {
            if (_acceptButton != null)
            {
                _acceptButton.onClick.AddListener(() => AcceptClicked?.Invoke(_quest));
            }

            if (_turnInButton != null)
            {
                _turnInButton.onClick.AddListener(() => TurnInClicked?.Invoke(_quest));
            }

            if (_cancelButton != null)
            {
                _cancelButton.onClick.AddListener(() => CancelClicked?.Invoke(_quest));
            }
        }

        /// <summary>
        /// 퀘스트 내용을 채웁니다. requestIndex는 게시판 안 의뢰 순번이며 짝수는 금색, 홀수는 파란색 카드입니다.
        /// </summary>
        public void Bind(QuestDefinitionSO quest, int requestIndex)
        {
            _quest = quest;
            bool isStory = quest.Slot == QuestSlot.Story;

            _cardImage.sprite = isStory ? _redCard : requestIndex % 2 == 0 ? _goldCard : _blueCard;
            _ribbon.SetActive(isStory);
            _titleText.text = quest.Title;
            _giverText.text = $"의뢰인 · {quest.Giver}";
            _descriptionText.text = quest.Description;
            _xpText.text = $"{quest.RewardXp} XP";
            _reportText.text = $"{quest.Giver}에게 보고";

            BuildRewardIcons(quest);
        }

        public void SetState(QuestCardState state)
        {
            _glow.SetActive(state == QuestCardState.Ready);
            _acceptButton.gameObject.SetActive(state == QuestCardState.Available || state == QuestCardState.Full);
            _acceptButton.interactable = state == QuestCardState.Available;
            _turnInButton.gameObject.SetActive(state == QuestCardState.Ready);
            _cancelButton.gameObject.SetActive(state == QuestCardState.Active);
            _storyActiveText.SetActive(state == QuestCardState.StoryActive);
            _reportRoot.SetActive(state == QuestCardState.ReportToNpc);
            _stamp.SetActive(state == QuestCardState.Done);

            if (state == QuestCardState.Available || state == QuestCardState.Full)
            {
                bool available = state == QuestCardState.Available;
                _acceptLabel.text = available ? "수락" : "수락 불가";
                _acceptLabel.color = available ? _acceptColor : _fullColor;
            }
        }

        private void BuildRewardIcons(QuestDefinitionSO quest)
        {
            foreach (GameObject spawned in _rewardObjects)
            {
                Destroy(spawned);
            }

            _rewardObjects.Clear();

            float x = 0f;
            foreach (QuestRewardItem reward in quest.RewardItems)
            {
                if (!reward.IsValid)
                {
                    continue;
                }

                Image icon = Instantiate(_rewardIconTemplate, _rewardRoot);
                icon.gameObject.SetActive(true);
                icon.sprite = reward.Item.Icon;
                icon.rectTransform.anchoredPosition = new Vector2(x * Unit, 0f);
                _rewardObjects.Add(icon.gameObject);
                x += 16f + 1f;

                if (reward.Amount > 1)
                {
                    TextMeshProUGUI amount = Instantiate(_rewardAmountTemplate, _rewardRoot);
                    amount.gameObject.SetActive(true);
                    amount.text = $"x{reward.Amount}";
                    amount.rectTransform.anchoredPosition = new Vector2(x * Unit, -8f * Unit);
                    _rewardObjects.Add(amount.gameObject);
                    x += 10f;
                }
            }
        }
    }
}
