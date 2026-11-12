using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// MP 구슬 위에 떠 있는 대쉬 쿨타임 표시.
    /// 퀵슬롯의 쿨타임 표시(UIQuickSlotItem)와 같은 방식으로, 어두운 방사형 오버레이가 시계 방향으로
    /// 걷히고 남은 시간을 숫자로 보여 준다. 대쉬가 준비되면 키 안내(SPACE)를 보이고 한 번 반짝인다
    /// </summary>
    public class UIDashCooldown : MonoBehaviour
    {
        [SerializeField] private Image cooldownOverlay;
        [SerializeField] private TextMeshProUGUI cooldownText;
        [SerializeField] private Image DashImage;
        [Tooltip("준비 완료 때 한 번 커졌다 돌아오는 연출 대상")]
        [SerializeField] private RectTransform pulseTarget;
        [SerializeField] private float pulseScale = 1.25f;
        [SerializeField] private float pulseDuration = 0.25f;

        private PlayerDashController dashController;
        private bool wasOnCooldown;
        private float pulseTimer = -1f;
        // 마지막으로 글자로 보여 준 남은 초. 바뀔 때만 글자를 다시 만든다 (-1은 아직 아무것도 안 보여 줌)
        private int shownSeconds = -1;

        private void Update()
        {
            // 플레이어는 씬 시작 후에 확정되므로 처음 한 번만 찾고, 못 찾으면 다음 프레임에 다시 시도한다
            if (dashController == null)
            {
                dashController = FindAnyObjectByType<PlayerDashController>();

                if (dashController == null)
                {
                    return;
                }
            }

            float remaining = dashController.CooldownRemaining;
            float total = dashController.Cooldown;
            bool onCooldown = remaining > 0f;

            if (cooldownOverlay != null)
            {
                cooldownOverlay.fillAmount = total > 0f ? Mathf.Clamp01(remaining / total) : 0f;
            }

            if (cooldownText != null)
            {
                // 쿨타임은 소수점 없이 올림한 정수 초로만 보여 준다 (0.3초 남아도 "1"). 초가 바뀔 때만 글자를 갱신한다
                int seconds = onCooldown ? Mathf.CeilToInt(remaining) : 0;
                if (seconds != shownSeconds)
                {
                    shownSeconds = seconds;
                    cooldownText.text = seconds > 0 ? seconds.ToString() : string.Empty;
                }
            }

            if (DashImage != null)
            {
                DashImage.gameObject.SetActive(!onCooldown);
            }

            // 쿨타임이 끝난 프레임에 한 번 반짝인다
            if (wasOnCooldown && !onCooldown)
            {
                pulseTimer = 0f;
            }

            wasOnCooldown = onCooldown;
            UpdatePulse();
        }

        private void UpdatePulse()
        {
            if (pulseTarget == null || pulseTimer < 0f)
            {
                return;
            }

            pulseTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(pulseTimer / pulseDuration);
            // 0 -> 1 -> 0 으로 오르내리는 값 (사인 반주기)
            float scale = Mathf.Lerp(1f, pulseScale, Mathf.Sin(t * Mathf.PI));
            pulseTarget.localScale = new Vector3(scale, scale, 1f);

            if (t >= 1f)
            {
                pulseTimer = -1f;
                pulseTarget.localScale = Vector3.one;
            }
        }
    }
}
