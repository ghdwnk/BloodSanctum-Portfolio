using UnityEngine;
using UnityEngine.InputSystem;

namespace RPGGame
{
    /// <summary>
    /// 스킬 아이콘에 마우스를 올렸을 때 상세 설명/소모MP/쿨다운을 보여주는 툴팁.
    /// UIItemTooltip과 완전히 같은 방식(화면에 하나만 존재, 커서를 따라다님)으로 만들었다
    /// </summary>
    public class UISkillTooltip : MonoBehaviour
    {
        private static UISkillTooltip instance;

        [SerializeField] private GameObject panel;
        [SerializeField] private RectTransform panelTransform;
        [SerializeField] private TMPro.TextMeshProUGUI nameText;
        [SerializeField] private TMPro.TextMeshProUGUI costText;
        [SerializeField] private TMPro.TextMeshProUGUI descriptionText;
        // 커서에서 툴팁을 얼마나 띄울지
        [SerializeField] private Vector2 cursorOffset = new Vector2(18f, -18f);

        private RectTransform canvasTransform;

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

        // 툴팁이 화면 밖으로 나가지 않도록 커서 위치를 보정해 배치하는 함수 (UIItemTooltip과 동일)
        private void FollowCursor()
        {
            if (Mouse.current == null)
            {
                return;
            }

            Vector2 cursor = Mouse.current.position.ReadValue();
            Vector2 size = panelTransform.rect.size * canvasTransform.localScale.x;

            float x = cursor.x + cursorOffset.x;
            float y = cursor.y + cursorOffset.y;
            if (x + size.x > Screen.width) x = cursor.x - cursorOffset.x - size.x;
            if (y - size.y < 0f) y = cursor.y - cursorOffset.y + size.y;

            panelTransform.position = new Vector3(x, y, 0f);
        }

        public static void Show(SkillData skill, int currentLevel)
        {
            if (instance == null || skill == null)
            {
                return;
            }

            instance.panel.SetActive(true);

            bool isUnlocked = currentLevel > 0;
            int level = Mathf.Clamp(currentLevel, 0, skill.maxLevel);

            if (instance.nameText != null)
            {
                instance.nameText.text = isUnlocked
                    ? $"{skill.skillName}  Lv.{level}/{skill.maxLevel}"
                    : skill.skillName;
            }

            if (instance.costText != null)
            {
                // 배우지 않은 스킬도 1레벨 기준 소모 MP와 쿨타임을 보여준다 (미습득 표시만 덧붙인다)
                int shownLevel = Mathf.Max(1, level);
                // 장비의 쿨타임 감소가 반영된 값을 보여준다
                PlayerStats stats = FindAnyObjectByType<PlayerStats>();
                float cooldown = skill.GetCooldown(shownLevel) * (stats != null ? stats.CooldownMultiplier : 1f);
                string cost = $"{UISkillSlotItem.MPColorTag}MP {skill.GetMPCost(shownLevel):0}</color> · {UISkillSlotItem.CooldownColorTag}쿨타임 {Mathf.CeilToInt(cooldown)}초</color>";
                instance.costText.text = isUnlocked ? cost : $"{cost} · <color=#8A8A8A>미습득</color>";
            }

            if (instance.descriptionText != null)
            {
                instance.descriptionText.text = UISkillSlotItem.BuildTooltipDescription(skill, level, isUnlocked);
            }

            instance.FollowCursor();
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
