using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// 플레이어 레벨과 누적 경험치를 관리하고, 화면 아래 경험치 바·레벨 숫자를 갱신한다.
    ///
    /// 경험치는 "누적값"으로 들고 있고, PlayerData 레벨 표의 requiredExp(그 레벨에 필요한 누적 경험치)와 비교해 레벨을 정한다.
    /// 한 번에 여러 레벨이 오르면 오른 수만큼 포인트를 주고 OnLevelUP을 한 번 발행한다.
    /// OnLevelUP은 인스펙터로 PlayerStateManager.OnPlayerLevelUP에 연결돼 있고, 레벨업 배너·스탯창은 코드로 구독한다.
    /// </summary>
    public class PlayerLevelController : MonoBehaviour
    {
        [Tooltip("레벨이 오를 때 새 레벨과 함께 발행 (PlayerStateManager.OnPlayerLevelUP이 연결돼 있다)")]
        [SerializeField] private UnityEvent<int> OnLevelUP = new UnityEvent<int>();

        [Header("HUD")]
        [Tooltip("레벨 숫자 텍스트")]
        [SerializeField] private TMPro.TextMeshProUGUI levelGaugeText;
        [Tooltip("경험치 바 (Image의 fillAmount를 쓴다)")]
        [SerializeField] private Image expBar;

        private int level = 1;
        private float currentExp;
        private int maxLevel;
        private PlayerData data;
        private PlayerStats playerStats;

        public int Level => level;
        public float CurrentExp => currentExp;

        private bool IsMaxLevel => level >= maxLevel;

        /// <summary>현재 레벨에 들어온 뒤 쌓은 경험치 (사망 페널티 계산 기준).</summary>
        public float ExpInCurrentLevel => Mathf.Max(0f, currentExp - data.levels[level - 1].requiredExp);

        private void Awake()
        {
            data = DataManager.Instance.playerData;
            maxLevel = data.levels.Count;

            RefreshHud();
        }

        public void SubscribeOnLevelUP(UnityAction<int> listener)
        {
            OnLevelUP.AddListener(listener);
        }

        /// <summary>경험치를 얻는다 (몬스터 처치·퀘스트 보상). 레벨이 오르면 포인트를 주고 OnLevelUP을 발행한다.</summary>
        public void GainExp(float exp)
        {
            currentExp += exp;

            if (IsMaxLevel)
            {
                RefreshExpBar();
                return;
            }

            // 높은 레벨부터 거꾸로 확인해 도달한 최고 레벨을 찾는다 (한 번에 여러 레벨이 오르는 경우도 처리된다)
            int newLevel = level;
            for (int ix = maxLevel - 1; ix >= 0; --ix)
            {
                if (currentExp >= data.levels[ix].requiredExp)
                {
                    newLevel = ix + 1;
                    break;
                }
            }

            if (newLevel > level)
            {
                int gainedLevelCount = newLevel - level;
                level = newLevel;

                if (playerStats == null)
                {
                    playerStats = GetComponentInParent<PlayerStats>();
                }

                if (playerStats != null)
                {
                    playerStats.GrantLevelUpPoints(gainedLevelCount);
                }

                OnLevelUP.Invoke(level);
            }

            RefreshHud();
        }

        /// <summary>
        /// 사망 페널티로 경험치를 잃는다. 현재 레벨에 들어올 때 필요했던 누적 경험치 아래로는 내려가지 않는다 (레벨 강등 없음).
        /// 실제로 잃은 양을 돌려준다.
        /// </summary>
        public float LoseExp(float amount)
        {
            if (amount <= 0f)
            {
                return 0f;
            }

            float floorExp = data.levels[level - 1].requiredExp;
            float before = currentExp;
            currentExp = Mathf.Max(floorExp, currentExp - amount);

            RefreshExpBar();
            return before - currentExp;
        }

        /// <summary>세이브에서 레벨·경험치를 되살린다. GainExp와 달리 레벨업 알림을 보내지 않는다 (불러오기 전용).</summary>
        public void LoadState(int savedLevel, float savedExp)
        {
            level = Mathf.Clamp(savedLevel, 1, maxLevel);
            currentExp = savedExp;

            RefreshHud();
        }

        // ───────────────────────── HUD ─────────────────────────

        private void RefreshHud()
        {
            RefreshExpBar();

            if (levelGaugeText != null)
            {
                levelGaugeText.text = level.ToString();
            }
        }

        // 경험치 바는 "현재 레벨 시작 ~ 다음 레벨" 구간에서 얼마나 찼는지를 보여 준다. 최대 레벨이면 가득 채운다
        private void RefreshExpBar()
        {
            if (expBar == null)
            {
                return;
            }

            if (IsMaxLevel)
            {
                expBar.fillAmount = 1f;
                return;
            }

            float currentLevelExp = data.levels[level - 1].requiredExp;
            float nextLevelExp = data.levels[level].requiredExp;
            float range = nextLevelExp - currentLevelExp;

            expBar.fillAmount = range > 0f ? Mathf.Clamp01((currentExp - currentLevelExp) / range) : 1f;
        }
    }
}
