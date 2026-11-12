using UnityEngine;
using UnityEngine.Events;

namespace RPGGame
{
    /// <summary>
    /// 상점의 구매·판매를 처리하는 관리자. 게임 씬의 매니저 오브젝트에 하나만 둔다.
    ///
    /// 상인 NPC(가론)에게 말을 걸면 상점 창(UIShopWindow)이 열리고, 창의 구매/판매 버튼이 여기의 TryBuy·TrySell을 부른다.
    /// 창은 표시만 하고, 조건 검사(레벨·골드·장착 여부)와 골드·인벤토리 조작은 전부 여기서 한다.
    /// 그래서 다른 곳에서 구매·판매를 일으켜도 규칙이 한 곳에서만 지켜진다.
    ///
    /// 알림 두 가지:
    ///  - SubscribeOnShopStateChanged : 구매·판매가 끝날 때마다 (상점 창 목록·골드 갱신)
    ///  - OnItemPurchased(정적 이벤트) : 무엇을 샀는지 (튜토리얼 퀘스트의 "물약 구매" 판정)
    /// </summary>
    public class ShopController : MonoBehaviour
    {
        /// <summary>한 번에 사고팔 수 있는 최대 개수. 수량 입력 실수로 너무 큰 수가 들어가지 않게 막는다.</summary>
        public const int MaxTradeCount = 999;

        private static ShopController instance;
        public static ShopController Instance => instance;

        /// <summary>구매가 성사될 때마다 산 아이템과 함께 발행한다 (개수와 상관없이 구매 한 번당 한 번).</summary>
        public static event System.Action<Item> OnItemPurchased;

        [Tooltip("이 상점의 판매 목록 (Data/Shop/General Store)")]
        [SerializeField] private ShopData shopData;

        // 구매·판매가 끝날 때마다 발행. 코드에서만 구독하므로 인스펙터에 노출하지 않는다
        private readonly UnityEvent onShopStateChanged = new UnityEvent();

        // 처음 필요할 때 찾아 기억해 둔다 (구매·판매 때마다 씬 전체를 뒤지지 않도록)
        private PlayerLevelController playerLevelController;
        private PlayerEquipment playerEquipment;

        public ShopData Data => shopData;

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

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        public void SubscribeOnShopStateChanged(UnityAction listener)
        {
            onShopStateChanged.AddListener(listener);
        }

        // ───────────────────────── 구매 ─────────────────────────

        /// <summary>
        /// 1개를 살 수 있는지. 상점 창이 버튼 활성화와 안내 문구에도 쓴다.
        /// </summary>
        /// <param name="reason">살 수 없을 때 그 이유 (화면에 그대로 보여 준다)</param>
        public bool CanBuy(ShopData.Entry entry, out string reason)
        {
            reason = string.Empty;

            if (entry == null || entry.item == null)
            {
                reason = "판매하지 않는 물건입니다.";
                return false;
            }

            if (GetPlayerLevel() < entry.requiredLevel)
            {
                reason = $"레벨이 부족합니다. (필요 레벨 {entry.requiredLevel})";
                return false;
            }

            if (CurrencyManager.Instance == null || CurrencyManager.Instance.Gold < entry.price)
            {
                reason = "재화가 부족합니다.";
                return false;
            }

            return true;
        }

        /// <summary>가진 골드로 살 수 있는 최대 개수 (MaxTradeCount를 넘지 않는다). 수량 입력 창의 상한으로 쓴다.</summary>
        public int GetMaxBuyCount(ShopData.Entry entry)
        {
            if (entry == null || entry.item == null || entry.price <= 0 || CurrencyManager.Instance == null)
            {
                return 0;
            }

            return Mathf.Clamp(CurrencyManager.Instance.Gold / entry.price, 0, MaxTradeCount);
        }

        /// <summary>count개를 산다. 실패하면 이유를 안내 창으로 띄우고 false를 돌려준다.</summary>
        public bool TryBuy(ShopData.Entry entry, int count = 1)
        {
            if (!CanBuy(entry, out string reason))
            {
                UISystemMessage.Show(reason);
                return false;
            }

            count = Mathf.Clamp(count, 1, MaxTradeCount);
            int total = entry.price * count;

            // CanBuy는 1개 값만 확인하므로 여러 개는 합계로 다시 확인한다
            if (!CurrencyManager.Instance.TrySpendGold(total))
            {
                UISystemMessage.Show("재화가 부족합니다.");
                return false;
            }

            // 상점 장비는 스탯을 굴리지 않고 원본(최저) 스탯으로 판다. 좋은 스탯은 드랍으로 얻는 몫으로 남겨 둔다
            InventoryManager.Instance.AddItem(entry.item, count, false);

            UISystemMessage.ShowToast(count > 1
                ? $"{entry.item.ColoredName} {count}개를 구매했습니다. (-{total:N0} 골드)"
                : $"{entry.item.ColoredName}을(를) 구매했습니다. (-{total:N0} 골드)");

            onShopStateChanged.Invoke();
            OnItemPurchased?.Invoke(entry.item);

            return true;
        }

        // ───────────────────────── 판매 ─────────────────────────

        /// <summary>
        /// 되팔 때 받는 1개 가격. 아이템 에셋에 판매가가 있으면 그 값, 없으면 상점 목록 가격 x 판매 비율.
        /// 둘 다 없으면 0(팔 수 없음).
        /// </summary>
        public int GetSellPrice(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            if (item.sellPrice > 0)
            {
                return item.sellPrice;
            }

            if (shopData != null)
            {
                foreach (ShopData.Entry entry in shopData.entries)
                {
                    if (entry.item == item)
                    {
                        return Mathf.Max(1, Mathf.RoundToInt(entry.price * shopData.sellRatio));
                    }
                }
            }

            return 0;
        }

        /// <summary>장비가 아닌 아이템(물약·재료)을 count개 판다. 장비는 TrySellEquipment로 개체 단위로 판다.</summary>
        public bool TrySell(Item item, int count = 1)
        {
            // 장비는 개체마다 스탯이 달라 개수로 팔 수 없다
            if (item is EquipmentItem)
            {
                return false;
            }

            int unitPrice = GetSellPrice(item);
            if (unitPrice <= 0)
            {
                UISystemMessage.Show("판매할 수 없는 물건입니다.");
                return false;
            }

            count = Mathf.Clamp(count, 1, MaxTradeCount);

            if (InventoryManager.Instance == null || !InventoryManager.Instance.RemoveItem(item, count))
            {
                return false;
            }

            int total = unitPrice * count;
            CurrencyManager.Instance?.AddGold(total);

            UISystemMessage.ShowToast(count > 1
                ? $"{item.ColoredName} {count}개를 판매했습니다. (+{total:N0} 골드)"
                : $"{item.ColoredName}을(를) 판매했습니다. (+{total:N0} 골드)");

            onShopStateChanged.Invoke();
            return true;
        }

        /// <summary>
        /// 장비 개체 하나를 판다. 판매가는 장비 종류(에셋) 기준이라 같은 종류면 스탯·강화가 달라도 같다.
        /// 장착 중인 장비는 팔 수 없다 (팔린 뒤에도 스탯에 남아 있게 된다).
        /// </summary>
        public bool TrySellEquipment(EquipmentInstance equipment)
        {
            if (equipment == null || equipment.item == null)
            {
                return false;
            }

            int price = GetSellPrice(equipment.item);
            if (price <= 0)
            {
                UISystemMessage.Show("판매할 수 없는 물건입니다.");
                return false;
            }

            PlayerEquipment equipped = GetPlayerEquipment();
            if (equipped != null && equipped.IsEquipped(equipment))
            {
                UISystemMessage.Show("장착 중인 장비는 판매할 수 없습니다.");
                return false;
            }

            if (InventoryManager.Instance == null || !InventoryManager.Instance.RemoveEquipment(equipment))
            {
                return false;
            }

            CurrencyManager.Instance?.AddGold(price);

            UISystemMessage.ShowToast($"{EnhancementManager.GetDisplayName(equipment, true)}을(를) 판매했습니다. (+{price:N0} 골드)");
            onShopStateChanged.Invoke();

            return true;
        }

        // ───────────────────────── 플레이어 참조 ─────────────────────────

        private int GetPlayerLevel()
        {
            if (playerLevelController == null)
            {
                playerLevelController = FindAnyObjectByType<PlayerLevelController>();
            }

            return playerLevelController != null ? playerLevelController.Level : 1;
        }

        private PlayerEquipment GetPlayerEquipment()
        {
            if (playerEquipment == null)
            {
                playerEquipment = FindAnyObjectByType<PlayerEquipment>();
            }

            return playerEquipment;
        }
    }
}
