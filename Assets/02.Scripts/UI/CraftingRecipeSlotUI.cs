using System;
using Consumable;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShotgunAndRun.UI
{
    /// <summary>
    /// 제작창 좌측/목록에 표시되는 개별 소모품 레시피 슬롯 UI 컴포넌트입니다.
    /// 소모품 아이콘, 이름, 선택 강조 하이라이트 및 클릭 이벤트를 독립적으로 관리합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CraftingRecipeSlotUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Image _iconImage;

        [SerializeField] private TextMeshProUGUI _nameText;

        [SerializeField] private GameObject _selectedHighlight;

        [SerializeField] private GameObject _craftableBadge;

        [SerializeField] private Button _clickButton;

        public ConsumableData Data
        {
            get;
            private set;
        }
        public bool IsSelected
        {
            get;
            private set;
        }

        private Action<ConsumableData> _onClickCallback;

        private void Awake()
        {
            if (_clickButton != null)
            {
                _clickButton.onClick.AddListener(HandleClicked);
            }
        }

        private void OnDestroy()
        {
            if (_clickButton != null)
            {
                _clickButton.onClick.RemoveListener(HandleClicked);
            }
        }

        /// <summary>
        /// 슬롯에 소모품 데이터를 바인딩하고 표시를 갱신합니다.
        /// </summary>
        public void Bind(ConsumableData data, bool isSelected, bool canCraft, Action<ConsumableData> onClickCallback)
        {
            Data = data;
            IsSelected = isSelected;
            _onClickCallback = onClickCallback;

            if (_iconImage != null)
            {
                if (data != null && data.Icon != null)
                {
                    _iconImage.sprite = data.Icon;
                    _iconImage.enabled = true;
                }
                else
                {
                    _iconImage.enabled = false;
                }
            }

            if (_nameText != null)
            {
                _nameText.text = data != null ? data.ConsumableName : string.Empty;
            }

            SetSelected(isSelected);
            SetCraftable(canCraft);
        }

        public void SetSelected(bool isSelected)
        {
            IsSelected = isSelected;
            if (_selectedHighlight != null)
            {
                _selectedHighlight.SetActive(isSelected);
            }
        }

        public void SetCraftable(bool canCraft)
        {
            if (_craftableBadge != null)
            {
                _craftableBadge.SetActive(canCraft);
            }
        }

        private void HandleClicked()
        {
            if (Data != null)
            {
                _onClickCallback?.Invoke(Data);
            }
        }
    }
}
