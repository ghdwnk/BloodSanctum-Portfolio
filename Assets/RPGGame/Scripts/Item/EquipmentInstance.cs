using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 인벤토리에 들어 있는 장비 "한 개".
    ///
    /// EquipmentItem(에셋)은 같은 종류 장비가 함께 쓰는 원본 정보이고, 얻을 때마다 굴린 스탯과 강화 단계는
    /// 개체마다 달라서 이 클래스가 따로 들고 있다. 그래서 같은 "강철 대검"을 두 개 가져도 스탯·강화가 각각 다르다.
    /// InventoryManager가 목록으로 보관하고, PlayerEquipment가 부위별로 장착 중인 개체를 가리킨다.
    /// 세이브할 때는 GameSaveLoader가 EquipmentSaveEntry(id·에셋 이름·스탯·강화 단계)로 바꿔 저장한다.
    /// </summary>
    [System.Serializable]
    public class EquipmentInstance
    {
        /// <summary>개체를 구분하는 고유 ID. 저장한 장착 상태를 불러올 때 어느 개체였는지 찾는 데 쓴다.</summary>
        public string id;
        /// <summary>원본 장비 에셋.</summary>
        public EquipmentItem item;
        // 굴려서 정해진 기본 스탯 (강화 배율을 곱하기 전 값)
        public float attack;
        public float defense;
        public float maxHP;
        /// <summary>강화 단계 (+N).</summary>
        public int enhanceLevel;

        /// <summary>
        /// 새 개체를 만든다.
        /// roll이 true면 원본 스탯 ~ 원본 x statRollMaxRatio 사이에서 스탯을 굴리고(몬스터 드랍·퀘스트 보상),
        /// false면 원본 스탯 그대로 쓴다(상점 구매·기본 지급 장비).
        /// </summary>
        public static EquipmentInstance Create(EquipmentItem item, bool roll)
        {
            return new EquipmentInstance
            {
                id = System.Guid.NewGuid().ToString("N"),
                item = item,
                attack = item.RollStat(item.attackBonus, roll),
                defense = item.RollStat(item.defenseBonus, roll),
                maxHP = item.RollStat(item.maxHPBonus, roll),
                enhanceLevel = 0
            };
        }

        /// <summary>원본 에셋의 아이콘 (UI 편의용).</summary>
        public Sprite sprite => item != null ? item.sprite : null;
    }
}
