using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 로딩화면에 표시할 팁/세계관 텍스트 목록.
    /// 인스펙터에서 문구를 자유롭게 추가/수정할 수 있도록 코드와 분리했다.
    /// </summary>
    [CreateAssetMenu(fileName = "LoadingTips", menuName = "RPGGame/Loading Tip Collection")]
    public class LoadingTipCollection : ScriptableObject
    {
        [TextArea(2, 4)]
        [SerializeField] private string[] tips;

        public string[] Tips => tips;

        // 마지막으로 보여준 팁의 인덱스. 씬 로딩과 텔레포트가 같은 목록을 이어서 돌도록 애셋 단위로 기억한다.
        // 정적 필드가 아니라 인스턴스 필드인 이유: 팁 목록이 여러 개면 각각 따로 순환해야 한다.
        // 애셋에 저장되지 않도록 NonSerialized로 둔다
        [System.NonSerialized] private int lastTipIndex = -1;

        // 목록을 처음부터 순서대로 하나씩 돌려주고, 끝나면 다시 처음으로 돌아간다
        public string NextTip()
        {
            if (tips == null || tips.Length == 0)
            {
                return null;
            }

            lastTipIndex = (lastTipIndex + 1) % tips.Length;

            return tips[lastTipIndex];
        }
    }
}
