using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// 화면 상단의 보스 전용 체력바.
    /// 교전 중인 보스가 있을 때만 나타나고, 페이즈가 바뀌면 단계도 함께 표시한다.
    /// </summary>
    public class UIBossHealthBar : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Image fillImage;
        [Tooltip("체력이 줄어들 때 천천히 따라오는 뒤쪽 바 (피해량이 눈에 보이게)")]
        [SerializeField] private Image delayedFillImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI phaseText;

        [Header("설정")]
        [Tooltip("이 거리 안에 보스가 들어오면 체력바를 띄운다")]
        [SerializeField] private float showDistance = 30f;
        [Tooltip("보스를 다시 찾는 주기. 매 프레임 씬을 검색하지 않기 위함")]
        [SerializeField] private float searchInterval = 0.5f;
        [SerializeField] private float delayedFillSpeed = 0.35f;

        [Header("글자 색")]
        [SerializeField] private Color titleColor = new Color(1f, 0.42f, 0.42f);
        [SerializeField] private Color nameColor = new Color(1f, 0.93f, 0.72f);
        [SerializeField] private Color phaseColor = new Color(0.45f, 0.85f, 1f);

        private BossStateManager currentBoss;
        private HPController bossHP;
        private float searchTimer;

        // 이름·페이즈 글자를 마지막으로 만든 대상. 보스나 페이즈가 바뀔 때만 글자를 다시 만든다
        // (정규식 처리와 문자열 조립을 매 프레임 하지 않기 위해서다)
        private BossStateManager textBoss;
        private int textPhaseIndex = -1;

        private void Update()
        {
            searchTimer -= Time.deltaTime;

            if (searchTimer <= 0f)
            {
                searchTimer = searchInterval;
                currentBoss = FindEngagedBoss();
                bossHP = currentBoss != null ? currentBoss.GetComponentInChildren<HPController>() : null;
            }

            bool show = currentBoss != null && !currentBoss.IsDead && bossHP != null;

            if (panel != null && panel.activeSelf != show)
            {
                panel.SetActive(show);

                // 새로 나타날 때는 뒤쪽 바를 현재 체력에 맞춰 초기화한다
                if (show && delayedFillImage != null)
                {
                    delayedFillImage.fillAmount = bossHP.HPRatio;
                }
            }

            if (!show)
            {
                return;
            }

            if (textBoss != currentBoss || textPhaseIndex != currentBoss.CurrentPhaseIndex)
            {
                textBoss = currentBoss;
                textPhaseIndex = currentBoss.CurrentPhaseIndex;
                UpdateTexts();
            }

            UpdateFill();
        }

        // 플레이어와 교전 거리 안에 있는 살아있는 보스를 찾는다
        private BossStateManager FindEngagedBoss()
        {
            BossStateManager nearest = null;
            float nearestDistance = showDistance;

            foreach (BossStateManager boss in FindObjectsByType<BossStateManager>())
            {
                if (boss.IsDead)
                {
                    continue;
                }

                float distance = boss.DistanceToPlayer;

                if (distance <= nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = boss;
                }
            }

            return nearest;
        }

        private void UpdateTexts()
        {
            if (nameText != null && currentBoss.Data != null)
            {
                BossData data = currentBoss.Data;
                string namePart = $"<color=#{ColorUtility.ToHtmlStringRGB(nameColor)}>{data.enemyName}</color>";
                nameText.text = string.IsNullOrEmpty(data.bossTitle)
                    ? namePart
                    : $"<color=#{ColorUtility.ToHtmlStringRGB(titleColor)}>[{data.bossTitle}]</color> {namePart}";
            }

            if (phaseText != null)
            {
                BossData.Phase phase = currentBoss.CurrentPhase;
                // "페이즈 1 (반각성)"처럼 괄호로 붙은 부제는 표시하지 않는다
                phaseText.color = phaseColor;
                phaseText.text = phase != null
                    ? System.Text.RegularExpressions.Regex.Replace(phase.phaseName, @"\s*\(.*?\)", string.Empty).Trim()
                    : string.Empty;
            }
        }

        private void UpdateFill()
        {
            float ratio = bossHP.HPRatio;

            if (fillImage != null)
            {
                fillImage.fillAmount = ratio;
            }

            // 뒤쪽 바는 조금 늦게 따라와 방금 들어간 피해량이 보이게 한다
            if (delayedFillImage != null && delayedFillImage.fillAmount > ratio)
            {
                delayedFillImage.fillAmount =
                    Mathf.MoveTowards(delayedFillImage.fillAmount, ratio, delayedFillSpeed * Time.deltaTime);
            }
            else if (delayedFillImage != null)
            {
                delayedFillImage.fillAmount = ratio;
            }
        }
    }
}
