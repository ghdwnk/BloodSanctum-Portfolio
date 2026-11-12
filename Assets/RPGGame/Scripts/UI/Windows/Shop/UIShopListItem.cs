using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// 상점 창의 목록 한 줄. 구매 목록과 판매 목록이 같은 모양이라 한 스크립트로 겸한다
    /// </summary>
    public class UIShopListItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI infoText;
        [SerializeField] private Button actionButton;
        [SerializeField] private TextMeshProUGUI actionLabel;

        // 구매 조건을 못 채운 줄은 배경을 붉게 해서 한눈에 구분한다
        private static readonly Color NormalColor = new Color(0.13f, 0.13f, 0.15f, 0.85f);
        private static readonly Color BlockedColor = new Color(0.35f, 0.10f, 0.12f, 0.85f);

        // 툴팁에 넘길 정보 (줄이 재사용되므로 표시할 때마다 갱신한다)
        private Item currentItem;
        // 판매 목록의 장비 줄이면 그 개체 (스탯/강화 단계가 반영된 툴팁과 이름을 보여준다)
        private EquipmentInstance currentInstance;
        private string tooltipHint = string.Empty;

        // 구매 목록용 표시
        // blockedReason: 구매할 수 없을 때 버튼을 눌러 보면 안내로 띄울 이유
        public void SetBuyEntry(ShopData.Entry entry, bool canBuy, string blockedReason, UnityAction onClick)
        {
            tooltipHint = $"구매 가격 {entry.price:N0} G";
            currentInstance = null;
            Apply(entry.item, $"Lv.{entry.requiredLevel}   {entry.price:N0} G", "구매", canBuy, blockedReason, onClick);
        }

        // 판매 목록용 표시. 보유 개수와 개당 판매가를 함께 보여준다
        public void SetSellEntry(Item item, int ownedCount, int unitPrice, bool canSell, UnityAction onClick)
        {
            string info = unitPrice > 0
                ? $"보유 {ownedCount}   {unitPrice:N0} G"
                : $"보유 {ownedCount}   판매 불가";

            tooltipHint = unitPrice > 0 ? $"판매 가격 {unitPrice:N0} G" : "판매할 수 없는 물건";
            currentInstance = null;
            Apply(item, info, "판매", canSell, "판매할 수 없는 물건입니다.", onClick);
        }

        // 판매 목록의 장비 한 줄. 장비는 개체마다 따로 파므로 보유 개수 대신 개당 판매가만 보여준다
        public void SetSellEquipmentEntry(EquipmentInstance instance, int unitPrice, bool canSell, UnityAction onClick)
        {
            string info = unitPrice > 0 ? $"{unitPrice:N0} G" : "판매 불가";

            tooltipHint = unitPrice > 0 ? $"판매 가격 {unitPrice:N0} G" : "판매할 수 없는 물건";
            currentInstance = instance;
            Apply(instance.item, info, "판매", canSell, "판매할 수 없는 물건입니다.", onClick);

            if (nameText != null)
            {
                nameText.text = EnhancementManager.GetDisplayName(instance, true);
            }
        }

        private void Apply(Item item, string info, string buttonLabel, bool interactable, string blockedReason, UnityAction onClick)
        {
            currentItem = item;

            if (iconImage != null)
            {
                iconImage.sprite = item != null ? item.sprite : null;
                iconImage.enabled = item != null && item.sprite != null;
            }

            if (nameText != null)
            {
                // 등급 색이 들어간 이름을 그대로 쓴다
                nameText.text = item != null ? item.ColoredName : "-";
            }

            if (infoText != null)
            {
                infoText.text = info;
            }

            if (background != null)
            {
                background.color = interactable ? NormalColor : BlockedColor;
            }

            if (actionLabel != null)
            {
                actionLabel.text = buttonLabel;
            }

            if (actionButton != null)
            {
                actionButton.interactable = interactable;
                // 줄이 재사용될 때 이전 콜백이 남지 않도록 매번 비운다
                actionButton.onClick.RemoveAllListeners();

                if (onClick != null)
                {
                    actionButton.onClick.AddListener(onClick);
                }

                // 비활성 버튼을 눌렀을 때 이유를 작은 안내로 띄운다
                UIBlockedButtonNotice.Apply(actionButton, interactable, blockedReason);
            }
        }

        // 인벤토리와 같은 툴팁을 그대로 쓴다. 하단 안내만 상점 문구로 바꿔 넘긴다
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (currentInstance != null)
            {
                UIItemTooltip.Show(currentInstance, tooltipHint);
            }
            else if (currentItem != null)
            {
                UIItemTooltip.Show(currentItem, tooltipHint);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            UIItemTooltip.Hide();
        }

        private void OnDisable()
        {
            // 줄이 꺼질 때(목록 축소) 툴팁이 남아 있지 않도록 정리한다
            UIItemTooltip.Hide();
        }
    }
}
