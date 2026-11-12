using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// 스킬창에서 스킬 하나를 표시하는 슬롯.
    /// 처음 봤을 때는 아이콘/이름/배우기 버튼만 보이고, 상세 설명·소모MP·쿨다운은
    /// 아이템 툴팁과 같은 방식으로 아이콘에 마우스를 올렸을 때 UISkillTooltip에 띄운다.
    /// 아직 배우지 않은 스킬은 잠김 상태로 표시하고, 배운 스킬만 퀵슬롯으로 드래그할 수 있다
    /// </summary>
    public class UISkillSlotItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMPro.TextMeshProUGUI nameText;
        [Tooltip("스킬 포인트를 투자해 배우거나 레벨을 올리는 버튼 (최대 레벨이면 숨긴다)")]
        [SerializeField] private Button unlockButton;
        [Tooltip("버튼 라벨 (배우기 / 레벨업). 비워두면 버튼 자식에서 자동으로 찾는다")]
        [SerializeField] private TMPro.TextMeshProUGUI unlockButtonLabel;
        // 잠긴 스킬의 아이콘에 입히는 색 (회색으로 어둡게)
        [SerializeField] private Color lockedIconColor = new Color(0.35f, 0.35f, 0.35f, 1f);

        // 텍스트 성격별 색상. 한눈에 구분되도록 소모MP/쿨다운/스탯(위력)/레벨 표기에 각각 다른 색을 쓴다.
        // UISkillTooltip에서도 같은 색을 써야 해서 internal로 연다
        internal const string MPColorTag = "<color=#6EC6FF>";      // 소모 MP: 파랑 (MP 자원 색과 통일)
        internal const string CooldownColorTag = "<color=#FFB347>"; // 쿨다운: 주황
        internal const string PowerColorTag = "<color=#8FD14F>";    // 위력(스탯) 성장: 초록
        internal const string LevelColorTag = "<color=#C8B98A>";    // 레벨 표기: 기존 톤(황갈색) 유지
        internal const string DamageColorTag = "<color=#FF8A7A>";   // 데미지/효과 수치: 붉은 계열

        private RectTransform dragIcon;

        public SkillData CurrentSkill { get; private set; }
        public int CurrentLevel { get; private set; }
        public bool IsUnlocked => CurrentLevel > 0;
        [Tooltip("최대 레벨에 도달하면 더 올릴 수 없다")]
        public bool CanLevelUp => CurrentSkill != null && CurrentLevel < CurrentSkill.maxLevel;

        // 슬롯에 스킬 데이터와 현재 레벨을 표시하는 함수 (0레벨이면 아직 배우지 않은 것)
        public void SetSkill(SkillData skill, int level)
        {
            CurrentSkill = skill;
            CurrentLevel = skill != null ? Mathf.Clamp(level, 0, skill.maxLevel) : 0;
            bool isUnlocked = IsUnlocked;

            if (iconImage != null)
            {
                iconImage.sprite = skill != null ? skill.icon : null;
                iconImage.enabled = skill != null && skill.icon != null;
                // 잠긴 스킬은 아이콘을 어둡게 처리해 한눈에 구분되도록 한다
                iconImage.color = isUnlocked ? Color.white : lockedIconColor;
            }

            if (nameText != null)
            {
                // 배운 스킬은 이름 뒤에 현재/최대 레벨을 붙인다
                nameText.text = skill == null
                    ? "-"
                    : isUnlocked ? $"{skill.skillName}  Lv.{CurrentLevel}/{skill.maxLevel}" : skill.skillName;
            }

            // 아직 올릴 여지가 남은 스킬에만 버튼을 노출한다
            if (unlockButton != null)
            {
                unlockButton.gameObject.SetActive(skill != null && CanLevelUp);

                if (unlockButtonLabel == null)
                {
                    unlockButtonLabel = unlockButton.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
                }

                if (unlockButtonLabel != null)
                {
                    unlockButtonLabel.text = isUnlocked ? "레벨업" : "배우기";
                }
            }
        }

        // 툴팁 본문: 핵심 수치(데미지/효과) 한 줄 + 간단한 설명. 다음 레벨 변화량은 보여주지 않는다.
        // UISkillTooltip이 호출하므로 인스턴스 상태 대신 파라미터로 넘겨받는 static 함수다.
        // 아직 배우지 않은 스킬(0레벨)도 1레벨 기준 수치를 보여준다
        internal static string BuildTooltipDescription(SkillData skill, int level, bool isUnlocked)
        {
            string effect = skill.GetEffectText(Mathf.Max(1, level));

            return string.IsNullOrEmpty(effect)
                ? skill.description
                : $"{DamageColorTag}{effect}</color>\n{skill.description}";
        }

        // 지금 마우스가 올라와 있는지. 창이 닫혀 이 칸이 꺼지면 OnPointerExit이 불리지 않으므로 OnDisable에서 이 값을 보고 툴팁을 치운다
        private bool isHovered;

        // 마우스를 올리면 상세 정보 툴팁을 띄운다 (아이템 툴팁과 같은 방식)
        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;

            if (CurrentSkill != null)
            {
                UISkillTooltip.Show(CurrentSkill, CurrentLevel);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            UISkillTooltip.Hide();
        }

        // 툴팁을 띄운 채 스킬창이 닫혀 이 칸이 꺼지면 툴팁만 공중에 남지 않게 함께 치운다 (다른 칸의 툴팁은 건드리지 않는다)
        private void OnDisable()
        {
            if (isHovered)
            {
                isHovered = false;
                UISkillTooltip.Hide();
            }
        }

        // 스킬창이 배우기 버튼 클릭을 처리할 수 있도록 구독을 열어준다
        public void SubscribeOnUnlockClicked(UnityAction<UISkillSlotItem> listener)
        {
            if (unlockButton == null)
            {
                return;
            }

            unlockButton.onClick.AddListener(() => listener(this));
        }

        // 남은 스킬 포인트가 없으면 버튼을 눌러도 소용없으므로 비활성화한다
        public void SetUnlockButtonInteractable(bool interactable)
        {
            if (unlockButton != null)
            {
                unlockButton.interactable = interactable;
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // 아직 배우지 않은 스킬은 퀵슬롯으로 끌어다 놓을 수 없다
            if (CurrentSkill == null || CurrentSkill.icon == null || !IsUnlocked)
            {
                return;
            }

            Canvas rootCanvas = GetComponentInParent<Canvas>().rootCanvas;
            dragIcon = UIDragIcon.Create(CurrentSkill.icon, rootCanvas.transform, new Vector2(64f, 64f));
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
