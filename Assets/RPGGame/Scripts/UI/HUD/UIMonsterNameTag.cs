using TMPro;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 몬스터 머리 위 HP바 상단에 "Lv.N 이름"을 표시하는 컴포넌트.
    /// 이름과 레벨의 출처는 EnemyData와 스테이트 매니저이고, 이 스크립트는 표시만 담당한다.
    /// 몬스터 종류가 늘어나도 프리팹에 이 컴포넌트만 붙이면 되도록 종류별 분기를 두지 않았다
    /// </summary>
    public class UIMonsterNameTag : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;

        // 마지막으로 표시한 문자열. 같은 내용이면 대입 자체를 건너뛴다.
        // TMP는 text에 값을 넣을 때마다 텍스트 메시를 다시 생성하므로,
        // 스폰 과정에서 Awake와 SetLevel로 두 번 호출돼도 재생성이 한 번만 일어나게 하기 위함
        private string lastText;

        // 레벨 표기 색. 이름(흰색)과 구분돼 위험도를 한눈에 읽게 한다
        private const string LevelColorHex = "#FFD24A";

        public void SetInfo(string enemyName, int level)
        {
            if (nameText == null)
            {
                return;
            }

            // 이름이 비어 있는 데이터라도 레벨은 보이도록 한다
            string text = string.IsNullOrEmpty(enemyName)
                ? $"<color={LevelColorHex}>Lv.{level}</color>"
                : $"<color={LevelColorHex}>Lv.{level}</color> {enemyName}";

            if (text == lastText)
            {
                return;
            }

            lastText = text;
            nameText.text = text;
        }
    }
}
