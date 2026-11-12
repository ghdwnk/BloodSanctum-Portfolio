using System.Collections.Generic;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 상점 하나가 파는 물건 목록(품목·가격·구매 제한 레벨)과 되팔기 비율.
    ///
    /// 에셋: Assets/RPGGame/Data/Shop/General Store(잡화 상점). 게임 씬의 ShopController가 참조한다.
    /// 코드를 고치지 않고 이 에셋만 바꿔 취급 품목과 가격을 조절할 수 있다.
    /// </summary>
    [CreateAssetMenu(fileName = "New Shop Data", menuName = "Scriptable Objects/Shop Data")]
    public class ShopData : ScriptableObject
    {
        /// <summary>판매 목록의 한 줄.</summary>
        [System.Serializable]
        public class Entry
        {
            [Tooltip("파는 아이템")]
            public Item item;
            [Tooltip("1개 가격 (골드)")]
            public int price = 100;
            [Tooltip("이 레벨 미만이면 살 수 없다")]
            public int requiredLevel = 1;
        }

        [Tooltip("상점 창 제목")]
        public string shopName = "잡화 상점";

        [Tooltip("되팔 때 받는 비율 (목록 가격 기준). 아이템 에셋에 판매가(sellPrice)가 따로 있으면 그 값이 우선이다")]
        [Range(0f, 1f)] public float sellRatio = 0.3f;

        [Tooltip("판매 목록. 이 순서대로 상점 창에 보인다")]
        public List<Entry> entries = new();
    }
}
