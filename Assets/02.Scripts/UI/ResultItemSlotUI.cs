using InventorySystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShotgunAndRun.UI
{
    /// <summary>
    /// 게임 결과창(GameResultUI)의 전리품 정산 목록에 표시되는 단일 아이템 슬롯 UI입니다.
    /// 아이템 아이콘 스프라이트와 우측 하단 획득 수량(개수)을 컴팩트하게 표시합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ResultItemSlotUI : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("아이템 아이콘 스프라이트를 렌더링하는 Image")]
        [SerializeField] private Image _iconImage;

        [Tooltip("우측 하단 획득 수량을 표시하는 TextMeshProUGUI")]
        [SerializeField] private TextMeshProUGUI _amountText;

        [Tooltip("슬롯 배경 프레임 Image (선택 사항)")]
        [SerializeField] private Image _frameImage;

        [Header("Display Format")]
        [Tooltip("수량 앞에 'x' 접두사를 붙일지 여부 (예: 'x3' vs '3')")]
        [SerializeField] private bool _usePrefixX = true;

        [Tooltip("수량이 1개일 때도 수량 텍스트를 표시할지 여부")]
        [SerializeField] private bool _showAmountIfOne = true;

        [Header("Visual Feedback (Extracted vs Lost)")]
        [SerializeField] private Color _extractedIconColor = Color.white;
        [SerializeField] private Color _lostIconColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
        [SerializeField] private Color _extractedTextColor = Color.white;
        [SerializeField] private Color _lostTextColor = new Color(0.85f, 0.45f, 0.45f, 0.8f);

        /// <summary>
        /// 아이템 데이터 및 수량, 탈출 성공 여부를 기반으로 슬롯 뷰를 설정합니다.
        /// </summary>
        public void SetItem(ItemData item, int amount, bool isExtracted)
        {
            if (item == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            // 아이콘 설정
            if (_iconImage != null)
            {
                _iconImage.sprite = item.Icon;
                _iconImage.enabled = item.Icon != null;
                _iconImage.color = isExtracted ? _extractedIconColor : _lostIconColor;
            }

            // 수량 텍스트 설정 (이름은 표시하지 않고 수량만 표기)
            if (_amountText != null)
            {
                if (amount <= 1 && !_showAmountIfOne)
                {
                    _amountText.text = string.Empty;
                }
                else
                {
                    _amountText.text = _usePrefixX ? $"x{amount}" : $"{amount}";
                }

                _amountText.color = isExtracted ? _extractedTextColor : _lostTextColor;
            }

            // 프레임 배경 색상 조절 (있을 경우)
            if (_frameImage != null)
            {
                _frameImage.color = isExtracted
                    ? Color.white
                    : new Color(0.7f, 0.7f, 0.7f, 0.7f);
            }
        }
    }
}
