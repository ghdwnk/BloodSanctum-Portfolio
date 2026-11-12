using UnityEngine;
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// 플레이어 MP바. 채워진 비율과 "현재/최대" 숫자를 갱신한다.
    /// MPController.OnMPChanged에 OnMPChanged가 인스펙터로 연결돼 있다 (이름을 바꾸지 않는다).
    /// </summary>
    public class MPBar : MonoBehaviour
    {
        [SerializeField] private Image mpBar;
        [Tooltip("\"80/100\" 형태로 숫자를 보여주기 위한 텍스트. MP는 자연 회복으로 계속 소수점이 바뀌므로 표시할 때는 정수로 반올림한다")]
        [SerializeField] private TMPro.TextMeshProUGUI valueText;

        public void OnMPChanged(float currentMP, float maxMP)
        {
            if (mpBar != null)
            {
                mpBar.fillAmount = currentMP / maxMP;
            }

            if (valueText != null)
            {
                valueText.text = $"{Mathf.CeilToInt(currentMP)}/{Mathf.CeilToInt(maxMP)}";
            }
        }
    }
}
