using UnityEngine;
using UnityEngine.Events;

namespace RPGGame
{
    /// <summary>
    /// Q, E, R, 1, 2, 3 키에 스킬이나 아이템(물약)을 등록해 두고 바로 쓰게 하는 퀵슬롯. 플레이어 루트에 붙어 있다.
    ///
    /// 스킬 창·인벤토리 창에서 퀵슬롯 칸(UIQuickSlotItem)으로 끌어다 놓으면 AssignSkill / AssignItem이 불린다.
    /// 칸 내용이 바뀔 때마다 칸 번호와 함께 알린다 (퀵슬롯 HUD 갱신, 튜토리얼 퀘스트의 "등록" 단계 판정).
    /// 등록 내용은 세이브하지 않는다.
    /// </summary>
    public class QuickSlotController : MonoBehaviour
    {
        /// <summary>퀵슬롯 한 칸. 스킬과 아이템 중 하나만 들어간다.</summary>
        [System.Serializable]
        public class SlotEntry
        {
            public SkillData skill;
            public Item item;
            // 장비는 같은 종류라도 개체마다 스탯·강화가 달라서 에셋(item)이 아니라 개체를 등록한다
            public EquipmentInstance equipment;

            public bool IsEmpty => skill == null && item == null && equipment == null;

            public void Clear()
            {
                skill = null;
                item = null;
                equipment = null;
            }
        }

        /// <summary>단축키로 장비를 바꿔 낄 때의 재사용 대기 시간(초). 모든 장비 칸이 함께 쓴다.</summary>
        public const float EquipCooldown = 3f;

        // 마지막으로 장비를 바꿔 낀 시각(Time.time). 장비 칸 전체가 공용 쿨타임을 쓰므로 정적 값으로 둔다
        private static float lastEquipTime = -EquipCooldown;

        /// <summary>장비 교체 쿨타임이 남은 시간(초). 0이면 바로 바꿀 수 있다.</summary>
        public static float EquipCooldownRemaining => Mathf.Max(0f, lastEquipTime + EquipCooldown - Time.time);

        // 에디터에서 도메인 리로드 없이 플레이를 다시 시작해도 이전 플레이의 쿨타임이 남지 않게 비운다
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            lastEquipTime = -EquipCooldown;
        }

        private readonly SlotEntry[] slots = CreateSlots();

        // 칸 내용이 바뀔 때 바뀐 칸 번호와 함께 발행. 코드에서만 구독하므로 인스펙터에 노출하지 않는다
        private readonly UnityEvent<int> onSlotChanged = new UnityEvent<int>();

        private PlayerSkillController skillController;
        private PlayerStateManager stateManager;
        private PlayerEquipment playerEquipment;

        /// <summary>칸 수 (6).</summary>
        public int Count => slots.Length;

        private static SlotEntry[] CreateSlots()
        {
            SlotEntry[] created = new SlotEntry[InputManager.QuickSlotKeyCount];
            for (int ix = 0; ix < created.Length; ++ix)
            {
                created[ix] = new SlotEntry();
            }

            return created;
        }

        private void Awake()
        {
            skillController = GetComponent<PlayerSkillController>();
            stateManager = GetComponent<PlayerStateManager>();
            playerEquipment = GetComponentInChildren<PlayerEquipment>();
        }

        private void Update()
        {
            if (stateManager != null && stateManager.IsPlayerDead)
            {
                return;
            }

            for (int ix = 0; ix < slots.Length; ++ix)
            {
                if (InputManager.IsQuickSlotPressed(ix))
                {
                    Activate(ix);
                }
            }
        }

        public void SubscribeOnSlotChanged(UnityAction<int> listener)
        {
            onSlotChanged.AddListener(listener);
        }

        /// <summary>index번 칸. 범위 밖이면 null.</summary>
        public SlotEntry GetSlot(int index)
        {
            return IsValidIndex(index) ? slots[index] : null;
        }

        /// <summary>스킬을 등록한다. 아직 배우지 않은 스킬은 등록할 수 없다.</summary>
        public void AssignSkill(int index, SkillData skill)
        {
            if (!IsValidIndex(index) || skill == null)
            {
                return;
            }

            if (skillController != null && !skillController.IsSkillUnlocked(skill))
            {
                UISystemMessage.ShowToastWarning($"{skill.skillName}은(는) 먼저 스킬창에서 배워야 합니다.");
                return;
            }

            slots[index].Clear();
            slots[index].skill = skill;
            onSlotChanged.Invoke(index);
        }

        /// <summary>아이템을 등록한다.</summary>
        public void AssignItem(int index, Item item)
        {
            if (!IsValidIndex(index) || item == null)
            {
                return;
            }

            slots[index].Clear();
            slots[index].item = item;
            onSlotChanged.Invoke(index);
        }

        /// <summary>장비 개체를 등록한다. 단축키를 누르면 이 장비로 바꿔 낀다.</summary>
        public void AssignEquipment(int index, EquipmentInstance equipment)
        {
            if (!IsValidIndex(index) || equipment == null || equipment.item == null)
            {
                return;
            }

            slots[index].Clear();
            slots[index].equipment = equipment;
            onSlotChanged.Invoke(index);
        }

        /// <summary>두 칸의 내용을 맞바꾼다 (한쪽이 비어 있으면 옮기는 효과).</summary>
        public void SwapSlots(int indexA, int indexB)
        {
            if (!IsValidIndex(indexA) || !IsValidIndex(indexB) || indexA == indexB)
            {
                return;
            }

            (slots[indexA], slots[indexB]) = (slots[indexB], slots[indexA]);
            onSlotChanged.Invoke(indexA);
            onSlotChanged.Invoke(indexB);
        }

        /// <summary>칸을 비운다 (우클릭).</summary>
        public void ClearSlot(int index)
        {
            if (!IsValidIndex(index))
            {
                return;
            }

            slots[index].Clear();
            onSlotChanged.Invoke(index);
        }

        private bool IsValidIndex(int index)
        {
            return index >= 0 && index < slots.Length;
        }

        // 단축키를 눌렀을 때: 스킬이면 시전하고, 아이템이면 가지고 있을 때만 사용한다
        private void Activate(int index)
        {
            SlotEntry slot = slots[index];
            if (slot.IsEmpty)
            {
                return;
            }

            if (slot.skill != null)
            {
                skillController.TryCastSkill(slot.skill);
                return;
            }

            if (slot.equipment != null)
            {
                SwapEquipment(index, slot);
                return;
            }

            if (InventoryManager.Instance == null || !InventoryManager.Instance.HasItem(slot.item))
            {
                UISystemMessage.ShowToastWarning($"{slot.item.itemName}이(가) 없습니다.");
                return;
            }

            slot.item.Use();
        }
        // 장비 칸 단축키: 이 칸의 장비를 끼고, 원래 끼고 있던 장비는 이 칸에 들어간다 (다시 누르면 되돌아온다)
        private void SwapEquipment(int index, SlotEntry slot)
        {
            EquipmentInstance target = slot.equipment;

            // 팔거나 버려서 인벤토리에서 사라진 장비면 칸을 비운다
            if (InventoryManager.Instance == null || !ContainsEquipment(target))
            {
                UISystemMessage.ShowToastWarning($"{target.item.itemName}이(가) 없습니다.");
                ClearSlot(index);
                return;
            }

            if (playerEquipment == null)
            {
                return;
            }

            if (playerEquipment.IsEquipped(target))
            {
                UISystemMessage.ShowToastWarning($"{target.item.itemName}은(는) 이미 장착 중입니다.");
                return;
            }

            // 공격·구르기·사망 같은 동작 중에는 바꿔 낄 수 없다. 대기(Idle)나 이동(Move)일 때만 가능하다
            if (stateManager != null
                && stateManager.CurrentState != PlayerStateManager.State.Idle
                && stateManager.CurrentState != PlayerStateManager.State.Move)
            {
                UISystemMessage.ShowToastWarning("지금은 장비를 바꿀 수 없습니다.");
                return;
            }

            float remaining = EquipCooldownRemaining;
            if (remaining > 0f)
            {
                UISystemMessage.ShowToastWarning($"장비를 바꾸려면 {Mathf.CeilToInt(remaining)}초 기다려야 합니다.");
                return;
            }

            // 같은 부위에 끼고 있던 장비를 미리 기억해 두었다가 이 칸에 넣는다
            EquipmentInstance previous = playerEquipment.GetEquipped(target.item.equipType);
            playerEquipment.Equip(target);
            lastEquipTime = Time.time;

            slot.equipment = previous;
            onSlotChanged.Invoke(index);
        }

        private static bool ContainsEquipment(EquipmentInstance equipment)
        {
            foreach (EquipmentInstance owned in InventoryManager.Instance.GetEquipments())
            {
                if (owned == equipment)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
