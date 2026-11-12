using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// 캐릭터 정보창의 장비 슬롯 하나. 해당 부위에 장착된 아이템을 보여주고,
    /// 우클릭하면 해제하고, 좌클릭으로 끌어서 인벤토리 창 위에 놓아도 해제된다. 마우스를 올리면 툴팁을 띄운다.
    /// (인벤토리의 우클릭 장착/해제와 같은 소리를 낸다)
    /// </summary>
    public class UIEquipmentSlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private EquipmentItem.EquipType equipType;
        [SerializeField] private Image iconImage;
        [Tooltip("등급 색을 입히는 테두리")]
        [SerializeField] private Image frameImage;
        [Tooltip("비어 있을 때 보여줄 부위 이름")]
        [SerializeField] private TMPro.TextMeshProUGUI labelText;

        private EquipmentInstance currentItem;

        public EquipmentItem.EquipType EquipType => equipType;

        // 슬롯에 장착 아이템을 반영하는 함수 (null이면 빈 슬롯)
        public void SetItem(EquipmentInstance equipped)
        {
            currentItem = equipped;
            EquipmentItem item = equipped != null ? equipped.item : null;

            if (iconImage != null)
            {
                iconImage.sprite = item != null ? item.sprite : null;
                iconImage.enabled = item != null && item.sprite != null;
            }

            if (frameImage != null)
            {
                // 빈 슬롯은 어두운 회색 테두리, 장착 중이면 등급 색
                frameImage.color = item != null ? item.GradeColor : new Color(0.35f, 0.35f, 0.35f, 1f);
            }

            if (labelText != null)
            {
                labelText.text = GetTypeName();
                // 아이템이 있으면 아이콘에 가리지 않도록 부위 이름을 흐리게
                labelText.alpha = item != null ? 0.4f : 1f;
            }
        }

        private string GetTypeName()
        {
            switch (equipType)
            {
                case EquipmentItem.EquipType.Armor: return "갑옷";
                case EquipmentItem.EquipType.Helmet: return "투구";
                default: return "무기";
            }
        }

        // 우클릭으로만 해제한다. 좌클릭은 드래그 시작용이라 단순 클릭은 아무 일도 하지 않는다
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Right)
            {
                return;
            }

            Unequip();
        }

        // 장착 해제 + 창 여닫기 소리 + 툴팁 정리. 우클릭과 드래그 해제가 같이 쓴다
        private void Unequip()
        {
            if (currentItem == null)
            {
                return;
            }

            PlayerEquipment equipment = FindAnyObjectByType<PlayerEquipment>();
            if (equipment != null)
            {
                UISound.Play();
                equipment.Unequip(equipType);
                UIItemTooltip.Hide();
            }
        }

        // ───────────────────────── 드래그 ─────────────────────────

        private RectTransform dragIcon;
        // 좌클릭 드래그만 인정한다 (우클릭으로 끌다 놓았을 때 해제되지 않게)
        private bool isDragging;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (currentItem == null || currentItem.item.sprite == null || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            isDragging = true;
            UIItemTooltip.Hide();

            Canvas rootCanvas = GetComponentInParent<Canvas>().rootCanvas;
            dragIcon = UIDragIcon.Create(currentItem.item.sprite, rootCanvas.transform, new Vector2(64f, 64f));
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragIcon != null)
            {
                dragIcon.position = eventData.position;
            }
        }

        // 놓은 자리에 인벤토리 창(칸 또는 배경)이 있으면 해제한다. 다른 곳에 놓으면 그대로 장착 상태다
        public void OnEndDrag(PointerEventData eventData)
        {
            if (dragIcon != null)
            {
                Destroy(dragIcon.gameObject);
                dragIcon = null;
            }

            if (!isDragging)
            {
                return;
            }

            isDragging = false;

            if (IsOverInventoryWindow(eventData))
            {
                Unequip();
            }
        }

        // 놓은 위치 아래의 UI를 훑어 인벤토리 창에 속한 것이 하나라도 있는지 확인한다
        private static readonly System.Collections.Generic.List<RaycastResult> raycastResults = new();

        private static bool IsOverInventoryWindow(PointerEventData eventData)
        {
            raycastResults.Clear();
            EventSystem.current.RaycastAll(eventData, raycastResults);

            foreach (RaycastResult result in raycastResults)
            {
                if (result.gameObject.GetComponentInParent<UIInventoryWindow>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        // 지금 마우스가 올라와 있는지. 창이 닫혀 이 칸이 꺼지면 OnPointerExit이 불리지 않으므로 OnDisable에서 이 값을 보고 툴팁을 치운다
        private bool isHovered;

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;

            if (currentItem != null)
            {
                UIItemTooltip.Show(currentItem, "우클릭 - 해제");
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            UIItemTooltip.Hide();
        }

        // 툴팁을 띄운 채 창이 닫혀 이 칸이 꺼지면 툴팁만 공중에 남지 않게 함께 치운다 (다른 칸의 툴팁은 건드리지 않는다)
        private void OnDisable()
        {
            if (isHovered)
            {
                isHovered = false;
                UIItemTooltip.Hide();
            }
        }
    }
}
