using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 그 자체로는 쓰지 않고 보관만 하는 재료 아이템.
    ///
    /// 에셋: Material_EnhanceStone(강화석 — 대장장이 강화 재료), Material_BloodStone(블러드스톤 조각 — 퀘스트 수집품).
    /// 인벤토리에서 사용을 눌러도 개수가 줄지 않고 "사용할 수 없는 아이템입니다" 안내만 띄운다.
    /// </summary>
    [CreateAssetMenu(fileName = "New Material Item", menuName = "Inventory/Item/MaterialItem")]
    public class MaterialItem : Item
    {
        [Header("설명")]
        [Tooltip("툴팁에 보이는 쓰임새 설명. 비워 두면 '장비 강화에 사용하는 재료.'가 나온다")]
        [SerializeField] private string description = string.Empty;

        /// <summary>툴팁에 보여 줄 쓰임새 설명.</summary>
        public string Description => string.IsNullOrEmpty(description) ? "장비 강화에 사용하는 재료." : description;

        public override void Use()
        {
            UISystemMessage.ShowToastWarning("사용할 수 없는 아이템입니다");
        }
    }
}
