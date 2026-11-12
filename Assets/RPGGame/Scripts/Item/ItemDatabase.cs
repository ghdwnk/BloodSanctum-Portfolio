using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 게임에 있는 모든 아이템 에셋 목록.
    ///
    /// 세이브 파일에는 아이템을 에셋 이름(문자열)으로 저장하므로, 불러올 때 GameSaveLoader가 이 목록에서
    /// 이름으로 실제 에셋을 찾는다. 테스트 도구(GameTestTools)의 "모든 아이템 지급"도 이 목록을 쓴다.
    /// 에셋은 Resources/Data/Item Database에 있고 DataManager가 불러 둔다.
    /// 새 아이템 에셋을 만들면 반드시 이 목록에도 넣어야 세이브·불러오기가 된다.
    /// </summary>
    [CreateAssetMenu(fileName = "Item Database", menuName = "Scriptable Objects/Item Database")]
    public class ItemDatabase : ScriptableObject
    {
        [Tooltip("게임에 있는 모든 아이템 에셋")]
        public Item[] items;

        /// <summary>
        /// 에셋 이름(파일 이름)으로 아이템을 찾는다. 없으면 null.
        /// 불러오기 때만 쓰고 목록이 20개 남짓이라, 사전(Dictionary)을 따로 만들지 않고 처음부터 훑는다.
        /// </summary>
        public Item FindByName(string assetName)
        {
            foreach (Item item in items)
            {
                if (item != null && item.name == assetName)
                {
                    return item;
                }
            }

            return null;
        }
    }
}
