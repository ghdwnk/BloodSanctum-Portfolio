using UnityEngine;
using UnityEngine.InputSystem;

namespace RPGGame
{
    /// <summary>
    /// 아이템에 마우스를 올렸을 때 이름/등급/스탯/특수효과를 보여주는 툴팁.
    /// 화면에 하나만 존재하며 커서 위치를 따라다닌다
    /// </summary>
    public class UIItemTooltip : MonoBehaviour
    {
        private static UIItemTooltip instance;

        [SerializeField] private GameObject panel;
        [SerializeField] private RectTransform panelTransform;
        [SerializeField] private TMPro.TextMeshProUGUI nameText;
        [SerializeField] private TMPro.TextMeshProUGUI gradeText;
        [SerializeField] private TMPro.TextMeshProUGUI statText;
        [SerializeField] private TMPro.TextMeshProUGUI effectText;
        [SerializeField] private TMPro.TextMeshProUGUI hintText;
        // 커서에서 툴팁을 얼마나 띄울지
        [SerializeField] private Vector2 cursorOffset = new Vector2(18f, -18f);

        private RectTransform canvasTransform;
        private PlayerEquipment playerEquipment;

        // 장착 장비와 비교할 때 쓰는 색 (더 좋음 / 더 나쁨)과 안내 문구 색
        private const string UpColor = "#6FD16F";
        private const string DownColor = "#E85D5D";
        private static readonly Color HintColor = new Color(0.58f, 0.62f, 0.68f);

        // 툴팁에 보여줄 스탯 목록 (표시 순서). 값은 GetStatValues의 배열과 같은 순서다
        private static readonly string[] StatLabels =
        {
            "공격력", "방어력", "체력", "치명타율", "치명타 피해",
            "스킬 쿨타임 감소", "대쉬 쿨타임 감소", "이동속도", "공격속도"
        };
        private static readonly string[] StatSuffixes = { "", "", "", "%", "%", "%", "%", "%", "%" };

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                canvasTransform = GetComponentInParent<Canvas>().rootCanvas.transform as RectTransform;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            Hide();
        }

        private void Update()
        {
            if (!panel.activeSelf)
            {
                return;
            }

            FollowCursor();
        }

        // 툴팁이 화면 밖으로 나가지 않도록 커서 위치를 보정해 배치하는 함수
        private void FollowCursor()
        {
            // 이 프로젝트는 새 Input System 전용이라 레거시 Input.mousePosition은 예외를 던진다
            if (Mouse.current == null)
            {
                return;
            }

            Vector2 cursor = Mouse.current.position.ReadValue();
            Vector2 size = panelTransform.rect.size * canvasTransform.localScale.x;

            // 오른쪽/아래로 넘칠 것 같으면 반대쪽으로 붙인다
            float x = cursor.x + cursorOffset.x;
            float y = cursor.y + cursorOffset.y;
            if (x + size.x > Screen.width) x = cursor.x - cursorOffset.x - size.x;
            if (y - size.y < 0f) y = cursor.y - cursorOffset.y + size.y;

            panelTransform.position = new Vector3(x, y, 0f);
        }

        // 장비 개체용: 굴려진 스탯과 강화 단계가 반영된 실제 값을 보여준다
        public static void Show(EquipmentInstance equipmentInstance, string hint)
        {
            if (equipmentInstance == null || equipmentInstance.item == null)
            {
                return;
            }

            Show(equipmentInstance.item, equipmentInstance, hint);
        }

        // 원본 에셋용(상점 구매 목록 등): 개체가 없으므로 에셋에 적힌 기본 스탯을 보여준다
        public static void Show(Item item, string hint)
        {
            Show(item, null, hint);
        }

        private static void Show(Item item, EquipmentInstance equipmentInstance, string hint)
        {
            if (instance == null || item == null)
            {
                return;
            }

            instance.panel.SetActive(true);

            if (instance.nameText != null)
            {
                // 강화된 장비는 이름 뒤에 +N이 붙는다
                instance.nameText.text = equipmentInstance != null
                    ? EnhancementManager.GetDisplayName(equipmentInstance)
                    : item.itemName;
                instance.nameText.color = item.GradeColor;
            }

            EquipmentItem equipment = item as EquipmentItem;

            if (instance.gradeText != null)
            {
                instance.gradeText.text = equipment != null
                    ? $"{item.GradeName} · {equipment.EquipTypeName}"
                    : item.GradeName;
                instance.gradeText.color = item.GradeColor;
            }

            if (instance.statText != null)
            {
                // 장비는 스탯 보정을, 소모품·재료는 효과 한 줄을 보여준다.
                // 예전에는 장비가 아니면 빈칸이라 물약 툴팁에 이름과 등급만 떴다
                instance.statText.text = equipment != null ? BuildStatText(equipment, equipmentInstance) : BuildUsageText(item);
            }

            if (instance.effectText != null)
            {
                string effect = equipment != null ? equipment.BuildEffectText() : string.Empty;
                instance.effectText.text = effect;
                instance.effectText.gameObject.SetActive(!string.IsNullOrEmpty(effect));
            }

            if (instance.hintText != null)
            {
                instance.hintText.text = hint;
                // 장착/해제 안내는 눈에 띄지 않게 차분한 색으로 둔다 (스탯 비교의 초록/빨강과 겹치지 않도록)
                instance.hintText.color = HintColor;
                instance.hintText.gameObject.SetActive(!string.IsNullOrEmpty(hint));
            }

            instance.FollowCursor();
        }

        // 장비가 아닌 아이템의 효과 설명을 만드는 함수
        private static string BuildUsageText(Item item)
        {
            if (item is HealthItem healthItem)
            {
                return $"체력을 {healthItem.healthAmount:0} 회복한다.";
            }

            if (item is MaterialItem materialItem)
            {
                return materialItem.Description;
            }

            return string.Empty;
        }

        // 장비 하나의 표시용 스탯 값을 StatLabels 순서로 돌려준다.
        // 개체가 있으면 굴려진 스탯에 강화 배율을 곱한 실제 적용값을, 없으면 에셋의 기본 스탯을 쓴다
        private static float[] GetStatValues(EquipmentItem equipment, EquipmentInstance equipmentInstance)
        {
            float multiplier = equipmentInstance != null ? EnhancementManager.GetStatMultiplier(equipmentInstance) : 1f;
            float attack = equipmentInstance != null ? equipmentInstance.attack : equipment.attackBonus;
            float defense = equipmentInstance != null ? equipmentInstance.defense : equipment.defenseBonus;
            float maxHP = equipmentInstance != null ? equipmentInstance.maxHP : equipment.maxHPBonus;

            return new[]
            {
                attack * multiplier, defense * multiplier, maxHP * multiplier,
                equipment.critRateBonus, equipment.critDamageBonus,
                equipment.cooldownReductionBonus, equipment.dashCooldownReductionBonus,
                equipment.moveSpeedBonus, equipment.attackSpeedBonus
            };
        }

        // 같은 부위에 장착 중인 개체를 돌려준다. 보고 있는 장비가 바로 그 장착품이면(장비창에서 본 경우) null이라 비교하지 않는다
        private static EquipmentInstance GetEquippedToCompare(EquipmentItem equipment, EquipmentInstance viewing)
        {
            if (instance.playerEquipment == null)
            {
                instance.playerEquipment = FindAnyObjectByType<PlayerEquipment>();
            }

            EquipmentInstance equipped = instance.playerEquipment != null
                ? instance.playerEquipment.GetEquipped(equipment.equipType)
                : null;

            return equipped != null && equipped != viewing ? equipped : null;
        }

        // 스탯을 줄바꿈으로 이어붙이는 함수. 장착 중인 장비가 있으면 항목별로 비교해
        // 더 높으면 초록 글씨+▲, 더 낮으면 빨강 글씨+▼를 붙인다
        private static string BuildStatText(EquipmentItem equipment, EquipmentInstance equipmentInstance)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();

            float[] values = GetStatValues(equipment, equipmentInstance);

            EquipmentInstance equippedItem = GetEquippedToCompare(equipment, equipmentInstance);
            float[] equippedValues = equippedItem != null ? GetStatValues(equippedItem.item, equippedItem) : null;

            for (int ix = 0; ix < values.Length; ++ix)
            {
                // 화면에 보이는 정수 값끼리 비교한다 (소수점 차이로 같은 숫자에 화살표가 붙지 않게)
                float shown = Mathf.Round(values[ix]);
                float other = equippedValues != null ? Mathf.Round(equippedValues[ix]) : 0f;

                // 이 장비에 없는 스탯은 감춘다. 단, 장착 중인 장비에는 있는 스탯이면 "0 ▼"로 잃는 것을 보여준다
                if (shown == 0f && other == 0f)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                string line = shown == 0f
                    ? $"{StatLabels[ix]} 0{StatSuffixes[ix]}"
                    : $"{StatLabels[ix]} +{shown:0}{StatSuffixes[ix]}";

                if (equippedValues == null || shown == other)
                {
                    builder.Append(line);
                }
                else if (shown > other)
                {
                    builder.Append("<color=").Append(UpColor).Append('>').Append(line).Append(" ▲</color>");
                }
                else
                {
                    builder.Append("<color=").Append(DownColor).Append('>').Append(line).Append(" ▼</color>");
                }
            }

            return builder.ToString();
        }

        public static void Hide()
        {
            if (instance != null && instance.panel != null)
            {
                instance.panel.SetActive(false);
            }
        }
    }
}
