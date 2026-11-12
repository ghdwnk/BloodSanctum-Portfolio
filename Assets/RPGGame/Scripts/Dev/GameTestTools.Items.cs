using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// GameTestTools - 재화·아이템·장비
    /// </summary>
    public partial class GameTestTools
    {
        [SerializeField, Min(0)] private int goldAmount = 100000;
        [SerializeField, Min(0)] private int enhanceStoneAmount = 100;

        [Tooltip("'아이템 지급'으로 인벤토리에 넣을 아이템")]
        [SerializeField] private Item giveItem;
        [SerializeField, Min(1)] private int giveItemCount = 1;

        [Tooltip("꺼짐: 장비를 원본 수치 그대로 지급한다. 켜짐: 실제 드랍처럼 스탯을 랜덤으로 굴린다")]
        [SerializeField] private bool rollEquipmentStats;

        [Tooltip("'지급하고 착용'으로 입을 장비")]
        [SerializeField] private EquipmentItem equipItem;
        [SerializeField, Range(0, EnhancementData.FixedMaxLevel)] private int equipItemEnhanceLevel;
        [Tooltip("'착용 중인 장비 강화 적용'이 맞출 강화 단계")]
        [SerializeField, Range(0, EnhancementData.FixedMaxLevel)] private int equippedEnhanceLevel = EnhancementData.FixedMaxLevel;

        // 강화에 쓰는 재료 아이템(강화석). 강화 데이터에 지정된 것을 그대로 쓴다
        private MaterialItem EnhanceStone =>
            EnhancementManager.Instance != null && EnhancementManager.Instance.Data != null
                ? EnhancementManager.Instance.Data.materialItem
                : null;

        public int CurrentGold => CurrencyManager.Instance != null ? CurrencyManager.Instance.Gold : 0;

        public int CurrentEnhanceStones =>
            EnhanceStone != null && InventoryManager.Instance != null ? InventoryManager.Instance.GetItemCount(EnhanceStone) : 0;

        // ── 골드 ──

        public void SetGold()
        {
            if (IsAllowed && CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.SetGold(goldAmount);
            }
        }

        public void AddGold()
        {
            if (IsAllowed && CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.AddGold(goldAmount);
            }
        }

        // ── 강화석 ──

        // 강화석 보유 수를 입력한 값으로 맞춘다 (모자라면 채우고 남으면 덜어낸다)
        public void SetEnhanceStones()
        {
            MaterialItem stone = EnhanceStone;
            if (!IsAllowed || stone == null || InventoryManager.Instance == null)
            {
                Debug.LogWarning("[GameTestTools] 강화석 아이템을 찾지 못했습니다. EnhancementManager의 강화 데이터를 확인하세요.");
                return;
            }

            int difference = enhanceStoneAmount - InventoryManager.Instance.GetItemCount(stone);
            if (difference > 0)
            {
                InventoryManager.Instance.AddItem(stone, difference);
            }
            else if (difference < 0)
            {
                InventoryManager.Instance.RemoveItem(stone, -difference);
            }
        }

        public void AddEnhanceStones()
        {
            MaterialItem stone = EnhanceStone;
            if (IsAllowed && stone != null && InventoryManager.Instance != null)
            {
                InventoryManager.Instance.AddItem(stone, enhanceStoneAmount);
            }
        }

        // ── 아이템 ──

        public void GiveItem()
        {
            if (!IsAllowed || giveItem == null || InventoryManager.Instance == null)
            {
                return;
            }

            InventoryManager.Instance.AddItem(giveItem, giveItemCount, rollEquipmentStats);
        }

        // 아이템 데이터베이스에 있는 장비를 종류마다 1개씩 지급한다
        public void GiveAllEquipment()
        {
            if (!IsAllowed || InventoryManager.Instance == null || DataManager.Instance.itemDatabase == null)
            {
                return;
            }

            int count = 0;
            foreach (Item item in DataManager.Instance.itemDatabase.items)
            {
                if (item is EquipmentItem)
                {
                    InventoryManager.Instance.AddItem(item, 1, rollEquipmentStats);
                    ++count;
                }
            }

            Debug.Log($"[GameTestTools] 장비 {count}종을 지급했습니다.");
        }

        // 장비를 제외한 소모품·재료를 종류마다 '지급할 개수'만큼 지급한다
        public void GiveAllConsumables()
        {
            if (!IsAllowed || InventoryManager.Instance == null || DataManager.Instance.itemDatabase == null)
            {
                return;
            }

            int count = 0;
            foreach (Item item in DataManager.Instance.itemDatabase.items)
            {
                if (item != null && !(item is EquipmentItem))
                {
                    InventoryManager.Instance.AddItem(item, giveItemCount);
                    ++count;
                }
            }

            Debug.Log($"[GameTestTools] 소모품·재료 {count}종을 {giveItemCount}개씩 지급했습니다.");
        }

        // ── 장비 ──

        // 지정한 장비를 새로 만들어 지급하고 바로 착용한다. 레벨 제한이 없는 게임이라 어떤 장비든 입을 수 있다
        public void GiveAndEquip()
        {
            if (!IsAllowed || equipItem == null || InventoryManager.Instance == null || !EnsurePlayer())
            {
                return;
            }

            PlayerEquipment equipment = playerState.GetComponent<PlayerEquipment>();
            if (equipment == null)
            {
                return;
            }

            EquipmentInstance instance = EquipmentInstance.Create(equipItem, rollEquipmentStats);
            instance.enhanceLevel = equipItemEnhanceLevel;

            InventoryManager.Instance.AddEquipment(instance);
            equipment.Equip(instance);
        }

        // 착용 중인 장비(무기·갑옷·투구)의 강화 단계를 입력한 값으로 맞추고 스탯을 다시 계산한다
        public void ApplyEquippedEnhance()
        {
            if (!IsAllowed || !EnsurePlayer())
            {
                return;
            }

            PlayerEquipment equipment = playerState.GetComponent<PlayerEquipment>();
            if (equipment == null)
            {
                return;
            }

            foreach (EquipmentItem.EquipType type in System.Enum.GetValues(typeof(EquipmentItem.EquipType)))
            {
                EquipmentInstance instance = equipment.GetEquipped(type);
                if (instance != null)
                {
                    instance.enhanceLevel = equippedEnhanceLevel;
                }
            }

            // 같은 개체를 다시 착용한 것으로 처리해 보정치와 무기 강화 이펙트를 갱신시킨다
            equipment.LoadState(equipment.GetEquippedIds());
        }
    }
}
