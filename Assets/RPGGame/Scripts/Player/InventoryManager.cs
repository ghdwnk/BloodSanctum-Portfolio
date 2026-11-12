using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace RPGGame
{
    /// <summary>인벤토리의 "개수로 쌓이는 아이템" 한 칸 (물약·재료).</summary>
    [System.Serializable]
    public class ItemSlot
    {
        public Item item;
        public int count;

        public ItemSlot(Item item, int count)
        {
            this.item = item;
            this.count = count;
        }

        public void AddCount(int amount)
        {
            count += amount;
        }
    }

    /// <summary>
    /// 플레이어 인벤토리. 게임 씬의 매니저 오브젝트에 하나만 둔다.
    ///
    /// 두 가지로 나눠 보관한다.
    ///  - 물약·재료: 같은 에셋을 개수로 쌓는다 (Item → ItemSlot 사전)
    ///  - 장비: 얻을 때마다 스탯이 달라 쌓지 않고 개체(EquipmentInstance)를 한 개씩 목록에 둔다 (얻은 순서)
    ///
    /// 내용이 바뀌는 모든 경로(드랍·퀘스트 보상·상점·사용·강화)가 여기를 거치고, 바뀔 때마다 한 번 알린다.
    /// 인벤토리 창·퀵슬롯·수집 퀘스트·강화 창이 SubscribeOnItemListChanged로 구독한다.
    /// 세이브 불러오기는 Load* 함수로 알림 없이 채운 뒤 NotifyItemListChanged로 한 번에 알린다.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class InventoryManager : MonoBehaviour
    {
        private static InventoryManager instance;
        public static InventoryManager Instance => instance;

        // 개수로 쌓이는 아이템 (물약·재료)
        private readonly Dictionary<Item, ItemSlot> items = new();
        // 장비 개체 (얻은 순서)
        private readonly List<EquipmentInstance> equipments = new();

        // 목록이 바뀔 때마다 발행. 코드에서만 구독하므로 인스펙터에 노출하지 않는다
        private readonly UnityEvent onItemListChanged = new UnityEvent();

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

        public void SubscribeOnItemListChanged(UnityAction listener)
        {
            onItemListChanged.AddListener(listener);
        }

        /// <summary>세이브 불러오기처럼 알림 없이 채운 뒤 화면을 한 번에 갱신할 때 부른다.</summary>
        public void NotifyItemListChanged()
        {
            onItemListChanged.Invoke();
        }

        // ───────────────────────── 조회 ─────────────────────────

        /// <summary>개수로 쌓이는 아이템의 칸. 없으면 null.</summary>
        public ItemSlot GetItemSlot(Item item)
        {
            return items.TryGetValue(item, out ItemSlot itemSlot) ? itemSlot : null;
        }

        /// <summary>개수로 쌓이는 아이템 목록 (장비 제외). 부를 때마다 새 목록을 만든다.</summary>
        public List<ItemSlot> GetItems()
        {
            return new List<ItemSlot>(items.Values);
        }

        /// <summary>장비 개체 목록 (얻은 순서, 읽기 전용).</summary>
        public IReadOnlyList<EquipmentInstance> GetEquipments()
        {
            return equipments;
        }

        /// <summary>가진 개수. 장비면 같은 종류의 개체 수를 센다. 없으면 0.</summary>
        public int GetItemCount(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            if (item is EquipmentItem)
            {
                int total = 0;
                foreach (EquipmentInstance equipment in equipments)
                {
                    if (equipment.item == item)
                    {
                        ++total;
                    }
                }

                return total;
            }

            return items.TryGetValue(item, out ItemSlot slot) ? slot.count : 0;
        }

        public bool HasItem(Item item)
        {
            return GetItemCount(item) > 0;
        }

        /// <summary>개체 ID로 장비를 찾는다 (세이브의 장착 상태 복원용). 없으면 null.</summary>
        public EquipmentInstance FindEquipmentById(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            foreach (EquipmentInstance equipment in equipments)
            {
                if (equipment.id == id)
                {
                    return equipment;
                }
            }

            return null;
        }

        // ───────────────────────── 넣기 / 빼기 ─────────────────────────

        /// <summary>
        /// 아이템을 count개 넣는다. 장비는 개수로 합치지 않고 count개의 개체를 따로 만든다
        /// (rollStats가 true면 개체마다 스탯을 새로 굴리고, false면 원본 스탯 그대로).
        /// </summary>
        public void AddItem(Item item, int count = 1, bool rollStats = true)
        {
            if (item == null || count <= 0)
            {
                return;
            }

            if (item is EquipmentItem equipmentItem)
            {
                for (int ix = 0; ix < count; ++ix)
                {
                    equipments.Add(EquipmentInstance.Create(equipmentItem, rollStats));
                }
            }
            else if (items.TryGetValue(item, out ItemSlot itemSlot))
            {
                itemSlot.AddCount(count);
            }
            else
            {
                items.Add(item, new ItemSlot(item, count));
            }

            // 넣는 쪽(드랍·보상·상점·지급)마다 알림을 따로 챙기면 하나만 빠져도 창이 열려 있는 동안 새 아이템이 안 보인다.
            // 목록이 바뀌는 이 자리에서 한 번에 알린다
            onItemListChanged.Invoke();
        }

        /// <summary>이미 만들어진 장비 개체 하나를 넣는다 (기본 지급 장비 등).</summary>
        public void AddEquipment(EquipmentInstance equipment)
        {
            if (equipment == null || equipment.item == null)
            {
                return;
            }

            equipments.Add(equipment);
            onItemListChanged.Invoke();
        }

        /// <summary>count개를 뺀다 (판매·강화 재료·퀘스트 제출). 모자라면 아무것도 빼지 않고 false.</summary>
        public bool RemoveItem(Item item, int count = 1)
        {
            if (item == null || count <= 0 || !items.TryGetValue(item, out ItemSlot slot) || slot.count < count)
            {
                return false;
            }

            slot.count -= count;
            if (slot.count <= 0)
            {
                items.Remove(item);
            }

            onItemListChanged.Invoke();
            return true;
        }

        /// <summary>
        /// 장비 두 개체의 목록 위치를 맞바꾼다. 장비를 교체해 입을 때, 벗겨진 장비가 입은 장비가 있던 칸에 그대로 들어가게 하려는 용도다.
        /// 인벤토리 창은 이 목록 순서대로 칸을 채우고 장착 중인 개체만 숨기므로, 위치를 바꿔 두면 칸이 1:1로 교체된다.
        /// 알림은 부르지 않는다 (호출하는 쪽이 장착 변경 알림으로 창을 다시 그린다).
        /// </summary>
        public void SwapEquipmentPositions(EquipmentInstance a, EquipmentInstance b)
        {
            int indexA = equipments.IndexOf(a);
            int indexB = equipments.IndexOf(b);

            if (indexA < 0 || indexB < 0 || indexA == indexB)
            {
                return;
            }

            equipments[indexA] = b;
            equipments[indexB] = a;
        }

        /// <summary>장비 개체 하나를 뺀다 (판매).</summary>
        public bool RemoveEquipment(EquipmentInstance equipment)
        {
            if (equipment == null || !equipments.Remove(equipment))
            {
                return false;
            }

            onItemListChanged.Invoke();
            return true;
        }

        /// <summary>아이템을 사용해 1개 줄인다 (HealthItem.Use가 부른다).</summary>
        public void OnItemUsed(Item item)
        {
            if (items.TryGetValue(item, out ItemSlot itemSlot))
            {
                --itemSlot.count;
                if (itemSlot.count <= 0)
                {
                    items.Remove(item);
                }
            }

            onItemListChanged.Invoke();
        }

        // ───────────────────────── 세이브 불러오기 (알림 없음) ─────────────────────────

        /// <summary>아이템 개수를 그대로 정한다 (더하기가 아니라 지정).</summary>
        public void LoadItem(Item item, int count)
        {
            if (item == null || count <= 0)
            {
                return;
            }

            items[item] = new ItemSlot(item, count);
        }

        /// <summary>장비 목록을 비운다.</summary>
        public void ClearEquipments()
        {
            equipments.Clear();
        }

        /// <summary>복원한 장비 개체를 넣는다.</summary>
        public void LoadEquipment(EquipmentInstance equipment)
        {
            if (equipment != null && equipment.item != null)
            {
                equipments.Add(equipment);
            }
        }
    }
}
