using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace RPGGame
{
    /// <summary>
    /// 대사 속 지역/인물 이름에 색을 입혀 강조하는 도우미.
    /// 대사 데이터(CSV)에 색 태그를 일일이 적지 않아도 되고, 이름이 바뀌어도 이 표만 고치면 된다.
    /// 지역 색은 마을 포탈 색(초록/파랑/보라/빨강)과 맞춰 "이 이름 = 저 포탈"로 바로 연결되게 했다
    /// </summary>
    public static class DialogueHighlighter
    {
        // 강조할 단어와 색. 긴 단어가 먼저 매칭되도록 정규식을 만들 때 길이 순으로 정렬한다
        private static readonly Dictionary<string, string> Keywords = new Dictionary<string, string>
        {
            // 지역
            { "초심자의 소굴", "#7BE07B" },
            { "황무지 동굴", "#6EC6FF" },
            { "심연의 소굴", "#C58BFF" },
            { "무너진 성채 터", "#FF7A7A" },
            { "성채 터", "#FF7A7A" },
            { "애쉬헤이븐", "#FFC857" },
            { "바엘스라", "#FFC857" },
            { "블러드생텀", "#FF6B6B" },
            // 인물
            { "세리나", "#8FE0FF" },
            { "카이드로스", "#8FE0FF" },
            { "가론", "#8FE0FF" },
            { "아엘브리스", "#FFD24A" },
            { "자라톤", "#FF6B6B" },
            { "드렉 말로스", "#FF6B6B" },
            // 사물
            { "붉은 수정 왕좌", "#FF7A7A" },
        };

        private static readonly Regex Pattern = new Regex(
            string.Join("|", Keywords.Keys.OrderByDescending(k => k.Length).Select(Regex.Escape)),
            RegexOptions.Compiled);

        // 이미 색 태그가 들어 있는 대사(보스 대사의 화자 표기 등)는 건드리지 않는다
        public static string Apply(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Contains("<color"))
            {
                return text;
            }

            return Pattern.Replace(text, match => $"<color={Keywords[match.Value]}>{match.Value}</color>");
        }
    }
}
