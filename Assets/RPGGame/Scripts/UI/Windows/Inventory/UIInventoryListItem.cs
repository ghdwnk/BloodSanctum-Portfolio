using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// 인벤토리 창의 칸 하나. 아이템 아이콘·개수(장비는 강화 단계)·등급 테두리를 보여 주고 마우스 동작을 처리한다.
    /// 장비는 우클릭으로 장착/해제하고, 물약 같은 소모품도 우클릭으로 사용한다. 마우스를 올리면 툴팁이 뜨고, 끌어서 퀵슬롯에 등록할 수 있다.
    /// </summary>
    public class UIInventoryListItem : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Item item;
        [Tooltip("장비 칸이면 이 칸이 가리키는 장비 개체 (소모품/재료 칸은 null)")]
        public EquipmentInstance equipment;
        public Image image;

        public TMPro.TextMeshProUGUI countText;
        [Tooltip("등급 색을 입히는 테두리 (없어도 동작하도록 null 허용)")]
        [SerializeField] private Image gradeFrame;

        private RectTransform dragIcon;

        public void SetItem(Item item)
        {
            this.item = item;
            equipment = null;

            // 등급 색 테두리 갱신
            if (gradeFrame != null)
            {
                gradeFrame.enabled = item != null;
                if (item != null)
                {
                    gradeFrame.color = item.GradeColor;
                }
            }
        }

        // 장비 개체 한 개를 이 칸에 표시한다. 개수 대신 강화 단계(+N)를 보여준다
        public void SetEquipment(EquipmentInstance instance)
        {
            SetItem(instance.item);
            equipment = instance;
            SetSprite(instance.item.sprite);
            countText.text = instance.enhanceLevel > 0 ? $"+{instance.enhanceLevel}" : string.Empty;
        }

        // 아이템 이미지를 설정하는 함수
        public void SetSprite(Sprite sprite)
        {
            image.sprite = sprite;
            image.enabled = sprite != null;
        }

        // 아이템 수를 설정하는 함수
        public void SetCount(int count)
        {
            countText.text = $"{count}";
        }

        // 빈 슬롯으로 비우는 함수 (고정 그리드에서 아이템이 없는 칸에 사용)
        public void SetEmpty()
        {
            SetItem(null);
            SetSprite(null);
            countText.text = string.Empty;
        }

        // 아이템 UI가 클릭됐을 때 실행될 함수.
        // 장비 장착/해제와 소모품 사용은 모두 우클릭으로만 한다 (좌클릭은 아무 일도 하지 않는다)
        public void OnPointerClick(PointerEventData eventData)
        {
            if (item == null)
            {
                return;
            }

            if (eventData.button != PointerEventData.InputButton.Right)
            {
                return;
            }

            if (equipment != null)
            {

                // 장착/해제 때 창 여닫기 소리를 낸다
                UISound.Play();

                // 같은 종류가 여러 개여도 이 칸의 개체 하나만 장착/해제한다
                FindAnyObjectByType<PlayerEquipment>()?.ToggleEquip(equipment);

                // 장착 상태가 바뀌면 툴팁의 안내 문구도 갱신한다.
                // 장착한 개체는 목록에서 사라져 이 칸이 다른 개체로 바뀔 수 있으므로 칸이 비었으면 툴팁을 닫는다
                ShowTooltip();
                return;
            }

            item.Use();
        }

        private void ShowTooltip()
        {
            if (equipment != null)
            {
                UIItemTooltip.Show(equipment, GetTooltipHint());
            }
            else if (item != null)
            {
                UIItemTooltip.Show(item, GetTooltipHint());
            }
            else
            {
                UIItemTooltip.Hide();
            }
        }

        // 지금 마우스가 올라와 있는지. 창이 닫혀 이 칸이 꺼지면 OnPointerExit이 불리지 않으므로 OnDisable에서 이 값을 보고 툴팁을 치운다
        private bool isHovered;

        // 마우스를 올리면 아이템 정보 툴팁을 띄운다
        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            ShowTooltip();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            UIItemTooltip.Hide();
        }

        // 툴팁을 띄운 채 창이 닫히거나 목록이 다시 만들어져 이 칸이 꺼지면, 툴팁만 공중에 남지 않게 함께 치운다.
        // 마우스가 올라와 있던 칸일 때만 치워서, 다른 칸이 띄운 툴팁을 잘못 끄지 않는다
        private void OnDisable()
        {
            if (isHovered)
            {
                isHovered = false;
                UIItemTooltip.Hide();
            }
        }

        // 아이템 종류와 장착 여부에 따라 툴팁 하단 안내 문구를 만드는 함수
        private string GetTooltipHint()
        {
            if (equipment == null)
            {
                // 소비용품(물약)만 우클릭으로 쓴다. 기타 아이템은 쓸 수 없으므로 안내를 비운다
                return item != null && item.category == Item.Category.Consumable ? "우클릭 - 사용" : string.Empty;
            }

            PlayerEquipment playerEquipment = FindAnyObjectByType<PlayerEquipment>();
            bool equipped = playerEquipment != null && playerEquipment.IsEquipped(equipment);

            return equipped ? "우클릭 - 해제" : "우클릭 - 장착";
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (item == null || item.sprite == null)
            {
                return;
            }

            Canvas rootCanvas = GetComponentInParent<Canvas>().rootCanvas;
            dragIcon = UIDragIcon.Create(item.sprite, rootCanvas.transform, new Vector2(64f, 64f));
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

            // 다른 인벤토리 칸 위에 놓았으면 그 칸으로 옮기거나 맞바꾸고 끝낸다 (칸은 놓은 자리에 고정된다)
            if (UIInventoryWindow.TryDropOnSlot(this, eventData.position, eventData.pressEventCamera))
            {
                return;
            }

            // UI 밖(게임 화면 바닥)에 놓았을 때만 "바닥에 떨어뜨림"으로 본다. 블러드스톤을 붉은 수정 앞에서 떨어뜨리면 봉인이 시작된다
            bool droppedOnGround = EventSystem.current != null && !EventSystem.current.IsPointerOverGameObject();
            if (droppedOnGround && equipment == null && item != null)
            {
                SealingThrone.OnItemDroppedOnGround(item);
            }
        }
    }
}
