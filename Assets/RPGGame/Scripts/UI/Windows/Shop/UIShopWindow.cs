using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 상점 창. 왼쪽에 상점이 파는 물건, 오른쪽에 내 인벤토리를 놓고 사고팔 수 있게 한다
    /// </summary>
    public class UIShopWindow : MonoBehaviour
    {
        private static UIShopWindow instance;

        [SerializeField] private GameObject window;
        [SerializeField] private TextMeshProUGUI titleText;
        [Tooltip("목록 줄을 붙일 부모 (VerticalLayoutGroup)")]
        [SerializeField] private RectTransform buyContent;
        [SerializeField] private RectTransform sellContent;
        [SerializeField] private UIShopListItem listItemPrefab;

        // 매 갱신마다 줄을 새로 만들지 않도록 만들어 둔 줄을 재사용한다
        private readonly List<UIShopListItem> buyRows = new();
        private readonly List<UIShopListItem> sellRows = new();
        private bool isSubscribed;
        // 장착 중인 장비는 판매 목록에서 제외한다 (벗어야 팔 수 있다)
        private PlayerEquipment playerEquipment;

        public static bool IsOn => instance != null && instance.window.activeSelf;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public static void Show()
        {
            if (instance == null)
            {
                return;
            }

            UISound.SetActive(instance.window, true);
            instance.Refresh();
        }

        public static void Close()
        {
            if (instance != null)
            {
                UISound.SetActive(instance.window, false);
                // 상점이 닫히면 열려 있던 수량 창도 함께 닫는다 (남아 있으면 이미 없는 상점의 거래를 확정하게 된다)
                UIQuantityDialog.Close();
            }
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnDisable()
        {
            UIQuantityDialog.Close();
        }

        private void Refresh()
        {
            ShopController shop = ShopController.Instance;

            if (shop == null || shop.Data == null)
            {
                return;
            }

            if (playerEquipment == null)
            {
                playerEquipment = FindAnyObjectByType<PlayerEquipment>();
            }

            if (!isSubscribed)
            {
                // 구매/판매나 아이템 변화가 있을 때 목록을 다시 그린다 (한 번만 구독)
                shop.SubscribeOnShopStateChanged(Refresh);
                InventoryManager.Instance?.SubscribeOnItemListChanged(Refresh);
                // 장착/해제 시에도 판매 목록에서 즉시 빠지거나 다시 나타나야 한다
                playerEquipment?.SubscribeOnEquipmentChanged(Refresh);
                isSubscribed = true;
            }

            if (titleText != null)
            {
                titleText.text = shop.Data.shopName;
            }

            RefreshBuyList(shop);
            RefreshSellList(shop);
        }

        private void RefreshBuyList(ShopController shop)
        {
            List<ShopData.Entry> entries = shop.Data.entries;
            EnsureRowCount(buyRows, buyContent, entries.Count);

            for (int ix = 0; ix < entries.Count; ++ix)
            {
                ShopData.Entry entry = entries[ix];
                bool canBuy = shop.CanBuy(entry, out string blockedReason);
                buyRows[ix].SetBuyEntry(entry, canBuy, blockedReason, () => OnBuyClicked(shop, entry));
            }
        }

        private void RefreshSellList(ShopController shop)
        {
            // 개수로 쌓이는 아이템(소모품/재료)과 장비 개체를 한 목록으로 보여준다.
            // 장착 중인 개체는 벗어야 팔 수 있으므로 제외하고, 같은 종류의 다른 개체는 그대로 판다
            List<ItemSlot> stacks = InventoryManager.Instance != null
                ? InventoryManager.Instance.GetItems()
                : new List<ItemSlot>();

            List<EquipmentInstance> equipments = new List<EquipmentInstance>();
            if (InventoryManager.Instance != null)
            {
                foreach (EquipmentInstance instance in InventoryManager.Instance.GetEquipments())
                {
                    if (playerEquipment != null && playerEquipment.IsEquipped(instance))
                    {
                        continue;
                    }

                    equipments.Add(instance);
                }
            }

            EnsureRowCount(sellRows, sellContent, equipments.Count + stacks.Count);

            for (int ix = 0; ix < equipments.Count; ++ix)
            {
                EquipmentInstance instance = equipments[ix];
                int unitPrice = shop.GetSellPrice(instance.item);
                sellRows[ix].SetSellEquipmentEntry(instance, unitPrice, unitPrice > 0, () => shop.TrySellEquipment(instance));
            }

            for (int ix = 0; ix < stacks.Count; ++ix)
            {
                ItemSlot slot = stacks[ix];
                int unitPrice = shop.GetSellPrice(slot.item);
                sellRows[equipments.Count + ix].SetSellEntry(slot.item, slot.count, unitPrice, unitPrice > 0, () => OnSellClicked(shop, slot.item));
            }
        }

        // 구매 버튼: 두 개 이상 살 수 있으면 수량을 묻는다. 하나밖에 못 사면(골드가 모자라는 경우 포함) 묻지 않고 바로 산다
        private void OnBuyClicked(ShopController shop, ShopData.Entry entry)
        {
            if (!shop.CanBuy(entry, out string reason))
            {
                UISystemMessage.Show(reason);
                return;
            }

            int maxCount = shop.GetMaxBuyCount(entry);

            if (maxCount <= 1)
            {
                shop.TryBuy(entry);
                return;
            }

            UIQuantityDialog.Show("몇 개 구매하시겠습니까?", entry.item.ColoredName, maxCount, entry.price, "합계",
                count => shop.TryBuy(entry, count));
        }

        // 판매 버튼: 두 개 이상 갖고 있으면 수량을 묻는다. 하나뿐이면 바로 판다
        private void OnSellClicked(ShopController shop, Item item)
        {
            int owned = InventoryManager.Instance != null ? InventoryManager.Instance.GetItemCount(item) : 0;

            if (owned <= 1)
            {
                shop.TrySell(item);
                return;
            }

            UIQuantityDialog.Show("몇 개 판매하시겠습니까?", item.ColoredName, Mathf.Min(owned, ShopController.MaxTradeCount),
                shop.GetSellPrice(item), "받을 금액", count => shop.TrySell(item, count));
        }

        // 필요한 줄 수만큼 만들고, 남는 줄은 꺼둔다 (매번 Destroy하면 GC가 계속 발생한다)
        private void EnsureRowCount(List<UIShopListItem> rows, RectTransform content, int count)
        {
            while (rows.Count < count)
            {
                rows.Add(Instantiate(listItemPrefab, content));
            }

            for (int ix = 0; ix < rows.Count; ++ix)
            {
                rows[ix].gameObject.SetActive(ix < count);
            }
        }
    }
}
