using System.Text;
using TMPro;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 스킬로 올라간 일시 버프를 화면 구석에 글자로 보여준다. 예) "공격력 +120  (6.4초)".
    /// 버프가 없으면 글자를 비워 아무것도 그리지 않는다.
    /// 버프 종류가 늘어도 이 컴포넌트는 labels 배열만 채우면 되도록 StatType 순서로 순회한다
    /// </summary>
    public class UIBuffStatus : MonoBehaviour
    {
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private TextMeshProUGUI text;
        [Tooltip("StatType 순서(Attack, Defense, MaxHP, CritRate)와 같은 표시 이름")]
        [SerializeField] private string[] labels = { "공격력", "방어력", "최대 체력", "치명타율" };
        [SerializeField] private Color buffColor = new Color(1f, 0.86f, 0.42f);

        private readonly StringBuilder builder = new StringBuilder(64);
        private string lastText = string.Empty;

        private void Awake()
        {
            if (text != null)
            {
                text.color = buffColor;
                text.text = string.Empty;
            }
        }

        private void Update()
        {
            if (playerStats == null)
            {
                playerStats = FindAnyObjectByType<PlayerStats>();
                if (playerStats == null)
                {
                    return;
                }
            }

            builder.Clear();
            for (int i = 0; i < (int)PlayerStats.StatType.Length; i++)
            {
                if (!playerStats.TryGetActiveBuff((PlayerStats.StatType)i, out float amount, out float remaining))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                string label = i < labels.Length ? labels[i] : ((PlayerStats.StatType)i).ToString();
                builder.Append(label).Append(" +").Append(Mathf.RoundToInt(amount))
                       .Append("  (").Append(Mathf.CeilToInt(remaining)).Append("초)");
            }

            // 문자열이 같으면 TMP를 다시 만들지 않는다 (남은 시간은 0.1초 단위로만 바뀐다)
            string result = builder.ToString();
            if (result != lastText && text != null)
            {
                lastText = result;
                text.text = result;
            }
        }
    }
}
