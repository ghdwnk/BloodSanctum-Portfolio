using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 장비 아이템의 원본 정보(무기·갑옷·투구 3부위).
    ///
    /// 이 에셋은 "같은 종류의 장비가 함께 쓰는 원본"이다. 플레이어가 장비를 하나 얻을 때마다
    /// EquipmentInstance(개체)가 새로 만들어져, 굴린 스탯과 강화 단계를 개체마다 따로 가진다.
    /// 장착하면 PlayerEquipment가 세 부위의 보정치를 모두 더해 PlayerStats에 넘긴다.
    ///  - 공격력·방어력·최대 체력: 개체가 굴린 값 x 강화 배율
    ///  - 나머지(치명타·쿨타임·속도 등): 이 에셋의 값 그대로
    /// </summary>
    [CreateAssetMenu(fileName = "New Equipment Item", menuName = "Inventory/Item/EquipmentItem")]
    public class EquipmentItem : Item
    {
        /// <summary>
        /// 장착 부위. 정수 값이 에셋에 저장돼 있으므로 순서를 바꾸지 않는다
        /// (Helmet은 예전 각반 자리를 이름만 바꾼 것이다).
        /// </summary>
        public enum EquipType { Weapon, Armor, Helmet }

        [Header("부위")]
        [Tooltip("장착 부위")]
        public EquipType equipType = EquipType.Weapon;

        [Header("무기 외형 (무기 부위만 사용)")]
        [Tooltip("장착하면 손에 든 무기 모델이 이 프리팹으로 바뀐다. 비워 두면 기본 검 모델을 쓴다")]
        public GameObject weaponModelPrefab;

        [Header("스탯 보정")]
        [Tooltip("공격력. 얻을 때 '이 값 ~ 이 값 x 스탯 랜덤 배율' 사이에서 굴린다")]
        public float attackBonus;
        [Tooltip("방어력. 공격력과 같은 방식으로 굴린다")]
        public float defenseBonus;
        [Tooltip("최대 체력. 공격력과 같은 방식으로 굴린다")]
        public float maxHPBonus;
        [Tooltip("치명타 확률 (%p)")]
        public float critRateBonus;
        [Tooltip("치명타 피해 배율 (%p)")]
        public float critDamageBonus;
        [Tooltip("스킬 쿨타임 감소 (%). 여러 장비의 값을 더한 뒤 PlayerStats에서 상한을 건다")]
        public float cooldownReductionBonus;
        [Tooltip("대쉬 쿨타임 감소 (%)")]
        public float dashCooldownReductionBonus;
        [Tooltip("이동 속도 증가 (%). 합산 후 PlayerStats에서 상한을 건다")]
        public float moveSpeedBonus;
        [Tooltip("공격 속도 증가 (%). 합산 후 PlayerStats에서 상한을 건다")]
        public float attackSpeedBonus;

        [Header("사망 시 부활")]
        [Tooltip("켜면 이 장비를 입고 죽었을 때 한 번 그 자리에서 되살아난다. 마을에서 부활하면 다시 쓸 수 있다")]
        public bool reviveOnDeath;
        [Tooltip("되살아날 때 채워 주는 체력 (최대 체력 대비 비율)")]
        [Range(0.01f, 1f)] public float reviveHPRatio = 0.3f;

        [Header("스탯 랜덤 범위")]
        [Tooltip("드랍으로 얻을 때 공격력·방어력·체력을 '원본 ~ 원본 x 이 배율' 사이에서 굴린다. 1이면 항상 원본 값")]
        [SerializeField, Min(1f)] private float statRollMaxRatio = 1.5f;

        [Header("툴팁")]
        [Tooltip("수치로 나타나지 않는 고유 효과 설명. 부활 효과 문구는 위 설정으로 자동으로 붙는다")]
        [TextArea(2, 4)] public string specialEffectText;

        /// <summary>부위의 한국어 이름.</summary>
        public string EquipTypeName
        {
            get
            {
                switch (equipType)
                {
                    case EquipType.Armor: return "갑옷";
                    case EquipType.Helmet: return "투구";
                    default: return "무기";
                }
            }
        }

        /// <summary>
        /// 툴팁에 보여 줄 고유 효과 문구. 부활 효과처럼 수치로 정해진 효과는 필드 값으로 문장을 만들어
        /// 문구와 실제 동작이 어긋나지 않게 한다.
        /// </summary>
        public string BuildEffectText()
        {
            string revive = reviveOnDeath
                ? $"사망 시 1회 최대 체력의 {reviveHPRatio * 100f:0}%로 부활"
                : string.Empty;

            if (string.IsNullOrEmpty(specialEffectText))
            {
                return revive;
            }

            return string.IsNullOrEmpty(revive) ? specialEffectText : $"{specialEffectText}\n{revive}";
        }

        /// <summary>
        /// 원본 스탯에서 한 번 굴린 값을 돌려준다. roll이 false면 원본 그대로.
        /// 정수로 반올림해서 툴팁(소수점 없이 표시)과 실제 적용값이 어긋나지 않게 한다.
        /// </summary>
        public float RollStat(float baseValue, bool roll)
        {
            if (!roll || baseValue <= 0f || statRollMaxRatio <= 1f)
            {
                return baseValue;
            }

            return Mathf.Round(Random.Range(baseValue, baseValue * statRollMaxRatio));
        }

        /// <summary>
        /// 장착/해제는 개체(EquipmentInstance) 단위라서 원본 에셋만으로는 처리할 수 없다.
        /// 인벤토리 창이 개체를 알고 있으므로 PlayerEquipment.ToggleEquip(개체)를 직접 부른다.
        /// </summary>
        public override void Use()
        {
        }
    }
}
