using InventorySystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShotgunAndRun.UI
{
    /// <summary>
    /// 무기 부품 강화 창 상단에 배치되는 고정 재료 슬롯 UI입니다.
    /// 재료 아이콘과 보유 수량 / 필요 수량을 표시하며,
    /// 재료가 있으면 알파값 1.0, 없으면 알파값을 낮춰(반투명) 미보유 상태임을 직관적으로 보여줍니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class UpgradeMaterialSlotUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Image _iconImage;

        [SerializeField] private TextMeshProUGUI _amountText;

        [SerializeField] private TextMeshProUGUI _nameText;

        [Header("Alpha Settings (미보유/보유 투명도)")]
        [SerializeField, Range(0.05f, 0.5f)] private float _emptyAlpha = 0.25f;

        [SerializeField, Range(0.5f, 1f)] private float _possessedAlpha = 1.0f;

        [Header("Text Colors")]
        [SerializeField] private Color _sufficientColor = new Color(0.35f, 1f, 0.35f, 1f);

        [SerializeField] private Color _insufficientColor = new Color(1f, 0.4f, 0.4f, 1f);

        public ItemData AssignedItem
        {
            get;
            private set;
        }
        public int RequiredAmount
        {
            get;
            private set;
        }
        public int CurrentAmount
        {
            get;
            private set;
        }
        public bool IsSufficient => CurrentAmount >= RequiredAmount;

        /// <summary>
        /// 슬롯에 재료 정보와 필요/보유 수량을 바인딩하고 뷰를 갱신합니다.
        /// </summary>
        public void Bind(ItemData item, int requiredAmount, int currentAmount)
        {
            AssignedItem = item;
            RequiredAmount = Mathf.Max(0, requiredAmount);
            CurrentAmount = Mathf.Max(0, currentAmount);

            gameObject.SetActive(true);

            // 아이콘 갱신
            if (_iconImage != null)
            {
                if (item != null && item.Icon != null)
                {
                    _iconImage.sprite = item.Icon;
                    _iconImage.enabled = true;

                    // 재료 보유 여부에 따른 알파값 설정
                    Color color = Color.white;
                    color.a = CurrentAmount > 0 ? _possessedAlpha : _emptyAlpha;
                    _iconImage.color = color;
                }
                else
                {
                    _iconImage.enabled = false;
                }
            }

            // 수량 텍스트 갱신 (보유량 / 필요량)
            if (_amountText != null)
            {
                if (RequiredAmount > 0)
                {
                    _amountText.gameObject.SetActive(true);
                    _amountText.text = $"{CurrentAmount} / {RequiredAmount}";
                    _amountText.color = IsSufficient ? _sufficientColor : _insufficientColor;
                }
                else
                {
                    // 필요 수량이 0인 경우(해당 강화 단계에서 요구되지 않는 서브 재료 등)
                    _amountText.gameObject.SetActive(false);
                }
            }

            // 이름 텍스트 (옵션)
            if (_nameText != null)
            {
                _nameText.text = item != null ? item.ItemName : string.Empty;
            }
        }

        /// <summary>
        /// 슬롯을 빈 상태(비활성화 또는 비어있는 표시)로 전환합니다.
        /// </summary>
        public void Clear()
        {
            AssignedItem = null;
            RequiredAmount = 0;
            CurrentAmount = 0;

            if (_iconImage != null)
            {
                _iconImage.sprite = null;
                _iconImage.enabled = false;
            }

            if (_amountText != null)
            {
                _amountText.gameObject.SetActive(false);
            }

            if (_nameText != null)
            {
                _nameText.text = string.Empty;
            }

            gameObject.SetActive(false);
        }
    }
}
