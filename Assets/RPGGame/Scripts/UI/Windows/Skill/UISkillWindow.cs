using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// K키로 여닫는 스킬창. 보유 스킬 4개의 아이콘/정보와 해금 상태를 보여주고,
    /// 남은 스킬 포인트를 표시해 여기서 스킬을 배울 수 있게 한다
    /// </summary>
    public class UISkillWindow : MonoBehaviour
    {
        private static UISkillWindow instance;

        [SerializeField] private GameObject window;
        [SerializeField] private RectTransform windowTransform;
        [SerializeField] private Vector2 initialPosition = new Vector2(-100f, -200f);
        [SerializeField] private UISkillSlotItem[] slots;
        [Tooltip("남은 스킬 포인트 표시 (스탯 포인트는 캐릭터 정보창에서 관리한다)")]
        [SerializeField] private TMPro.TextMeshProUGUI skillPointText;

        private PlayerSkillController skillController;
        private PlayerStats playerStats;
        private bool isSubscribed;

        public static bool IsOn => instance != null && instance.window.activeSelf;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // 배우기 버튼은 한 번만 연결해두고, 이후에는 표시 상태만 갱신한다
            foreach (UISkillSlotItem slot in slots)
            {
                slot.SubscribeOnUnlockClicked(OnUnlockClicked);
            }
        }

        private void OnEnable()
        {
            CacheReferences();
            RefreshSlots();
        }

        private void CacheReferences()
        {
            if (skillController == null)
            {
                skillController = FindAnyObjectByType<PlayerSkillController>();
            }

            if (playerStats == null)
            {
                playerStats = FindAnyObjectByType<PlayerStats>();
            }

            // 레벨업으로 포인트가 늘거나 스킬을 배울 때 창이 열려 있어도 즉시 반영되도록 한 번만 구독한다
            if (!isSubscribed && skillController != null && playerStats != null)
            {
                skillController.SubscribeOnSkillUnlockChanged(RefreshSlots);
                playerStats.SubscribeOnStatsChanged(RefreshSlots);
                isSubscribed = true;
            }
        }

        // 배우기 버튼을 눌렀을 때 실행되는 함수
        private void OnUnlockClicked(UISkillSlotItem slot)
        {
            if (skillController == null || slot.CurrentSkill == null)
            {
                return;
            }

            skillController.TryLevelUpSkill(slot.CurrentSkill);
        }

        private void RefreshSlots()
        {
            if (skillController == null)
            {
                return;
            }

            int remainingSkillPoints = playerStats != null ? playerStats.AvailableSkillPoints : 0;

            for (int i = 0; i < slots.Length; ++i)
            {
                SkillData skill = skillController.GetSkill(i);
                slots[i].SetSkill(skill, skillController.GetSkillLevel(skill));
                // 포인트가 없거나 이미 최대 레벨이면 눌러도 소용없다
                slots[i].SetUnlockButtonInteractable(remainingSkillPoints > 0 && slots[i].CanLevelUp);
            }

            if (skillPointText != null)
            {
                skillPointText.text = $"스킬 포인트  {remainingSkillPoints}";
            }
        }

        public static void Show()
        {
            UISound.SetActive(instance.window, true);

            if (instance.windowTransform != null)
            {
                instance.windowTransform.anchoredPosition = instance.initialPosition;
            }

            instance.CacheReferences();
            instance.RefreshSlots();
        }

        public static void Close()
        {
            UISound.SetActive(instance.window, false);
        }
    }
}
