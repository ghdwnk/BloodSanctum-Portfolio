using UnityEngine;
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// 캐릭터 정보창의 스탯 패널을 갱신하는 스크립트.
    /// 기획서 20-2절의 최종 스탯(BaseStat + LevelStat + EquipmentStat + BuffStat)을 표시하고,
    /// 남은 스탯 포인트를 각 스탯에 분배할 수 있게 한다
    /// </summary>
    public class UICharacterStatsPanel : MonoBehaviour
    {
        [Header("스탯 텍스트")]
        [SerializeField] private TMPro.TextMeshProUGUI levelText;
        [SerializeField] private TMPro.TextMeshProUGUI attackText;
        [SerializeField] private TMPro.TextMeshProUGUI defenseText;
        [SerializeField] private TMPro.TextMeshProUGUI hpText;
        [SerializeField] private TMPro.TextMeshProUGUI critRateText;
        [SerializeField] private TMPro.TextMeshProUGUI critDamageText;

        [Header("스탯 포인트 분배")]
        [SerializeField] private TMPro.TextMeshProUGUI statPointText;
        [SerializeField] private Button attackUpButton;
        [SerializeField] private Button defenseUpButton;
        [SerializeField] private Button maxHPUpButton;
        [SerializeField] private Button critRateUpButton;
        [SerializeField] private Button resetButton;

        // 각 행에서 수치가 시작되는 가로 위치 (라벨 길이와 무관하게 열을 맞추기 위해 사용)
        private const string ValueTag = "<pos=52%>";

        private PlayerStats playerStats;
        private bool isSubscribed;

        private void OnEnable()
        {
            Refresh();
        }

        private void Start()
        {
            PlayerLevelController levelController = FindAnyObjectByType<PlayerLevelController>();
            if (levelController != null)
            {
                levelController.SubscribeOnLevelUP(OnPlayerLevelUP);
            }

            if (attackUpButton != null)
            {
                attackUpButton.onClick.AddListener(() => Allocate(PlayerStats.StatType.Attack));
            }

            if (defenseUpButton != null)
            {
                defenseUpButton.onClick.AddListener(() => Allocate(PlayerStats.StatType.Defense));
            }

            if (maxHPUpButton != null)
            {
                maxHPUpButton.onClick.AddListener(() => Allocate(PlayerStats.StatType.MaxHP));
            }

            if (critRateUpButton != null)
            {
                critRateUpButton.onClick.AddListener(() => Allocate(PlayerStats.StatType.CritRate));
            }

            if (resetButton != null)
            {
                resetButton.onClick.AddListener(OnResetButtonClicked);
            }

            Refresh();
        }

        private void OnPlayerLevelUP(int newLevel)
        {
            Refresh();
        }

        // 스탯 포인트를 1개 투자하는 함수
        private void Allocate(PlayerStats.StatType statType)
        {
            if (playerStats == null)
            {
                return;
            }

            if (!playerStats.TryAllocateStatPoint(statType))
            {
                UISystemMessage.Show("남은 스탯 포인트가 없습니다.");
                return;
            }

            Refresh();
        }

        private void OnResetButtonClicked()
        {
            if (playerStats == null)
            {
                return;
            }

            playerStats.ResetStatPoints();
            Refresh();
        }

        // 최종 스탯을 기준으로 텍스트와 버튼 상태를 갱신하는 함수
        private void Refresh()
        {
            if (playerStats == null)
            {
                playerStats = FindAnyObjectByType<PlayerStats>();
            }

            if (playerStats == null)
            {
                return;
            }

            // 스탯이 바뀔 때마다(포인트 분배, 장비, 버프) 자동으로 갱신되도록 한 번만 구독한다
            if (!isSubscribed)
            {
                playerStats.SubscribeOnStatsChanged(Refresh);
                isSubscribed = true;
            }

            PlayerStateManager stateManager = FindAnyObjectByType<PlayerStateManager>();
            int level = stateManager != null && stateManager.CurrentLevelData != null
                ? stateManager.CurrentLevelData.level
                : 1;

            // 전투력: 최종 스탯을 합산한 대략적인 지표
            float combatPower = level + playerStats.Attack + playerStats.Defense + playerStats.MaxHP
                                + playerStats.CritRate;

            // 라벨과 수치가 겹치지 않도록 TMP의 <pos> 태그로 값 시작 위치를 고정한다
            if (levelText != null) levelText.text = $"레벨{ValueTag}{level}";
            if (attackText != null) attackText.text = $"공격력{ValueTag}{playerStats.Attack:0}";
            if (defenseText != null) defenseText.text = $"방어력{ValueTag}{playerStats.Defense:0}";
            if (hpText != null) hpText.text = $"체력{ValueTag}{playerStats.MaxHP:0}";
            if (critRateText != null) critRateText.text = $"치명타 확률{ValueTag}{playerStats.CritRate:0}%";
            if (critDamageText != null) critDamageText.text = $"치명타 피해{ValueTag}{playerStats.CritDamage:0}%";

            int remainingStatPoints = playerStats.AvailableStatPoints;

            // 스킬 포인트는 스킬창(K)에서 표시/사용하므로 여기서는 스탯 포인트만 다룬다
            if (statPointText != null) statPointText.text = $"스탯 포인트{ValueTag}{remainingStatPoints}";

            // 남은 포인트가 없으면 분배 버튼을 비활성화한다
            bool canAllocate = remainingStatPoints > 0;
            if (attackUpButton != null) attackUpButton.interactable = canAllocate;
            if (defenseUpButton != null) defenseUpButton.interactable = canAllocate;
            if (maxHPUpButton != null) maxHPUpButton.interactable = canAllocate;
            if (critRateUpButton != null) critRateUpButton.interactable = canAllocate;
        }
    }
}
