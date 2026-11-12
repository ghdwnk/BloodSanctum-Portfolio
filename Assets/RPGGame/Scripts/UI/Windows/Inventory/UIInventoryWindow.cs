using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// 인벤토리 창(I). 고정 칸 그리드에 장비/소모품/기타 탭별로 아이템을 보여 준다.
    /// 장비 탭은 장비 개체를 한 칸에 하나씩(장착 중인 장비는 숨긴다), 나머지 탭은 개수로 쌓인 아이템을 보여 준다.
    /// 아이템 목록이나 장착 구성이 바뀌면 자동으로 다시 그린다 (처음 켜질 때 한 번만 구독).
    /// </summary>
    public class UIInventoryWindow : MonoBehaviour
    {
        private static UIInventoryWindow instance;

        [SerializeField] private GameObject window;
        [SerializeField] private RectTransform windowTransform;
        [SerializeField] private Vector2 initialPosition = new Vector2(-30f, -525f);
        [SerializeField] private RectTransform contentTransform;
        [SerializeField] private UIInventoryListItem itemPrefab;
        [SerializeField] private int slotCount = 24;

        [SerializeField] private Button equipmentTabButton;
        [SerializeField] private Button consumableTabButton;
        [SerializeField] private Button etcTabButton;
        [Tooltip("선택되지 않은 탭을 흐리게 표시하기 위한 알파값")]
        [SerializeField] private float unselectedTabAlpha = 0.45f;

        // 고정 슬롯 목록 (기본 slotCount개, 아이템이 더 많으면 RefreshGrid가 늘린다)
        private readonly List<UIInventoryListItem> slots = new();
        // RefreshGrid가 매번 새 리스트를 만들지 않도록 재사용하는 버퍼.
        // 장비 탭은 장비 개체를 한 칸에 하나씩, 그 외 탭은 개수로 쌓인 아이템을 칸마다 보여준다
        private readonly List<ItemSlot> visibleItems = new();
        private readonly List<EquipmentInstance> visibleEquipments = new();
        private Item.Category currentCategory = Item.Category.Equipment;
        // 장착 중인 장비는 인벤토리 목록에서 숨긴다 (캐릭터 상태창에서만 보인다).
        // InventoryManager 자체에서는 빼지 않는다 - 강화창이 "인벤토리의 장비 목록"을
        // 그대로 써서 착용 중인 장비도 강화 대상으로 보여줘야 하기 때문
        private PlayerEquipment playerEquipment;
        // 이벤트 구독을 이미 했는지 (창을 여닫을 때 중복 구독을 막는다)
        private bool isInventorySubscribed;
        private bool isEquipmentSubscribed;

        // UI 인벤토리 창이 열려있는 지를 알려주는 프로퍼티
        public static bool IsOn => instance != null && instance.window.activeSelf;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                Initialize();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Initialize()
        {
            // 고정 슬롯을 미리 생성 (아이템 유무와 상관없이 항상 존재)
            for (int i = 0; i < slotCount; i++)
            {
                UIInventoryListItem slot = Instantiate(itemPrefab, contentTransform);
                slot.SetEmpty();
                slots.Add(slot);
            }

            equipmentTabButton.onClick.AddListener(() => SelectTab(Item.Category.Equipment));
            consumableTabButton.onClick.AddListener(() => SelectTab(Item.Category.Consumable));
            etcTabButton.onClick.AddListener(() => SelectTab(Item.Category.Etc));

            SelectTab(Item.Category.Equipment);
        }

        private void OnEnable()
        {
            // 구독은 처음 켜질 때 한 번만 한다. 창을 열 때마다 구독하면 같은 갱신 함수가 열린 횟수만큼 쌓여
            // 아이템이 바뀔 때마다 목록을 여러 번 다시 그린다
            if (!isInventorySubscribed && InventoryManager.Instance != null)
            {
                InventoryManager.Instance.SubscribeOnItemListChanged(RefreshGrid);
                isInventorySubscribed = true;
            }

            if (playerEquipment == null)
            {
                playerEquipment = FindAnyObjectByType<PlayerEquipment>();
            }

            // 장착·해제 시에도 즉시 목록에서 사라지거나 다시 나타나야 한다
            if (!isEquipmentSubscribed && playerEquipment != null)
            {
                playerEquipment.SubscribeOnEquipmentChanged(RefreshGrid);
                isEquipmentSubscribed = true;
            }
        }

        // 탭(카테고리)을 전환하는 함수
        public void SelectTab(Item.Category category)
        {
            currentCategory = category;

            SetTabButtonHighlight(equipmentTabButton, category == Item.Category.Equipment);
            SetTabButtonHighlight(consumableTabButton, category == Item.Category.Consumable);
            SetTabButtonHighlight(etcTabButton, category == Item.Category.Etc);

            RefreshGrid();
        }

        // 선택된 탭은 원래 색을 그대로, 나머지는 흐리게 표시
        private void SetTabButtonHighlight(Button button, bool selected)
        {
            Image image = button.GetComponent<Image>();
            if (image == null)
            {
                return;
            }

            Color color = image.color;
            color.a = selected ? 1f : unselectedTabAlpha;
            image.color = color;
        }

        // 탭별 칸 배치. 칸 번호 → 그 칸에 놓인 것 (장비는 EquipmentInstance, 그 외는 Item, 빈 칸은 null).
        // 아이템이 늘거나 줄어도 다른 아이템을 앞으로 당기지 않고, 각자 놓인 칸에 그대로 고정된다
        private readonly Dictionary<Item.Category, List<object>> layouts = new();

        private List<object> GetLayout(Item.Category category)
        {
            if (!layouts.TryGetValue(category, out List<object> layout))
            {
                layout = new List<object>();
                layouts.Add(category, layout);
            }

            return layout;
        }

        // 현재 선택된 탭(카테고리)의 아이템을 각자 고정된 칸에 그리는 함수.
        // 새로 생긴 아이템만 빈 칸에 들어가고, 사라진 아이템의 칸은 비워 둔다
        private void RefreshGrid()
        {
            visibleItems.Clear();
            visibleEquipments.Clear();

            if (currentCategory == Item.Category.Equipment)
            {
                foreach (EquipmentInstance equipment in InventoryManager.Instance.GetEquipments())
                {
                    // 장착 중인 개체는 상태창에만 보이면 되므로 인벤토리 목록에서는 건너뛴다.
                    // 같은 종류의 다른 개체는 그대로 목록에 남는다
                    if (playerEquipment != null && playerEquipment.IsEquipped(equipment))
                    {
                        continue;
                    }

                    visibleEquipments.Add(equipment);
                }
            }

            stackLookup.Clear();
            foreach (ItemSlot itemSlot in InventoryManager.Instance.GetItems())
            {
                if (itemSlot.item.category == currentCategory)
                {
                    visibleItems.Add(itemSlot);
                    stackLookup[itemSlot.item] = itemSlot;
                }
            }

            List<object> cells = GetLayout(currentCategory);

            // 지금 보여야 하는 것들의 집합
            visibleKeys.Clear();
            foreach (EquipmentInstance equipment in visibleEquipments) visibleKeys.Add(equipment);
            foreach (ItemSlot itemSlot in visibleItems) visibleKeys.Add(itemSlot.item);

            // 사라진 것(장착·판매·소진)의 칸을 비운다. 장비를 교체해 입을 때 벗겨진 장비가 그 칸에 들어가도록 비운 칸을 기억해 둔다
            freedCells.Clear();
            for (int ix = 0; ix < cells.Count; ++ix)
            {
                if (cells[ix] != null && !visibleKeys.Contains(cells[ix]))
                {
                    cells[ix] = null;
                    freedCells.Add(ix);
                }
            }

            // 새로 생긴 것은 방금 비워진 칸 → 첫 빈 칸 순서로 넣는다
            placedKeys.Clear();
            foreach (object cell in cells)
            {
                if (cell != null) placedKeys.Add(cell);
            }

            foreach (EquipmentInstance equipment in visibleEquipments) PlaceNew(cells, equipment);
            foreach (ItemSlot itemSlot in visibleItems) PlaceNew(cells, itemSlot.item);

            // 뒤쪽의 빈 칸은 잘라내 필요한 칸 수를 정한다
            while (cells.Count > 0 && cells[cells.Count - 1] == null)
            {
                cells.RemoveAt(cells.Count - 1);
            }

            // 기본 슬롯 수(빈 칸 표시용)를 넘으면 모자란 만큼만 더 만든다
            int neededCount = Mathf.Max(slotCount, cells.Count);
            while (slots.Count < neededCount)
            {
                UIInventoryListItem slot = Instantiate(itemPrefab, contentTransform);
                slot.SetEmpty();
                slots.Add(slot);
            }

            for (int ix = 0; ix < slots.Count; ++ix)
            {
                // 필요 없는 여분 슬롯은 꺼서 그리드 레이아웃(스크롤 길이)에서 빠지게 한다
                bool needed = ix < neededCount;
                if (slots[ix].gameObject.activeSelf != needed)
                {
                    slots[ix].gameObject.SetActive(needed);
                }

                if (!needed)
                {
                    continue;
                }

                object cell = ix < cells.Count ? cells[ix] : null;
                if (cell is EquipmentInstance equipmentInstance)
                {
                    slots[ix].SetEquipment(equipmentInstance);
                }
                else if (cell is Item cellItem && stackLookup.TryGetValue(cellItem, out ItemSlot stack))
                {
                    slots[ix].SetItem(stack.item);
                    slots[ix].SetSprite(stack.item.sprite);
                    slots[ix].SetCount(stack.count);
                }
                else
                {
                    slots[ix].SetEmpty();
                }
            }
        }

        private readonly Dictionary<Item, ItemSlot> stackLookup = new();
        private readonly HashSet<object> visibleKeys = new();
        private readonly HashSet<object> placedKeys = new();
        private readonly List<int> freedCells = new();

        // 아직 칸이 없는 아이템을 비워진 칸 → 첫 빈 칸 → 맨 뒤 순서로 놓는다
        private void PlaceNew(List<object> cells, object key)
        {
            if (placedKeys.Contains(key))
            {
                return;
            }

            int target = -1;
            if (freedCells.Count > 0)
            {
                target = freedCells[0];
                freedCells.RemoveAt(0);
            }
            else
            {
                target = cells.IndexOf(null);
            }

            if (target < 0)
            {
                cells.Add(key);
            }
            else
            {
                cells[target] = key;
            }

            placedKeys.Add(key);
        }

        /// <summary>
        /// 칸을 끌어다 놓았을 때 부른다. 놓은 자리에 인벤토리 칸이 있으면 그 칸과 내용을 맞바꾼다 (빈 칸이면 그냥 옮긴다).
        /// 놓은 자리가 인벤토리 칸이 아니면 false (퀵슬롯 등록 같은 다른 드롭 처리를 막지 않는다).
        /// </summary>
        public static bool TryDropOnSlot(UIInventoryListItem source, Vector2 screenPosition, Camera eventCamera)
        {
            return instance != null && instance.MoveToSlotAt(source, screenPosition, eventCamera);
        }

        private bool MoveToSlotAt(UIInventoryListItem source, Vector2 screenPosition, Camera eventCamera)
        {
            int from = slots.IndexOf(source);
            if (from < 0)
            {
                return false;
            }

            int to = -1;
            for (int ix = 0; ix < slots.Count; ++ix)
            {
                if (!slots[ix].gameObject.activeInHierarchy)
                {
                    continue;
                }

                RectTransform rect = slots[ix].transform as RectTransform;
                if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, eventCamera))
                {
                    to = ix;
                    break;
                }
            }

            if (to < 0)
            {
                return false;
            }

            if (to != from)
            {
                List<object> cells = GetLayout(currentCategory);
                while (cells.Count <= Mathf.Max(from, to))
                {
                    cells.Add(null);
                }

                (cells[from], cells[to]) = (cells[to], cells[from]);
                RefreshGrid();
            }

            return true;
        }

        // 인벤토리 창 열기 함수
        public static void Show()
        {
            UISound.SetActive(instance.window, true);

            // 드래그로 옮겨놨어도 열 때마다 항상 초기 위치로 되돌림
            if (instance.windowTransform != null)
            {
                instance.windowTransform.anchoredPosition = instance.initialPosition;
            }
        }

        // 인벤토리 창 닫기 함수
        public static void Close()
        {
            UISound.SetActive(instance.window, false);
        }
    }
}
