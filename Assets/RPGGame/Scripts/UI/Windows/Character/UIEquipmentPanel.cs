using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 캐릭터 정보창의 장비 패널. 무기/갑옷/투구 3개 슬롯을 장착 상태에 맞춰 갱신한다
    /// </summary>
    public class UIEquipmentPanel : MonoBehaviour
    {
        [SerializeField] private UIEquipmentSlot[] slots;

        private PlayerEquipment equipment;
        private bool isSubscribed;

        private void OnEnable()
        {
            Refresh();
        }

        private void Start()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (equipment == null)
            {
                equipment = FindAnyObjectByType<PlayerEquipment>();
            }

            if (equipment == null)
            {
                return;
            }

            // 장착 구성이 바뀔 때만 갱신되도록 한 번만 구독한다
            if (!isSubscribed)
            {
                equipment.SubscribeOnEquipmentChanged(Refresh);
                isSubscribed = true;
            }

            foreach (UIEquipmentSlot slot in slots)
            {
                if (slot != null)
                {
                    slot.SetItem(equipment.GetEquipped(slot.EquipType));
                }
            }
        }
    }
}
