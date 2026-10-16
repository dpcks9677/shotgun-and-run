using ShotgunAndRun.Core;
using ShotgunAndRun.Quest;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShotgunAndRun.UI
{
    /// <summary>
    /// 진행 중인 의뢰를 취소할지 묻는 확인 팝업입니다. 취소하면 의뢰가 게시판으로 돌아갑니다.
    /// ESC 처리는 QuestBoardUI가 맡습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class QuestCancelPopupUI : MonoBehaviour, IPopupUI
    {
        [Tooltip("어두운 판 + 팝업을 담은 루트 (열고 닫는 대상)")]
        [SerializeField] private GameObject _root;

        [Tooltip("\"[제목]은 게시판으로 돌아가고,\" 줄")]
        [SerializeField] private TextMeshProUGUI _messageText;

        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _backButton;

        private QuestDefinitionSO _quest;

        public bool IsOpen => _root != null && _root.activeSelf;

        private void Awake()
        {
            if (_confirmButton != null)
            {
                _confirmButton.onClick.AddListener(Confirm);
            }

            if (_backButton != null)
            {
                _backButton.onClick.AddListener(Close);
            }
        }

        private void OnDisable()
        {
            PopupManager.Pop(this);
        }

        private void OnDestroy()
        {
            PopupManager.Pop(this);

            if (_confirmButton != null)
            {
                _confirmButton.onClick.RemoveListener(Confirm);
            }

            if (_backButton != null)
            {
                _backButton.onClick.RemoveListener(Close);
            }
        }

        public void Open(QuestDefinitionSO quest)
        {
            if (quest == null)
            {
                return;
            }

            _quest = quest;
            if (_messageText != null)
            {
                _messageText.text = $"[{quest.Title}]은 게시판으로 돌아가고,";
            }

            if (_root != null)
            {
                _root.SetActive(true);
                PopupManager.Push(this);
            }
        }

        public void Close()
        {
            PopupManager.Pop(this);
            _quest = null;
            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        private void Confirm()
        {
            GameManager manager = GameManager.Instance;
            if (_quest != null && manager != null && manager.Quests != null && manager.Quests.TryAbandon(_quest))
            {
                manager.SaveProgress();
            }

            Close();
        }
    }
}
