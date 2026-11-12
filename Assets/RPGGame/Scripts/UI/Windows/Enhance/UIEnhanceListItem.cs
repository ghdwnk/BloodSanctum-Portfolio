using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// 강화창 왼쪽 목록의 한 줄. 강화할 장비를 고르는 용도라 상점 줄과 달리 선택 상태를 갖는다
    /// </summary>
    public class UIEnhanceListItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private Button selectButton;

        private static readonly Color NormalColor = new Color(0.13f, 0.13f, 0.15f, 0.85f);
        private static readonly Color SelectedColor = new Color(0.33f, 0.12f, 0.14f, 0.95f);

        private EquipmentInstance currentItem;

        public void Set(EquipmentInstance item, int level, bool isEquipped, bool isSelected, UnityAction onClick)
        {
            currentItem = item;

            if (iconImage != null)
            {
                iconImage.sprite = item != null ? item.sprite : null;
                iconImage.enabled = item != null && item.sprite != null;
            }

            if (nameText != null)
            {
                // 장착 중인 장비를 먼저 강화하게 되므로 한눈에 보이도록 표시를 붙인다
                nameText.text = item == null ? "-"
                    : isEquipped ? $"{item.item.ColoredName} <color=#8FD46A>[착용]</color>" : item.item.ColoredName;
            }

            if (levelText != null)
            {
                levelText.text = level > 0 ? $"+{level}" : string.Empty;
            }

            if (background != null)
            {
                background.color = isSelected ? SelectedColor : NormalColor;
            }

            if (selectButton != null)
            {
                // 줄을 재사용하므로 이전 선택 콜백이 남지 않게 매번 비운다
                selectButton.onClick.RemoveAllListeners();

                if (onClick != null)
                {
                    selectButton.onClick.AddListener(onClick);
                }
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (currentItem != null)
            {
                UIItemTooltip.Show(currentItem, "클릭해서 강화할 장비 선택");
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            UIItemTooltip.Hide();
        }

        private void OnDisable()
        {
            UIItemTooltip.Hide();
        }
    }
}
