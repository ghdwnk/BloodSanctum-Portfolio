using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 체력/마나 바 옆에 항상 표시되는 퀵슬롯 6칸을 관리. 스킬 쿨다운/아이템 개수를 실시간 갱신한다
    /// </summary>
    public class UIQuickSlotBar : MonoBehaviour
    {
        [SerializeField] private UIQuickSlotItem[] slots;

        private QuickSlotController quickSlotController;
        private PlayerSkillController skillController;

        private void OnEnable()
        {
            if (quickSlotController == null)
            {
                quickSlotController = FindAnyObjectByType<QuickSlotController>();
            }

            if (skillController == null)
            {
                skillController = FindAnyObjectByType<PlayerSkillController>();
            }

            if (quickSlotController != null)
            {
                for (int i = 0; i < slots.Length; ++i)
                {
                    slots[i].Initialize(quickSlotController, i, i + 1);
                }
            }

            RefreshAll();
        }

        private void Update()
        {
            RefreshAll();
        }

        private void RefreshAll()
        {
            if (quickSlotController == null)
            {
                return;
            }

            for (int i = 0; i < slots.Length; ++i)
            {
                RefreshSlot(i);
            }
        }

        public void RefreshSlot(int index)
        {
            if (quickSlotController == null || index < 0 || index >= slots.Length)
            {
                return;
            }

            QuickSlotController.SlotEntry slot = quickSlotController.GetSlot(index);
            UIQuickSlotItem slotItem = slots[index];

            if (slot == null || slot.IsEmpty)
            {
                slotItem.SetEmpty();
                return;
            }

            if (slot.skill != null)
            {
                slotItem.SetIcon(slot.skill.icon);

                float remaining = skillController != null ? skillController.GetCooldownRemaining(slot.skill) : 0f;
                slotItem.SetCooldown(remaining, skillController != null ? skillController.GetCooldownDuration(slot.skill) : slot.skill.cooldown);
                slotItem.SetCount(0, false);
                return;
            }

            if (slot.equipment != null)
            {
                slotItem.SetIcon(slot.equipment.sprite);
                slotItem.SetCooldown(QuickSlotController.EquipCooldownRemaining, QuickSlotController.EquipCooldown);
                slotItem.SetCountText(slot.equipment.enhanceLevel > 0 ? $"+{slot.equipment.enhanceLevel}" : string.Empty);
                return;
            }

            if (slot.item != null)
            {
                slotItem.SetIcon(slot.item.sprite);

                // 체력물약은 종류와 상관없이 공용 쿨타임을 표시한다
                if (slot.item is HealthItem)
                {
                    slotItem.SetCooldown(HealthItem.CooldownRemaining, HealthItem.PotionCooldown);
                }
                else
                {
                    slotItem.SetCooldown(0f, 0f);
                }

                int count = 0;
                if (InventoryManager.Instance != null)
                {
                    ItemSlot itemSlot = InventoryManager.Instance.GetItemSlot(slot.item);
                    count = itemSlot?.count ?? 0;
                }

                slotItem.SetCount(count, true);
            }
        }
    }
}
