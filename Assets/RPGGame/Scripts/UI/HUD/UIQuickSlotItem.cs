using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// 퀵슬롯 하나의 아이콘/쿨다운/개수 표시, 드래그 드롭 수신(다른 스킬/아이템/퀵슬롯), 우클릭 해제를 담당하는 뷰
    /// </summary>
    public class UIQuickSlotItem : MonoBehaviour, IDropHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Image cooldownOverlayImage;
        [SerializeField] private TMPro.TextMeshProUGUI cooldownText;
        [SerializeField] private TMPro.TextMeshProUGUI countText;

        private QuickSlotController quickSlotController;
        private int slotIndex;
        private RectTransform dragIcon;

        public int SlotIndex => slotIndex;

        public void Initialize(QuickSlotController controller, int index, int keyNumber)
        {
            quickSlotController = controller;
            slotIndex = index;
        }

        public void SetEmpty()
        {
            SetIcon(null);
            SetCooldown(0f, 0f);
            SetCount(0, false);
        }

        public void SetIcon(Sprite sprite)
        {
            if (iconImage == null)
            {
                return;
            }

            iconImage.sprite = sprite;

            // 빈 칸도 드래그한 스킬/아이템을 받아야 하므로 Image를 끄지 않고 투명하게만 둔다.
            // 슬롯 루트에는 Graphic이 없어서, 아이콘 Image가 꺼지면 이 칸 위에는 레이캐스트를 받는 대상이 사라져 드롭이 전달되지 않는다
            // (알파 0인 Image도 레이캐스트는 받는다)
            iconImage.enabled = true;
            iconImage.color = sprite != null ? Color.white : Color.clear;
        }

        // remaining/total이 모두 0이면 쿨다운 표시를 하지 않는 스킬(또는 아이템) 슬롯을 의미
        public void SetCooldown(float remaining, float total)
        {
            bool onCooldown = remaining > 0f;

            if (cooldownOverlayImage != null)
            {
                cooldownOverlayImage.fillAmount = total > 0f ? Mathf.Clamp01(remaining / total) : 0f;
            }

            if (cooldownText != null)
            {
                cooldownText.text = onCooldown ? Mathf.CeilToInt(remaining).ToString() : string.Empty;
            }
        }

        /// <summary>개수 자리에 문자열을 그대로 쓴다 (장비 칸의 "+N" 강화 단계).</summary>
        public void SetCountText(string text)
        {
            if (countText != null)
            {
                countText.text = text;
            }
        }

        public void SetCount(int count, bool visible)
        {
            if (countText != null)
            {
                countText.text = visible ? count.ToString() : string.Empty;
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (quickSlotController == null || eventData.pointerDrag == null)
            {
                return;
            }

            // 다른 퀵슬롯에서 드래그해온 경우 - 두 슬롯의 내용을 서로 바꿈(빈 슬롯이면 그냥 이동)
            UIQuickSlotItem quickSlotSource = eventData.pointerDrag.GetComponent<UIQuickSlotItem>();
            if (quickSlotSource != null && quickSlotSource != this)
            {
                quickSlotController.SwapSlots(quickSlotSource.SlotIndex, slotIndex);
                return;
            }

            UISkillSlotItem skillSource = eventData.pointerDrag.GetComponent<UISkillSlotItem>();
            if (skillSource != null && skillSource.CurrentSkill != null)
            {
                quickSlotController.AssignSkill(slotIndex, skillSource.CurrentSkill);
                return;
            }

            UIInventoryListItem itemSource = eventData.pointerDrag.GetComponent<UIInventoryListItem>();
            if (itemSource != null && itemSource.equipment != null)
            {
                // 장비는 끌어온 칸의 개체 그대로 등록한다 (에셋이 아니라 이 개체의 스탯·강화를 쓴다)
                quickSlotController.AssignEquipment(slotIndex, itemSource.equipment);
                return;
            }

            if (itemSource != null && itemSource.item != null)
            {
                quickSlotController.AssignItem(slotIndex, itemSource.item);
            }
        }

        // 우클릭으로 퀵슬롯 등록 해제
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Right || quickSlotController == null)
            {
                return;
            }

            quickSlotController.ClearSlot(slotIndex);
        }

        // 퀵슬롯 재배치를 위한 드래그 소스 - 왼쪽 버튼일 때만 시작해서 우클릭(해제)과 겹치지 않게 함
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || quickSlotController == null)
            {
                return;
            }

            QuickSlotController.SlotEntry slot = quickSlotController.GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty)
            {
                return;
            }

            Sprite sprite = slot.skill != null ? slot.skill.icon
                : slot.equipment != null ? slot.equipment.sprite
                : slot.item.sprite;
            if (sprite == null)
            {
                return;
            }

            Canvas rootCanvas = GetComponentInParent<Canvas>().rootCanvas;
            dragIcon = UIDragIcon.Create(sprite, rootCanvas.transform, new Vector2(56f, 56f));
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragIcon != null)
            {
                dragIcon.position = eventData.position;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (dragIcon != null)
            {
                Destroy(dragIcon.gameObject);
                dragIcon = null;
            }
        }
    }
}
