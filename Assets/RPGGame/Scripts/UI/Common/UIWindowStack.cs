using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 열려 있는 UI 창을 연 순서대로 기억해 두었다가 ESC로 하나씩 닫게 해 주는 도우미.
    /// 각 창의 Show/Close 코드를 고치지 않고, 매 프레임 "열려 있는가"만 살펴서 순서를 기록한다
    /// (단축키·버튼·NPC 상호작용 등 어떤 방법으로 열고 닫아도 같은 기준으로 따라간다).
    /// 새 창을 ESC로 닫을 수 있게 하려면 아래 Entries에 한 줄만 추가하면 된다
    /// </summary>
    public static class UIWindowStack
    {
        // true: 가장 나중에 연 창부터 닫는다 (겹쳐 열린 창은 위에 있는 것부터 걷어내는 일반적인 방식)
        // false: 가장 먼저 연 창부터 닫는다
        private const bool CloseNewestFirst = true;

        private struct Entry
        {
            public Func<bool> IsOn;
            public Action Close;
        }

        // ESC로 닫을 수 있는 창들. 수량 입력창·시스템 메시지처럼 자기 안에서 ESC를 처리하는 모달은 넣지 않는다
        private static readonly Entry[] Entries =
        {
            new Entry { IsOn = () => UIInventoryWindow.IsOn, Close = UIInventoryWindow.Close },
            new Entry { IsOn = () => UIQuestWindow.IsOn, Close = UIQuestWindow.Close },
            new Entry { IsOn = () => UISkillWindow.IsOn, Close = UISkillWindow.Close },
            new Entry { IsOn = () => UICharacterStatusWindow.IsOn, Close = UICharacterStatusWindow.Close },
            new Entry { IsOn = () => UIShopWindow.IsOn, Close = UIShopWindow.Close },
            new Entry { IsOn = () => UIEnhanceWindow.IsOn, Close = UIEnhanceWindow.Close },
            new Entry { IsOn = () => UITeleportWindow.IsOn, Close = UITeleportWindow.Close }
        };

        // 연 순서대로 쌓은 창 번호 (Entries의 인덱스). 앞쪽이 먼저 열린 창이다
        private static readonly List<int> openOrder = new List<int>();

        // 수량 입력창·시스템 메시지가 이번 프레임의 ESC를 이미 썼는지 판단하는 데 쓴다
        private static int escapeConsumedFrame = -1;

        public static bool EscapeConsumedThisFrame => escapeConsumedFrame == Time.frameCount;

        // 모달이 ESC를 처리했음을 알린다. 같은 프레임에 ESC가 한 번 더 처리돼 환경설정창이 뜨는 것을 막는다
        public static void ConsumeEscape()
        {
            escapeConsumedFrame = Time.frameCount;
        }

        // 지금 열려 있는 창 목록을 기록과 맞춘다. 새로 열린 창은 맨 뒤에 붙이고, 닫힌 창은 뺀다
        public static void Refresh()
        {
            for (int ix = openOrder.Count - 1; ix >= 0; --ix)
            {
                if (!Entries[openOrder[ix]].IsOn())
                {
                    openOrder.RemoveAt(ix);
                }
            }

            for (int ix = 0; ix < Entries.Length; ++ix)
            {
                if (Entries[ix].IsOn() && !openOrder.Contains(ix))
                {
                    openOrder.Add(ix);
                }
            }
        }

        /// <summary>열려 있는 창을 전부 닫는다 (엔딩 후 마을 이동처럼 화면이 통째로 바뀔 때 쓴다).</summary>
        public static void CloseAll()
        {
            for (int ix = 0; ix < Entries.Length; ++ix)
            {
                if (Entries[ix].IsOn())
                {
                    Entries[ix].Close();
                }
            }

            openOrder.Clear();
        }

        /// <summary>열린 창이 있으면 하나만 닫고 true를 돌려준다. 하나도 없으면 false다.</summary>
        public static bool TryCloseNext()
        {
            Refresh();

            if (openOrder.Count == 0)
            {
                return false;
            }

            int position = CloseNewestFirst ? openOrder.Count - 1 : 0;
            int entryIndex = openOrder[position];
            openOrder.RemoveAt(position);

            Entries[entryIndex].Close();
            return true;
        }
    }
}
