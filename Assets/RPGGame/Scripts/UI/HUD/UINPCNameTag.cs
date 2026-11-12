using TMPro;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// NPC 머리 위에 "[담당] 이름"을 표시하는 컴포넌트.
    /// 표시만 담당하고 문자열의 출처는 NPCData이므로, NPC가 늘어나도
    /// 프리팹에 이 컴포넌트만 붙이고 데이터에 담당을 적으면 된다 (UIMonsterNameTag와 같은 구조)
    /// </summary>
    public class UINPCNameTag : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [Tooltip("담당 표기에 입힐 색. 이름과 구분되도록 다른 색을 준다")]
        [SerializeField] private string roleColorHex = "#FFC24D";

        // 마지막으로 표시한 문자열. TMP는 text에 대입할 때마다 텍스트 메시를 다시 만들기 때문에
        // 같은 내용이면 대입 자체를 건너뛴다 (UIMonsterNameTag와 같은 이유)
        private string lastText;

        public void SetInfo(string npcName, string role)
        {
            if (nameText == null)
            {
                return;
            }

            // 담당은 윗줄에 작게, 이름은 아랫줄에 크게 두 줄로 나눠 표시한다.
            // 한 줄로 붙이면 이름이 길어질수록 가로로 넓어져 화면을 가린다
            string text = string.IsNullOrEmpty(role)
                ? npcName
                : $"<size=75%><color={roleColorHex}>[{role}]</color></size>\n{npcName}";

            if (text == lastText)
            {
                return;
            }

            lastText = text;
            nameText.text = text;
        }
    }
}
